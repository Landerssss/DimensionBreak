using UnityEngine;
using System.Collections;

/// <summary>
/// 优化版玩家控制器：包含基础移动 / 二段跳 / 冲刺 / 攻击 / 虚空钩锁 / 下坠攻击
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

    // ────────────────── 二段跳 (等级解锁) ──────────────────
    [Header("=== 二段跳解锁 ===")]
    [Tooltip("达到此等级后解锁二段跳")]
    [SerializeField] private int doubleJumpUnlockLevel = 3;
    private int maxJumps = 1;      // 当前最大跳跃次数
    private int currentJumps = 0;  // 当前已跳跃次数

    // ────────────────── 冲刺 (Shift) ──────────────────
    [Header("=== 冲刺 ===")]
    [SerializeField] private float dashSpeed = 25f;
    [SerializeField] private float dashDuration = 0.2f;
    [SerializeField] private float dashCooldown = 1f;
    [SerializeField] private float dashAttackWidth = 1f;
    [SerializeField] private LayerMask enemyLayer;

    // ────────────────── 近战攻击 (左键) ──────────────────
    [Header("=== 近战攻击 ===")]
    [SerializeField] private float attackRange = 1.5f;
    [SerializeField] private Transform attackPoint;
    [SerializeField] private float attackCooldown = 0.35f;

    // ────────────────── 虚空钩锁 (T / 右键) ──────────────────
    [Header("=== 虚空钩锁 ===")]
    [SerializeField] private float grappleSpeed = 30f;
    [SerializeField] private float grappleMaxDistance = 10f;
    [SerializeField] private float grappleArriveThreshold = 0.5f;
    [SerializeField] private LineRenderer grappleLineRenderer;

    // ────────────────── 下坠攻击 (空中单压S) ──────────────────
    [Header("=== 下坠攻击 ===")]
    [SerializeField] private float diveSpeed = 45f;
    [SerializeField] private float diveAOERadius = 2f;
    [SerializeField] private float diveDamageMultiplier = 1.5f;
    [SerializeField] private float diveCooldown = 1.5f;

    // ────────────────── 内部状态 ──────────────────
    private Rigidbody2D rb;
    private bool isGrounded;
    private bool isFacingRight = true;
    private float defaultGravity;

    // 冲刺状态
    private bool isDashing;
    private bool canDash = true;
    public bool IsDashing => isDashing;

    // 下坠状态
    public bool IsDiving { get; private set; }
    private bool canDive = true;
    private float lastAttackTime = -999f;

    // 钩锁状态
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
        
        // 动态更新最大跳跃次数（根据等级判断是否允许二段跳）
        maxJumps = (playerStats != null && playerStats.CurrentLevel >= doubleJumpUnlockLevel) ? 2 : 1;
        
        if (isGrounded && !IsDiving)
        {
            currentJumps = 0; // 落地重置跳跃次数计数器
        }

        // 落地后结束下坠状态 —— 触发 AOE
        if (isGrounded && IsDiving)
        {
            IsDiving = false;
            PerformDiveAOE();
        }

        // 钩锁移动中处理
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

    // ══════════════════ 移动与跳跃 ══════════════════

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
        // 只要当前跳跃次数未达上限，即可执行跳跃（支持二段跳逻辑）
        if (Input.GetButtonDown("Jump") && currentJumps < maxJumps)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            currentJumps++;
        }
    }

    // ══════════════════ 下坠攻击 ══════════════════

    void HandleDiveInput()
    {
        // 优化1：在空中单击 S 键即可触发下坠，提升响应速度
        if (!isGrounded && Input.GetKeyDown(KeyCode.S) && canDive)
        {
            ActivateDive();
        }
    }

    void ActivateDive()
    {
        IsDiving = true;
        canDive = false;
        // 赋予向下的极高初速度
        rb.linearVelocity = new Vector2(rb.linearVelocity.x * 0.3f, -diveSpeed);
    }

    void PerformDiveAOE()
    {
        float dmg = playerStats != null ? playerStats.GetFinalDamage(diveDamageMultiplier) : 15f;
        Collider2D[] enemies = Physics2D.OverlapCircleAll(transform.position, diveAOERadius, enemyLayer);
        foreach (var col in enemies)
        {
            EnemyAI enemy = col.GetComponent<EnemyAI>();
            if (enemy != null) enemy.TakeDamage(dmg);
        }
        rb.linearVelocity = new Vector2(0, jumpForce * 0.5f); // 落地微量反弹，增加手感
        StartCoroutine(DiveCooldownCoroutine());
    }

    IEnumerator DiveCooldownCoroutine()
    {
        yield return new WaitForSeconds(diveCooldown);
        canDive = true;
    }

    // ────────────────── 虚空钩锁逻辑 ──────────────────

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
        
        // 优化3-B & 3-G：根据鼠标距离截断最大范围
        float dist = Mathf.Min(Vector2.Distance(origin, mouseWorld), grappleMaxDistance);
        
        // 优化3-A：虚空钩锁，不依赖Layer碰撞，直接设定目标位移点
        grappleTarget = origin + dir * dist;
        
        isGrappling = true;
        rb.gravityScale = 0; // 钩锁期间无重力

        if (grappleLineRenderer != null)
        {
            grappleLineRenderer.enabled = true;
            grappleLineRenderer.SetPosition(0, transform.position);
            grappleLineRenderer.SetPosition(1, grappleTarget);
        }
    }

    void HandleGrappleMovement()
    {
        // 优化3-E：玩家一旦按下任意移动方向键，立刻取消钩锁状态
        if (Input.GetAxisRaw("Horizontal") != 0 || Input.GetAxisRaw("Vertical") != 0)
        {
            EndGrapple();
            return;
        }

        Vector2 pos = transform.position;
        Vector2 direction = (grappleTarget - pos).normalized;
        rb.linearVelocity = direction * grappleSpeed;

        if (grappleLineRenderer != null)
            grappleLineRenderer.SetPosition(0, transform.position);

        // 优化3-C：到达虚拟目标点，执行结束逻辑
        if (Vector2.Distance(pos, grappleTarget) < grappleArriveThreshold)
        {
            CheckPhase2Transition(grappleTarget); // 为阶段二纸片转场预留的逻辑
            EndGrapple();
        }
    }

    // 优化3-C：在钩锁滑行过程中，如果碰到实体墙壁等障碍物，强制结束并掉落
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (isGrappling)
        {
            EndGrapple();
        }
    }

    void EndGrapple()
    {
        isGrappling = false;
        rb.gravityScale = defaultGravity;
        rb.linearVelocity = Vector2.zero; // 结束后立刻开始受重力下坠
        if (grappleLineRenderer != null) grappleLineRenderer.enabled = false;
    }

    // 优化4：阶段二转场预留占位
    void CheckPhase2Transition(Vector2 arrivePoint)
    {
        // TODO: 第二阶段纸片转场逻辑
        // 此处应检测到达点附近是否有特殊的墙壁标签，触发转场动画
    }

    // ────────────────── 冲刺与攻击 ──────────────────

    void HandleDash()
    {
        if (Input.GetKeyDown(KeyCode.LeftShift) && canDash) StartCoroutine(DashCoroutine());
    }

    IEnumerator DashCoroutine()
    {
        isDashing = true;
        canDash = false;
        float originalGrav = rb.gravityScale;
        rb.gravityScale = 0;
        float dir = isFacingRight ? 1f : -1f;
        rb.linearVelocity = new Vector2(dir * dashSpeed, 0);

        yield return new WaitForSeconds(dashDuration);

        rb.gravityScale = originalGrav;
        rb.linearVelocity = Vector2.zero;
        isDashing = false;
        yield return new WaitForSeconds(dashCooldown);
        canDash = true;
    }

    public void InstantResetDash()
    {
        canDash = true;
        StopCoroutine(nameof(DashCoroutine));
        isDashing = false;
        rb.gravityScale = defaultGravity;
    }

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
        float dmg = playerStats != null ? playerStats.GetFinalDamage(1f) : 10f;
        Collider2D[] enemies = Physics2D.OverlapCircleAll(attackPoint != null ? attackPoint.position : transform.position, attackRange, enemyLayer);
        foreach (var col in enemies)
        {
            EnemyAI enemy = col.GetComponent<EnemyAI>();
            if (enemy != null) enemy.TakeDamage(dmg);
        }
    }

    // ────────────────── 工具方法 ──────────────────

    void Flip()
    {
        isFacingRight = !isFacingRight;
        Vector3 scaler = transform.localScale;
        scaler.x *= -1;
        transform.localScale = scaler;
    }
}