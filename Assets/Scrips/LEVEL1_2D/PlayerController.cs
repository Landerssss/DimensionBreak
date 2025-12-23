using UnityEngine;
using System.Collections;

public class PlayerController : MonoBehaviour
{
    [Header("=== 基础属性 ===")]
    public float moveSpeed = 8f;
    public float jumpForce = 16f;
    public LayerMask groundLayer;
    public Transform groundCheck; // 在主角脚底放一个空物体
    public float groundCheckRadius = 0.2f;

    [Header("=== 技能：次元突刺 (Dash) ===")]
    public float dashSpeed = 25f;
    public float dashDuration = 0.2f;
    public float dashCooldown = 1f;
    public GameObject dashAfterImagePrefab; // (可选) 残影特效预制体

    [Header("=== 技能：滞空 (Float) ===")]
    public float floatGravity = 0.5f; // 滞空时的重力比例
    public float maxFloatTime = 2f; // 最长滞空时间

    [Header("=== 技能：裂空下坠 (Dive) ===")]
    public float diveSpeed = 30f;
    
    // --- 内部状态 ---
    private Rigidbody2D rb;
    private bool isGrounded;
    private bool isDashing;
    private bool canDash = true;
    private bool isFacingRight = true;
    private float defaultGravity;
    private float currentFloatTime;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        defaultGravity = rb.gravityScale;
    }

    void Update()
    {
        // 0. 地面检测
        isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
        
        // 如果在冲刺中，禁止其他输入
        if (isDashing) return;

        // 1. 基础移动
        float moveInput = Input.GetAxisRaw("Horizontal");
        rb.linearVelocity = new Vector2(moveInput * moveSpeed, rb.linearVelocity.y);

        // 翻转角色朝向
        if (moveInput > 0 && !isFacingRight) Flip();
        else if (moveInput < 0 && isFacingRight) Flip();

        // 2. 跳跃
        if (Input.GetButtonDown("Jump") && isGrounded)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
        }

        // 3. 技能：次元突刺 (按 Shift 或 K)
        if (Input.GetKeyDown(KeyCode.LeftShift) && canDash)
        {
            StartCoroutine(DashCoroutine());
        }

        // 4. 技能：滞空 (空中按住跳跃键)
        if (Input.GetButton("Jump") && !isGrounded && rb.linearVelocity.y < 0)
        {
            // 只有下落时才能滞空
            rb.gravityScale = floatGravity; 
            // 这里可以加一个计时器限制滞空时间，防止无限飞
        }
        else
        {
            rb.gravityScale = defaultGravity;
        }

        // 5. 技能：裂空下坠 (空中按 下 + 攻击/跳跃，这里设为 S 键)
        if (!isGrounded && Input.GetKeyDown(KeyCode.S))
        {
            // 瞬间向下的速度
            rb.linearVelocity = new Vector2(0, -diveSpeed);
            // 可以加一个状态标记 isDiving，用于碰撞检测时造成AOE
        }
    }

    // --- 核心技能逻辑：突刺 ---
    IEnumerator DashCoroutine()
    {
        isDashing = true;
        canDash = false;
        
        // 记录原始重力并设为0，防止突刺时下坠
        float originalGravity = rb.gravityScale;
        rb.gravityScale = 0;
        
        // 施加突刺速度 (朝向当前方向)
        float direction = isFacingRight ? 1f : -1f;
        rb.linearVelocity = new Vector2(direction * dashSpeed, 0);

        // TODO: 这里可以生成残影特效

        yield return new WaitForSeconds(dashDuration);

        // 结束突刺
        rb.gravityScale = originalGravity;
        rb.linearVelocity = Vector2.zero; // 停顿一下增加打击感，或者保留部分惯性
        isDashing = false;

        // 冷却
        yield return new WaitForSeconds(dashCooldown);
        canDash = true;
    }

    // --- 碰撞检测：处理杀怪逻辑 ---
    void OnTriggerEnter2D(Collider2D other)
    {
        // 如果撞到了敌人
        if (other.CompareTag("Enemy"))
        {
            SimpleEnemy enemy = other.GetComponent<SimpleEnemy>();
            
            // 判定1：如果是突刺状态撞到的 -> 秒杀并重置CD
            if (isDashing)
            {
                if(enemy != null) enemy.TakeDamage(100); // 造成巨大伤害
                ResetDash(); // 【爽点核心】击杀重置！
                Debug.Log("次元突刺击杀！重置冷却！");
            }
            // 判定2：如果是下坠状态 (速度非常快向下)
            else if (rb.linearVelocity.y < -20f) 
            {
                 if(enemy != null) enemy.TakeDamage(50);
                 // 下坠命中后弹起
                 rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce * 0.8f);
            }
            // 判定3：普通碰撞 -> 主角受伤 (略)
        }
        else if (other.CompareTag("Portal"))
        {
            Debug.Log("进入维度裂缝！切换塔防模式！");

            // 1. 禁用主角控制
            this.enabled = false; 
            rb.linearVelocity = Vector2.zero;
            // 可以把主角隐藏，或者播放一个消失动画
            // gameObject.SetActive(false); 

            // 2. 通知摄像机切换
            Camera.main.GetComponent<CameraDimensionController>().SwitchToTDMode();

            // 3. 通知塔防系统生成网格
            FindObjectOfType<TowerGridSystem>().GenerateGrid();
        }
    }

    // 【爽点核心】重置突刺
    public void ResetDash()
    {
        canDash = true;
        StopCoroutine("DashCoroutine"); // 停止当前的协程逻辑（如果需要立即打断）
        // 恢复重力防止卡在空中（看设计需求，也可以不恢复让你继续飞）
        rb.gravityScale = defaultGravity; 
        isDashing = false; 
    }

    void Flip()
    {
        isFacingRight = !isFacingRight;
        Vector3 scaler = transform.localScale;
        scaler.x *= -1;
        transform.localScale = scaler;
    }

    // 辅助线显示
    void OnDrawGizmos()
    {
        if (groundCheck != null)
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
    }
}
