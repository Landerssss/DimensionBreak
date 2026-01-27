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

    [Header("=== 技能配置 ===")]
    public float dashSpeed = 25f;
    public float dashCooldown = 1f;
    public float diveSpeed = 30f;

    // --- 内部状态 ---
    private Rigidbody2D rb;
    private bool isGrounded;
    private bool canDash = true;
    private bool isDashing = false;
    private bool isFacingRight = true;
    private float defaultGravity;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        defaultGravity = rb.gravityScale;
    }

    void Update()
    {
        // 如果正在剧情转场中，禁止操作
        if (GameManager.Instance.isTransitioning) 
        {
            rb.SetVelocity(Vector2.zero);
            return;
        }

        // 0. 地面检测
        isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);

        if (isDashing) return;

        // 1. 基础移动
        float moveInput = Input.GetAxisRaw("Horizontal");
        rb.SetVelocity(new Vector2(moveInput * moveSpeed, rb.GetVelocity().y));

        if (moveInput > 0 && !isFacingRight) Flip();
        else if (moveInput < 0 && isFacingRight) Flip();

        // 2. 跳跃 (无需等级)
        if (Input.GetButtonDown("Jump") && isGrounded)
        {
            rb.SetVelocity(new Vector2(rb.GetVelocity().x, jumpForce));
        }

        // 3. 技能一：次元突刺 (需 Lv.20)
        // 判定条件：按下键 + CD转好 + 技能已解锁
        if (Input.GetKeyDown(KeyCode.LeftShift) && canDash && GameManager.Instance.IsDashUnlocked())
        {
            StartCoroutine(DashCoroutine());
        }
        else if (Input.GetKeyDown(KeyCode.LeftShift) && !GameManager.Instance.IsDashUnlocked())
        {
            Debug.Log($"等级不足！需要 {GameManager.Instance.dashUnlockLevel} 级解锁突刺");
        }

        // 4. 技能二：裂空下坠 (需 Lv.50)
        // 判定条件：在空中 + 按下S + 技能已解锁
        if (!isGrounded && Input.GetKeyDown(KeyCode.S))
        {
            if (GameManager.Instance.IsDiveUnlocked())
            {
                // 下坠逻辑：速度快，伤害高
                rb.SetVelocity(new Vector2(0, -diveSpeed));
            }
            else
            {
                Debug.Log($"等级不足！需要 {GameManager.Instance.diveUnlockLevel} 级解锁下坠");
            }
        }
    }

    // --- 技能逻辑保持不变 ---
    IEnumerator DashCoroutine()
    {
        isDashing = true;
        canDash = false;
        float originalGravity = rb.gravityScale;
        rb.gravityScale = 0;
        
        float direction = isFacingRight ? 1f : -1f;
        rb.SetVelocity(new Vector2(direction * dashSpeed, 0));

        yield return new WaitForSeconds(0.2f); // 突刺时间

        rb.gravityScale = originalGravity;
        rb.SetVelocity(Vector2.zero);
        isDashing = false;
        yield return new WaitForSeconds(dashCooldown);
        canDash = true;
    }

    // 击杀重置接口
    public void ResetDash()
    {
        canDash = true;
        isDashing = false;
        rb.gravityScale = defaultGravity;
        StopCoroutine("DashCoroutine");
    }

    void Flip()
    {
        isFacingRight = !isFacingRight;
        Vector3 scaler = transform.localScale;
        scaler.x *= -1;
        transform.localScale = scaler;
    }
}