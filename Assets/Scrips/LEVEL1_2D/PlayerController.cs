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

    [Header("=== 技能数值 ===")]
    public float dashSpeed = 25f;
    public float dashDuration = 0.2f;
    public float dashCooldown = 1f;
    public float diveSpeed = 40f; // 下坠速度要快

    // --- 状态标记 ---
    private Rigidbody2D rb;
    private bool isGrounded;
    private bool isFacingRight = true;
    
    // 技能状态
    private bool isDashing = false;
    private bool canDash = true;
    public bool IsDashing => isDashing; // 给敌人脚本读取的属性
    public bool IsDiving { get; private set; } // 下坠状态

    private float defaultGravity;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        defaultGravity = rb.gravityScale;
    }

    void Update()
    {
        // 如果正在转场（比如播放跳跃动画），禁止操作
        if (GameManager.Instance.isTransitioning) 
        {
            rb.SetVelocity(Vector2.zero);
            return;
        }

        // 0. 地面检测
        isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);

        // 落地后取消下坠状态
        if (isGrounded) IsDiving = false;

        // 冲刺中禁止其他操作
        if (isDashing) return;

        // 1. 左右移动
        float xInput = Input.GetAxisRaw("Horizontal");
        // 保持Y轴速度（除非在下坠）
        float yVelocity = IsDiving ? -diveSpeed : rb.GetVelocity().y;
        rb.SetVelocity(new Vector2(xInput * moveSpeed, yVelocity));

        // 翻转朝向
        if (xInput > 0 && !isFacingRight) Flip();
        else if (xInput < 0 && isFacingRight) Flip();

        // 2. 跳跃
        if (Input.GetButtonDown("Jump") && isGrounded)
        {
            rb.SetVelocity(new Vector2(rb.GetVelocity().x, jumpForce));
        }

        // 3. 技能：次元突刺 (需等级解锁)
        if (Input.GetKeyDown(KeyCode.LeftShift) && canDash)
        {
            if (GameManager.Instance.CanUseDash())
            {
                StartCoroutine(DashCoroutine());
            }
            else
            {
                Debug.Log($"等级不足！{GameManager.Instance.dashUnlockLevel}级解锁突刺");
            }
        }

        // 4. 技能：裂空下坠 (需等级解锁)
        // 空中 + 按下S键
        if (!isGrounded && Input.GetKeyDown(KeyCode.S))
        {
            if (GameManager.Instance.CanUseDive())
            {
                // 开启下坠
                IsDiving = true;
                // 给一个瞬间向下的爆发力
                rb.SetVelocity(new Vector2(0, -diveSpeed));
            }
            else
            {
                Debug.Log($"等级不足！{GameManager.Instance.diveUnlockLevel}级解锁下坠");
            }
        }
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

        yield return new WaitForSeconds(dashDuration);

        // 结束
        rb.gravityScale = originalGrav;
        rb.SetVelocity(Vector2.zero); // 停顿一下增加打击感
        isDashing = false;

        // 进入冷却
        yield return new WaitForSeconds(dashCooldown);
        canDash = true;
    }

    // 【爽点核心】击杀敌人后调用此方法，立即重置突刺
    public void InstantResetDash()
    {
        Debug.Log("击杀重置突刺！");
        canDash = true;
        StopCoroutine("DashCoroutine");
        isDashing = false;
        rb.gravityScale = defaultGravity;
        // 可以在这里加一个屏幕震动或者特效
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