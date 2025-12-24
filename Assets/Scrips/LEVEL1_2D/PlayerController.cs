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
    // private float currentFloatTime; // 暂时未使用的变量，先保留

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
        
        // 【修复点1】使用 SetVelocity(...) 方法，而不是赋值
        // 【修复点2】获取Y轴速度使用 GetVelocity().y
        rb.SetVelocity(new Vector2(moveInput * moveSpeed, rb.GetVelocity().y));

        // 翻转角色朝向
        if (moveInput > 0 && !isFacingRight) Flip();
        else if (moveInput < 0 && isFacingRight) Flip();

        // 2. 跳跃
        if (Input.GetButtonDown("Jump") && isGrounded)
        {
            // 【修复点3】同上
            rb.SetVelocity(new Vector2(rb.GetVelocity().x, jumpForce));
        }

        // 3. 技能：次元突刺 (按 Shift 或 K)
        if (Input.GetKeyDown(KeyCode.LeftShift) && canDash)
        {
            StartCoroutine(DashCoroutine());
        }

        // 4. 技能：滞空 (空中按住跳跃键)
        // 【修复点4】将 linearVelocity.y 改为 GetVelocity().y
        if (Input.GetButton("Jump") && !isGrounded && rb.GetVelocity().y < 0)
        {
            // 只有下落时才能滞空
            rb.gravityScale = floatGravity; 
        }
        else
        {
            rb.gravityScale = defaultGravity;
        }

        // 5. 技能：裂空下坠 (空中按 S 键)
        if (!isGrounded && Input.GetKeyDown(KeyCode.S))
        {
            // 【修复点5】使用 SetVelocity(...)
            rb.SetVelocity(new Vector2(0, -diveSpeed));
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
        // 【修复点6】使用 SetVelocity(...)
        rb.SetVelocity(new Vector2(direction * dashSpeed, 0));

        // TODO: 这里可以生成残影特效

        yield return new WaitForSeconds(dashDuration);

        // 结束突刺
        rb.gravityScale = originalGravity;
        // 【修复点7】使用 SetVelocity(...)
        rb.SetVelocity(Vector2.zero); // 停顿一下增加打击感
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
                if(enemy != null) enemy.TakeDamage(100); 
                ResetDash(); // 【爽点核心】击杀重置！
                Debug.Log("次元突刺击杀！重置冷却！");
            }
            // 判定2：如果是下坠状态 (速度非常快向下)
            // 【修复点8】这里你之前漏了括号，且要用 GetVelocity()
            else if (rb.GetVelocity().y < -20f) 
            {
                 if(enemy != null) enemy.TakeDamage(50);
                 // 下坠命中后弹起
                 // 【修复点9】使用 SetVelocity(...)
                 rb.SetVelocity(new Vector2(rb.GetVelocity().x, jumpForce * 0.8f));
            }
            // 判定3：普通碰撞 -> 主角受伤 (略)
        }
        else if (other.CompareTag("Portal"))
        {
            Debug.Log("进入维度裂缝！切换塔防模式！");
            
            // 1. 禁用主角控制
            this.enabled = false; 
            
            // 【修复点10】使用 SetVelocity(...)
            rb.SetVelocity(Vector2.zero);
            
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
        StopCoroutine("DashCoroutine"); 
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
