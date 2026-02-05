using UnityEngine;
using System.Collections;

public class PlayerController : MonoBehaviour
{
    [Header("=== 基础属性 ===")]
    public float moveSpeed = 8f;
    public float jumpForce = 16f;
    public LayerMask groundLayer;
    public Transform groundCheck;
    public float groundCheckRadius = 0.2f;

    [Header("=== 技能：次元突刺 ===")]
    public float dashSpeed = 25f;
    public float dashDuration = 0.2f;
    public float dashCooldown = 1f;
    
    [Header("=== 技能：裂口下坠 (无CD) ===")]
    public float diveSpeed = 45f;         // 下坠速度
    public float diveDamageBonus = 1.5f;  // 下坠伤害加成

    // --- 状态标记 ---
    private Rigidbody2D rb;
    private bool isGrounded;
    private bool isFacingRight = true;
    
    // 技能状态
    private bool isDashing = false;
    private bool canDash = true;
    public bool IsDashing => isDashing;
    public bool IsDiving { get; private set; }

    private float defaultGravity;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        defaultGravity = rb.gravityScale;
    }

    void Update()
    {
        // 如果正在转场（比如播放跳跃动画），禁止操作
        if (GameManager.Instance != null && GameManager.Instance.isTransitioning) 
        {
            rb.SetVelocity(Vector2.zero);
            return;
        }

        // 0. 地面检测
        isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);

        // 落地后取消下坠状态
        if (isGrounded && IsDiving) 
        {
            IsDiving = false;
            // 落地瞬间可以产生一个震地效果
            // TODO: 添加震地特效和AOE伤害
        }

        // 冲刺中禁止其他操作
        if (isDashing) return;

        // 1. 左右移动
        float xInput = Input.GetAxisRaw("Horizontal");
        float yVelocity = IsDiving ? -diveSpeed : rb.GetVelocity().y;
        
        // 下坠时保留一部分水平速度
        float xVelocity = IsDiving ? rb.GetVelocity().x * 0.95f : xInput * moveSpeed;
        rb.SetVelocity(new Vector2(xVelocity, yVelocity));

        // 翻转朝向（只在不下坠时）
        if (!IsDiving)
        {
            if (xInput > 0 && !isFacingRight) Flip();
            else if (xInput < 0 && isFacingRight) Flip();
        }

        // 2. 跳跃
        if (Input.GetButtonDown("Jump") && isGrounded)
        {
            rb.SetVelocity(new Vector2(rb.GetVelocity().x, jumpForce));
        }

        // 3. 技能：次元突刺 (需等级解锁，有CD)
        if (Input.GetKeyDown(KeyCode.LeftShift) && canDash)
        {
            if (GameManager.Instance.CanUseDash())
            {
                StartCoroutine(DashCoroutine());
            }
            else
            {
                Debug.Log($"等级不足！需要 Lv.{GameManager.Instance.dashUnlockLevel} 解锁【次元突刺】");
            }
        }

        // 4. 技能：裂口下坠 (需等级解锁，无CD！)
        // 空中 + 按下S键 + 不在下坠状态
        if (!isGrounded && Input.GetKeyDown(KeyCode.S) && !IsDiving)
        {
            if (GameManager.Instance.CanUseDive())
            {
                ActivateDive();
            }
            else
            {
                Debug.Log($"等级不足！需要 Lv.{GameManager.Instance.diveUnlockLevel} 解锁【裂口下坠】");
            }
        }
    }

    /// <summary>
    /// 激活裂口下坠（无CD设计，但需要等级）
    /// </summary>
    void ActivateDive()
    {
        IsDiving = true;
        
        // 给一个瞬间向下的爆发力，保留部分水平速度
        float currentX = rb.GetVelocity().x;
        rb.SetVelocity(new Vector2(currentX * 0.3f, -diveSpeed));
        
        // TODO: 下坠特效（残影、拖尾等）
        Debug.Log("裂口下坠！");
    }

    // --- 突刺逻辑 ---
    IEnumerator DashCoroutine()
    {
        isDashing = true;
        canDash = false;
        
        // 突刺时无视重力
        float originalGrav = rb.gravityScale;
        rb.gravityScale = 0;

        // 向面朝方向冲
        float dir = isFacingRight ? 1f : -1f;
        rb.SetVelocity(new Vector2(dir * dashSpeed, 0));
        
        // TODO: 生成残影特效

        yield return new WaitForSeconds(dashDuration);

        // 结束
        rb.gravityScale = originalGrav;
        rb.SetVelocity(Vector2.zero); // 停顿一下增加打击感
        isDashing = false;

        // 进入冷却
        yield return new WaitForSeconds(dashCooldown);
        canDash = true;
    }

    /// <summary>
    /// 【爽点核心】击杀敌人后调用此方法，立即重置突刺
    /// </summary>
    public void InstantResetDash()
    {
        Debug.Log("★ 击杀重置突刺！");
        canDash = true;
        StopCoroutine("DashCoroutine");
        isDashing = false;
        rb.gravityScale = defaultGravity;
        
        // TODO: 屏幕震动特效
        // CameraShake.Instance?.Shake(0.1f, 0.2f);
    }

    void Flip()
    {
        isFacingRight = !isFacingRight;
        Vector3 scaler = transform.localScale;
        scaler.x *= -1;
        transform.localScale = scaler;
    }

    // 辅助线
    void OnDrawGizmos()
    {
        if(groundCheck) Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
    }
}