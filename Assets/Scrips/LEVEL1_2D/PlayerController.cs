using UnityEngine;
using System.Collections;

/// <summary>
/// 阶段一玩家控制器：移动 / 跳跃 / 冲刺 / 攻击 / 钩锁 / 下坠攻击
/// 所有数值均通过 [SerializeField] 暴露到 Inspector。
/// </summary>
public class PlayerController : MonoBehaviour
{
    // ────────────────── 基础移动 ──────────────────
    [Header("=== 基础移动 ===")]
    [SerializeField] private float moveSpeed = 8f;
    [SerializeField] private float jumpForce = 16f;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckRadius = 0.2f;

    // ────────────────── 冲刺 (Shift) ──────────────────
    [Header("=== 冲刺 ===")]
    [SerializeField] private float dashSpeed = 25f;
    [SerializeField] private float dashDuration = 0.2f;
    [SerializeField] private float dashCooldown = 1f;
    [Tooltip("达到此等级后冲刺附带路径伤害")]
    [SerializeField] private int dashAttackUnlockLevel = 20;
    [SerializeField] private float dashAttackDamageMultiplier = 2f;
    [SerializeField] private float dashAttackWidth = 1f;
    [SerializeField] private LayerMask enemyLayer;

    // ────────────────── 近战攻击 (左键) ──────────────────
    [Header("=== 近战攻击 ===")]
    [SerializeField] private float attackRange = 1.5f;
    [SerializeField] private Transform attackPoint;
    [SerializeField] private float attackCooldown = 0.35f;

    // ────────────────── 钩锁 (T / 右键) ──────────────────
    [Header("=== 钩锁 ===")]
    [SerializeField] private float grappleSpeed = 20f;
    [SerializeField] private float grappleMaxDistance = 15f;
    [SerializeField] private float grappleArriveThreshold = 0.5f;
    [SerializeField] private LayerMask grappleObstacleLayer;
    [SerializeField] private LineRenderer grappleLineRenderer;

    // ────────────────── 下坠攻击 (空中双击S) ──────────────────
    [Header("=== 下坠攻击 ===")]
    [SerializeField] private float diveSpeed = 45f;
    [SerializeField] private float diveAOERadius = 2f;
    [SerializeField] private float diveDamageMultiplier = 1.5f;
    [SerializeField] private float diveCooldown = 1.5f;
    [Tooltip("双击S的判定间隔（秒）")]
    [SerializeField] private float doubleTapWindow = 0.3f;

    // ────────────────── 内部状态 ──────────────────
    private Rigidbody2D rb;
    private bool isGrounded;
    private bool isFacingRight = true;
    private float defaultGravity;

    // 冲刺
    private bool isDashing;
    private bool canDash = true;
    public bool IsDashing => isDashing;

    // 下坠
    public bool IsDiving { get; private set; }
    private bool canDive = true;
    private float lastSPressTime = -999f;

    // 攻击
    private float lastAttackTime = -999f;

    // 钩锁
    private bool isGrappling;
    private Vector2 grappleTarget;

    // 引用
    private PlayerStats playerStats;

    // ══════════════════ 生命周期 ══════════════════

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        defaultGravity = rb.gravityScale;
        playerStats = GetComponent<PlayerStats>();

        if (grappleLineRenderer != null)
            grappleLineRenderer.enabled = false;
    }

    void Update()
    {
        // 转场中禁止操作
        if (GameManager.Instance != null && GameManager.Instance.isTransitioning)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        // 地面检测
        isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);

        // 落地后结束下坠状态 —— 触发 AOE
        if (isGrounded && IsDiving)
        {
            IsDiving = false;
            PerformDiveAOE();
        }

        // 钩锁移动中
        if (isGrappling)
        {
            HandleGrappleMovement();
            return;
        }

        // 冲刺中禁止其他操作
        if (isDashing) return;

        HandleMovement();
        HandleJump();
        HandleDash();
        HandleAttack();
        HandleGrappleInput();
        HandleDiveInput();
    }

    // ══════════════════ 移动 ══════════════════

    void HandleMovement()
    {
        float xInput = Input.GetAxisRaw("Horizontal");
        float yVelocity = IsDiving ? -diveSpeed : rb.linearVelocity.y;
        float xVelocity = IsDiving ? rb.linearVelocity.x * 0.95f : xInput * moveSpeed;

        rb.linearVelocity = new Vector2(xVelocity, yVelocity);

        if (!IsDiving)
        {
            if (xInput > 0 && !isFacingRight) Flip();
            else if (xInput < 0 && isFacingRight) Flip();
        }
    }

    void HandleJump()
    {
        if (Input.GetButtonDown("Jump") && isGrounded)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
        }
    }

    // ══════════════════ 冲刺 ══════════════════

    void HandleDash()
    {
        if (Input.GetKeyDown(KeyCode.LeftShift) && canDash)
        {
            StartCoroutine(DashCoroutine());
        }
    }

    IEnumerator DashCoroutine()
    {
        isDashing = true;
        canDash = false;

        float originalGrav = rb.gravityScale;
        rb.gravityScale = 0;

        float dir = isFacingRight ? 1f : -1f;
        rb.linearVelocity = new Vector2(dir * dashSpeed, 0);

        // 突刺进化：达到等级后路径上检测敌人并造成伤害
        bool hasDashAttack = playerStats != null && playerStats.CurrentLevel >= dashAttackUnlockLevel;
        if (hasDashAttack)
        {
            DashAttackSweep(dir);
        }

        yield return new WaitForSeconds(dashDuration);

        rb.gravityScale = originalGrav;
        rb.linearVelocity = Vector2.zero;
        isDashing = false;

        yield return new WaitForSeconds(dashCooldown);
        canDash = true;
    }

    /// <summary>
    /// 冲刺路径伤害扫描
    /// </summary>
    void DashAttackSweep(float direction)
    {
        float baseDmg = playerStats != null ? playerStats.GetFinalDamage(dashAttackDamageMultiplier) : 10f;

        Vector2 origin = (Vector2)transform.position;
        Vector2 size = new Vector2(dashSpeed * dashDuration, dashAttackWidth);
        Vector2 dashDir = new Vector2(direction, 0);

        RaycastHit2D[] hits = Physics2D.BoxCastAll(origin, size, 0f, dashDir, dashSpeed * dashDuration, enemyLayer);
        foreach (var hit in hits)
        {
            EnemyAI enemy = hit.collider.GetComponent<EnemyAI>();
            if (enemy != null)
            {
                enemy.TakeDamage(baseDmg);
            }
        }
    }

    public void InstantResetDash()
    {
        canDash = true;
        StopCoroutine(nameof(DashCoroutine));
        isDashing = false;
        rb.gravityScale = defaultGravity;
    }

    // ══════════════════ 近战攻击 ══════════════════

    void HandleAttack()
    {
        if (Input.GetMouseButtonDown(0) && Time.time >= lastAttackTime + attackCooldown)
        {
            lastAttackTime = Time.time;
            PerformMeleeAttack();
        }
    }

    void PerformMeleeAttack()
    {
        float skillMultiplier = 1f; // 基础普攻倍率
        float dmg = playerStats != null ? playerStats.GetFinalDamage(skillMultiplier) : 10f;

        Collider2D[] enemies = Physics2D.OverlapCircleAll(
            attackPoint != null ? attackPoint.position : transform.position,
            attackRange, enemyLayer);

        foreach (var col in enemies)
        {
            EnemyAI enemy = col.GetComponent<EnemyAI>();
            if (enemy != null)
            {
                enemy.TakeDamage(dmg);
            }
        }
    }

    // ══════════════════ 钩锁 ══════════════════

    void HandleGrappleInput()
    {
        if (Input.GetKeyDown(KeyCode.T) || Input.GetMouseButtonDown(1))
        {
            LaunchGrapple();
        }
    }

    void LaunchGrapple()
    {
        Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mouseWorld.z = 0;

        Vector2 origin = transform.position;
        Vector2 dir = ((Vector2)mouseWorld - origin).normalized;
        float dist = Mathf.Min(Vector2.Distance(origin, mouseWorld), grappleMaxDistance);

        RaycastHit2D hit = Physics2D.Raycast(origin, dir, dist, grappleObstacleLayer);
        if (hit.collider != null)
        {
            grappleTarget = hit.point;
            isGrappling = true;
            rb.gravityScale = 0;

            if (grappleLineRenderer != null)
            {
                grappleLineRenderer.enabled = true;
                grappleLineRenderer.SetPosition(0, transform.position);
                grappleLineRenderer.SetPosition(1, grappleTarget);
            }
        }
    }

    void HandleGrappleMovement()
    {
        Vector2 pos = (Vector2)transform.position;
        Vector2 direction = (grappleTarget - pos).normalized;
        rb.linearVelocity = direction * grappleSpeed;

        if (grappleLineRenderer != null)
            grappleLineRenderer.SetPosition(0, transform.position);

        if (Vector2.Distance(pos, grappleTarget) < grappleArriveThreshold)
        {
            EndGrapple();
        }

        // 允许玩家提前取消
        if (Input.GetKeyDown(KeyCode.T) || Input.GetMouseButtonDown(1) || Input.GetButtonDown("Jump"))
        {
            EndGrapple();
        }
    }

    void EndGrapple()
    {
        isGrappling = false;
        rb.gravityScale = defaultGravity;
        rb.linearVelocity = Vector2.zero;

        if (grappleLineRenderer != null)
            grappleLineRenderer.enabled = false;
    }

    // ══════════════════ 下坠攻击 ══════════════════

    void HandleDiveInput()
    {
        if (!isGrounded && Input.GetKeyDown(KeyCode.S) && canDive)
        {
            if (Time.time - lastSPressTime <= doubleTapWindow)
            {
                // 双击 S 确认
                ActivateDive();
                lastSPressTime = -999f; // 重置
            }
            else
            {
                lastSPressTime = Time.time;
            }
        }
    }

    void ActivateDive()
    {
        IsDiving = true;
        canDive = false;

        float currentX = rb.linearVelocity.x;
        rb.linearVelocity = new Vector2(currentX * 0.3f, -diveSpeed);

        Debug.Log("下坠攻击！");
    }

    void PerformDiveAOE()
    {
        float dmg = playerStats != null ? playerStats.GetFinalDamage(diveDamageMultiplier) : 15f;

        Collider2D[] enemies = Physics2D.OverlapCircleAll(transform.position, diveAOERadius, enemyLayer);
        foreach (var col in enemies)
        {
            EnemyAI enemy = col.GetComponent<EnemyAI>();
            if (enemy != null)
            {
                enemy.TakeDamage(dmg);
            }
        }

        // 反弹
        rb.linearVelocity = new Vector2(0, jumpForce * 0.5f);

        StartCoroutine(DiveCooldownCoroutine());
    }

    IEnumerator DiveCooldownCoroutine()
    {
        yield return new WaitForSeconds(diveCooldown);
        canDive = true;
    }

    // ══════════════════ 工具方法 ══════════════════

    void Flip()
    {
        isFacingRight = !isFacingRight;
        Vector3 scaler = transform.localScale;
        scaler.x *= -1;
        transform.localScale = scaler;
    }

    void OnDrawGizmosSelected()
    {
        if (groundCheck != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }

        // 攻击范围
        Gizmos.color = Color.red;
        Vector3 atkPos = attackPoint != null ? attackPoint.position : transform.position;
        Gizmos.DrawWireSphere(atkPos, attackRange);

        // 下坠 AOE 范围
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, diveAOERadius);
    }
}