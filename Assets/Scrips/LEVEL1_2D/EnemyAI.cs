using UnityEngine;
using System.Collections;

/// <summary>
/// 阶段一通用敌人 AI：巡逻 → 索敌 → 追击 → 攻击停顿 → 死亡动画。
/// 彻底修复素材偏移导致的频闪，增加物理防卡墙检测。
/// </summary>
public class EnemyAI : MonoBehaviour
{
    // ────────────────── 生命值 ──────────────────
    [Header("=== 生命值 ===")]
    [SerializeField] private float maxHP = 100f;
    private float currentHP;

    // ────────────────── 巡逻 ──────────────────
    [Header("=== 巡逻 ===")]
    [SerializeField] private float patrolSpeed = 2f;
    [SerializeField] private float patrolLeftOffset = 3f;
    [SerializeField] private float patrolRightOffset = 3f;
    [SerializeField] private float patrolWaitTime = 0.5f;

    // ────────────────── 索敌与追击 ──────────────────
    [Header("=== 索敌与追击 ===")]
    [SerializeField] private float detectRadius = 5f;
    [SerializeField] private float chaseSpeed = 4f;
    [SerializeField] private LayerMask playerLayer;
    [Tooltip("脱离追击的距离（大于索敌半径）")]
    [SerializeField] private float loseChasingDistance = 8f;

    // ────────────────── 攻击与墙壁碰撞 ──────────────────
    [Header("=== 交互与攻击 ===")]
    [Tooltip("墙壁/障碍物图层，碰到会立刻回头")]
    [SerializeField] private LayerMask obstacleLayer;
    [Tooltip("触发攻击的距离")]
    [SerializeField] private float attackRange = 1.2f;
    [Tooltip("攻击后的停顿时间（秒），期间忽略玩家")]
    [SerializeField] private float attackPauseTime = 1.0f;

    // ────────────────── 经验与反馈 ──────────────────
    [Header("=== 经验与死亡 ===")]
    [SerializeField] private float expReward = 800f;
    [SerializeField] private float hitFlashDuration = 0.1f;
    [Tooltip("死亡动画播放需要的时间，之后销毁")]
    [SerializeField] private float deathAnimationTime = 1.2f;

    // ────────────────── 内部状态 ──────────────────
    private enum State { Patrol, Wait, Chase, Attack, Dead }
    private State state = State.Patrol;

    private Vector2 startPos;
    private float patrolLeftX;
    private float patrolRightX;
    private int patrolDir = 1; // 1=右, -1=左
    private float waitTimer;

    private Transform playerTarget;
    private SpriteRenderer spriteRenderer;
    private Rigidbody2D rb;
    private Collider2D col;
    private Animator anim;

    // ══════════════════ 生命周期 ══════════════════

    void Start()
    {
        currentHP = maxHP;
        startPos = transform.position;
        patrolLeftX = startPos.x - patrolLeftOffset;
        patrolRightX = startPos.x + patrolRightOffset;

        spriteRenderer = GetComponent<SpriteRenderer>();
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        anim = GetComponent<Animator>();

        patrolDir = 1;
    }

    void Update()
    {
        if (state == State.Dead) return;

        switch (state)
        {
            case State.Patrol:
                Patrol();
                TryDetectPlayer();
                break;
            case State.Wait:
                // 等待期间停止移动
                rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
                waitTimer -= Time.deltaTime;
                if (waitTimer <= 0) state = State.Patrol;
                TryDetectPlayer();
                break;
            case State.Chase:
                ChasePlayer();
                break;
            case State.Attack:
                // 攻击停顿期间，交由 Coroutine 处理，这里什么都不做
                break;
        }
    }

    void FixedUpdate()
    {
        // 防卡墙射线检测
        if (state == State.Dead || state == State.Attack || state == State.Wait) return;

        // 从碰撞体中心向前发射极短的射线检测墙壁
        Vector2 center = col != null ? (Vector2)col.bounds.center : (Vector2)transform.position;
        RaycastHit2D hit = Physics2D.Raycast(center, Vector2.right * patrolDir, 0.6f, obstacleLayer);
        
        if (hit.collider != null)
        {
            // 撞到障碍物，强制回头并等待
            if (state == State.Patrol)
            {
                patrolDir *= -1;
                state = State.Wait;
                waitTimer = patrolWaitTime;
            }
            // 如果在追击时撞墙，也可以选择停顿一下，防止一直抽搐
            else if (state == State.Chase)
            {
                rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
            }
        }
    }

    // ══════════════════ 巡逻 ══════════════════

    void Patrol()
    {
        float targetX = patrolDir > 0 ? patrolRightX : patrolLeftX;
        
        // 改为物理速度驱动，解决 Pivot 不在中心导致的频闪
        rb.linearVelocity = new Vector2(patrolDir * patrolSpeed, rb.linearVelocity.y);
        FaceDirection(patrolDir);

        // 使用物理中心点判断是否到达边缘
        float currentX = col != null ? col.bounds.center.x : transform.position.x;
        if ((patrolDir > 0 && currentX >= targetX) || (patrolDir < 0 && currentX <= targetX))
        {
            patrolDir *= -1;
            state = State.Wait;
            waitTimer = patrolWaitTime;
        }
    }

    // ══════════════════ 索敌与追击 ══════════════════

    void TryDetectPlayer()
    {
        Vector2 center = col != null ? (Vector2)col.bounds.center : (Vector2)transform.position;
        Collider2D hit = Physics2D.OverlapCircle(center, detectRadius, playerLayer);
        
        if (hit != null)
        {
            playerTarget = hit.transform;
            state = State.Chase;
        }
    }

    void ChasePlayer()
    {
        if (playerTarget == null)
        {
            state = State.Patrol;
            return;
        }

        Vector2 center = col != null ? (Vector2)col.bounds.center : (Vector2)transform.position;
        Vector2 targetPos = playerTarget.position; 
        
        float dist = Vector2.Distance(center, targetPos);

        // 丢失目标
        if (dist > loseChasingDistance)
        {
            playerTarget = null;
            state = State.Patrol;
            return;
        }

        // 进入攻击范围
        if (dist <= attackRange)
        {
            StartCoroutine(AttackRoutine());
            return;
        }

        // 追击移动
        float dir = targetPos.x > center.x ? 1f : -1f;
        rb.linearVelocity = new Vector2(dir * chaseSpeed, rb.linearVelocity.y);
        FaceDirection(dir > 0 ? 1 : -1);
    }

    // ══════════════════ 攻击与停顿 ══════════════════

    IEnumerator AttackRoutine()
    {
        state = State.Attack;
        rb.linearVelocity = new Vector2(0, rb.linearVelocity.y); // 强制停步

        // 触发攻击动画（请在 Animator 中设置 "Attack" Trigger）
        if (anim != null) anim.SetTrigger("Attack");
        
        // 停顿，忽略玩家
        yield return new WaitForSeconds(attackPauseTime);

        // 停顿结束后重新进入等待或巡逻，让逻辑自动重新索敌
        state = State.Wait;
        waitTimer = 0.1f; 
    }

    // ══════════════════ 受击与死亡 ══════════════════

    public void TakeDamage(float damage)
    {
        if (state == State.Dead) return;

        currentHP -= damage;
        
        if (spriteRenderer != null)
        {
            spriteRenderer.color = Color.red;
            Invoke(nameof(ResetColor), hitFlashDuration);
        }

        if (currentHP <= 0f)
        {
            Die();
        }
    }

    void ResetColor()
    {
        if (spriteRenderer != null && state != State.Dead)
            spriteRenderer.color = Color.white;
    }

    void Die()
    {
        if (state == State.Dead) return;
        state = State.Dead;

        // 结算经验
        PlayerStats stats = FindAnyObjectByType<PlayerStats>();
        if (stats != null) stats.OnEnemyKilled(expReward);

        // 刷新玩家冲刺
        PlayerController pc = FindAnyObjectByType<PlayerController>();
        if (pc != null && pc.IsDashing) pc.InstantResetDash();

        // 1. 播放死亡动画
        if (anim != null) anim.SetTrigger("Die");

        // 2. 彻底关闭物理，防止诈尸移动或挡住玩家
        rb.linearVelocity = Vector2.zero;
        rb.simulated = false; 
        if (col != null) col.enabled = false;

        // 3. 等待动画播放完毕后销毁
        Destroy(gameObject, deathAnimationTime);
    }

    // ══════════════════ 工具方法 ══════════════════

    void FaceDirection(int dir)
    {
        if (spriteRenderer != null)
            spriteRenderer.flipX = dir < 0;
    }

    void OnDrawGizmosSelected()
    {
        Vector3 center = Application.isPlaying && col != null ? col.bounds.center : transform.position;
        
        Gizmos.color = Color.yellow;
        float left = Application.isPlaying ? patrolLeftX : center.x - patrolLeftOffset;
        float right = Application.isPlaying ? patrolRightX : center.x + patrolRightOffset;
        Gizmos.DrawLine(new Vector3(left, center.y, 0), new Vector3(right, center.y, 0));
        
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(center, detectRadius);

        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(center, attackRange);
    }
}