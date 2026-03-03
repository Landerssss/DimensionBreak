using UnityEngine;
using System.Collections;

public class PlayerController : MonoBehaviour
{
    [Header("=== 基础移动 ===")]
    [SerializeField] private float moveSpeed = 8f;
    [SerializeField] private float jumpForce = 16f;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckRadius = 0.2f;

    [Header("=== 二段跳解锁 ===")]
    [Tooltip("达到此等级后解锁二段跳")]
    [SerializeField] private int doubleJumpUnlockLevel = 3;
    private int maxJumps = 1;
    private int currentJumps = 0;

    [Header("=== 冲刺 ===")]
    [SerializeField] private float dashSpeed = 25f;
    [SerializeField] private float dashDuration = 0.2f;
    [SerializeField] private float dashCooldown = 1f;
    [SerializeField] private float dashAttackWidth = 1f;
    [SerializeField] private LayerMask enemyLayer;

    [Header("=== 近战攻击 ===")]
    [SerializeField] private float attackRange = 1.5f;
    [SerializeField] private Transform attackPoint;
    [SerializeField] private float attackCooldown = 0.35f;

    [Header("=== 虚空钩锁 ===")]
    [SerializeField] private float grappleSpeed = 30f;
    [SerializeField] private float grappleMaxDistance = 10f;
    [SerializeField] private float grappleArriveThreshold = 0.5f;
    [SerializeField] private LineRenderer grappleLineRenderer;

    [Header("=== 下坠攻击 ===")]
    [SerializeField] private float diveSpeed = 45f;
    [SerializeField] private float diveAOERadius = 2f;
    [SerializeField] private float diveDamageMultiplier = 1.5f;
    [SerializeField] private float diveCooldown = 1.5f;

    private Rigidbody2D rb;
    private bool isGrounded;
    private bool isFacingRight = true;
    private float defaultGravity;

    private bool isDashing;
    private bool canDash = true;
    public bool IsDashing => isDashing;

    public bool IsDiving { get; private set; }
    private bool canDive = true;
    private float lastAttackTime = -999f;

    private bool isGrappling;
    private Vector2 grappleTarget;

    private PlayerStats playerStats;

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
        if (GameManager.Instance != null && GameManager.Instance.isTransitioning)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
        
        // 动态更新最大跳跃次数
        maxJumps = (playerStats != null && playerStats.CurrentLevel >= doubleJumpUnlockLevel) ? 2 : 1;
        
        if (isGrounded && !IsDiving)
        {
            currentJumps = 0; // 落地重置跳跃次数
        }

        if (isGrounded && IsDiving)
        {
            IsDiving = false;
            PerformDiveAOE();
        }

        if (isGrappling)
        {
            HandleGrappleMovement();
            return;
        }

        if (isDashing) return;

        HandleMovement();
        HandleJump();
        HandleDash();
        HandleAttack();
        HandleGrappleInput();
        HandleDiveInput();
    }

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
        if (Input.GetButtonDown("Jump") && currentJumps < maxJumps)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            currentJumps++;
        }
    }

    void HandleDiveInput()
    {
        // 优化1：单机S即可下坠，无需双击
        if (!isGrounded && Input.GetKeyDown(KeyCode.S) && canDive)
        {
            ActivateDive();
        }
    }

    void ActivateDive()
    {
        IsDiving = true;
        canDive = false;
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
        rb.linearVelocity = new Vector2(0, jumpForce * 0.5f); // 落地微弹
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
        
        // 优化3-B & 3-G：极限距离判断
        float dist = Mathf.Min(Vector2.Distance(origin, mouseWorld), grappleMaxDistance);
        
        // 优化3-A：不依赖Layer，直接算目标点
        grappleTarget = origin + dir * dist;
        
        isGrappling = true;
        rb.gravityScale = 0;

        if (grappleLineRenderer != null)
        {
            grappleLineRenderer.enabled = true;
            grappleLineRenderer.SetPosition(0, transform.position);
            grappleLineRenderer.SetPosition(1, grappleTarget);
        }
    }

    void HandleGrappleMovement()
    {
        // 优化3-E：玩家按WASD立刻取消
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

        // 优化3-C：到达目标点立刻下坠
        if (Vector2.Distance(pos, grappleTarget) < grappleArriveThreshold)
        {
            CheckPhase2Transition(grappleTarget); // 阶段二预留
            EndGrapple();
        }
    }

    // 优化3-C：碰到任何带碰撞体的障碍物立刻掉落
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
        rb.linearVelocity = Vector2.zero; // 立刻下坠
        if (grappleLineRenderer != null) grappleLineRenderer.enabled = false;
    }

    // 优化4：阶段二转场预留占位
    void CheckPhase2Transition(Vector2 arrivePoint)
    {
        // TODO: 第二阶段纸片转场
        // 检测 arrivePoint 附近是否有 "Phase2Trigger" 标签的墙壁机关
        // 如果有，则通知 GameManager 切换状态并播放纸片特效
    }

    // ────────────────── 冲刺与攻击 (保持不变) ──────────────────
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

    void Flip()
    {
        isFacingRight = !isFacingRight;
        Vector3 scaler = transform.localScale;
        scaler.x *= -1;
        transform.localScale = scaler;
    }
}