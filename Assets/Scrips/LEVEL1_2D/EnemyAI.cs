using UnityEngine;

/// <summary>
/// 阶段一通用敌人 AI：巡逻 → 索敌 → 追击（不跳跃）→ 死亡给经验。
/// 所有数值全部 [SerializeField] 暴露到面板。
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
    [Tooltip("相对于初始位置的左侧巡逻距离")]
    [SerializeField] private float patrolLeftOffset = 3f;
    [Tooltip("相对于初始位置的右侧巡逻距离")]
    [SerializeField] private float patrolRightOffset = 3f;
    [Tooltip("到达端点后等待的时间")]
    [SerializeField] private float patrolWaitTime = 0.5f;

    // ────────────────── 索敌与追击 ──────────────────
    [Header("=== 索敌与追击 ===")]
    [SerializeField] private float detectRadius = 5f;
    [SerializeField] private float chaseSpeed = 4f;
    [SerializeField] private LayerMask playerLayer;
    [Tooltip("脱离追击的距离（大于索敌半径）")]
    [SerializeField] private float loseChasingDistance = 8f;

    // ────────────────── 经验 ──────────────────
    [Header("=== 掉落经验 ===")]
    [SerializeField] private float expReward = 800f;

    // ────────────────── 受击反馈 ──────────────────
    [Header("=== 受击反馈 ===")]
    [SerializeField] private float hitFlashDuration = 0.1f;

    // ────────────────── 内部状态 ──────────────────
    private enum State { Patrol, Wait, Chase }
    private State state = State.Patrol;

    private Vector2 startPos;
    private float patrolLeftX;
    private float patrolRightX;
    private int patrolDir = 1; // 1=右, -1=左
    private float waitTimer;

    private Transform playerTarget;
    private SpriteRenderer spriteRenderer;
    private Rigidbody2D rb;

    // ══════════════════ 生命周期 ══════════════════

    void Start()
    {
        currentHP = maxHP;
        startPos = transform.position;
        patrolLeftX = startPos.x - patrolLeftOffset;
        patrolRightX = startPos.x + patrolRightOffset;

        spriteRenderer = GetComponent<SpriteRenderer>();
        rb = GetComponent<Rigidbody2D>();

        // 默认朝右巡逻
        patrolDir = 1;
    }

    void Update()
    {
        switch (state)
        {
            case State.Patrol:
                Patrol();
                TryDetectPlayer();
                break;
            case State.Wait:
                waitTimer -= Time.deltaTime;
                if (waitTimer <= 0) state = State.Patrol;
                TryDetectPlayer();
                break;
            case State.Chase:
                ChasePlayer();
                break;
        }
    }

    // ══════════════════ 巡逻 ══════════════════

    void Patrol()
    {
        float targetX = patrolDir > 0 ? patrolRightX : patrolLeftX;
        float step = patrolSpeed * Time.deltaTime;

        Vector2 pos = transform.position;
        pos.x = Mathf.MoveTowards(pos.x, targetX, step);
        transform.position = pos;

        FaceDirection(patrolDir);

        if (Mathf.Abs(pos.x - targetX) < 0.05f)
        {
            patrolDir *= -1;
            state = State.Wait;
            waitTimer = patrolWaitTime;
        }
    }

    // ══════════════════ 索敌 ══════════════════

    void TryDetectPlayer()
    {
        Collider2D hit = Physics2D.OverlapCircle(transform.position, detectRadius, playerLayer);
        if (hit != null)
        {
            playerTarget = hit.transform;
            state = State.Chase;
        }
    }

    // ══════════════════ 追击 ══════════════════

    void ChasePlayer()
    {
        if (playerTarget == null)
        {
            state = State.Patrol;
            return;
        }

        float dist = Vector2.Distance(transform.position, playerTarget.position);
        if (dist > loseChasingDistance)
        {
            playerTarget = null;
            state = State.Patrol;
            return;
        }

        // 只在水平方向追击，不跳跃
        float dir = playerTarget.position.x > transform.position.x ? 1f : -1f;
        float step = chaseSpeed * Time.deltaTime;

        Vector2 pos = transform.position;
        pos.x += dir * step;
        transform.position = pos;

        FaceDirection(dir > 0 ? 1 : -1);
    }

    // ══════════════════ 受击与死亡 ══════════════════

    /// <summary>
    /// 受到伤害
    /// </summary>
    public void TakeDamage(float damage)
    {
        currentHP -= damage;
        Debug.Log($"{gameObject.name} 受到 {damage:F0} 伤害，剩余 HP: {currentHP:F0}");

        // 闪烁反馈
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
        if (spriteRenderer != null)
            spriteRenderer.color = Color.white;
    }

    void Die()
    {
        Debug.Log($"{gameObject.name} 被击杀！掉落经验 {expReward}");

        // 通知玩家角色的 PlayerStats
        PlayerStats stats = FindAnyObjectByType<PlayerStats>();
        if (stats != null)
        {
            stats.OnEnemyKilled(expReward);
        }

        // 如果被突刺击杀，重置玩家冲刺 CD
        PlayerController pc = FindAnyObjectByType<PlayerController>();
        if (pc != null && pc.IsDashing)
        {
            pc.InstantResetDash();
        }

        // TODO: 死亡特效
        Destroy(gameObject);
    }

    // ══════════════════ 工具方法 ══════════════════

    void FaceDirection(int dir)
    {
        if (spriteRenderer != null)
            spriteRenderer.flipX = dir < 0;
    }

    void OnDrawGizmosSelected()
    {
        // 巡逻范围
        Gizmos.color = Color.yellow;
        Vector3 center = Application.isPlaying ? (Vector3)startPos : transform.position;
        float left = Application.isPlaying ? patrolLeftX : center.x - patrolLeftOffset;
        float right = Application.isPlaying ? patrolRightX : center.x + patrolRightOffset;
        Gizmos.DrawLine(new Vector3(left, center.y, 0), new Vector3(right, center.y, 0));

        // 索敌范围
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(center, detectRadius);
    }
}
