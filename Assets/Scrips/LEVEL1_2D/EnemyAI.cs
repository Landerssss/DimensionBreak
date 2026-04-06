using UnityEngine;
using System.Collections;

/// <summary>
/// 阶段一通用敌人 AI：巡逻 → 索敌 → 追击 → 攻击（暂停） → 死亡给经验。
/// 所有数值全部 [SerializeField] 暴露到面板。
/// 使用 Collider2D.bounds.center 替代 transform.position 来避免精灵偏移导致的抖动。
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

    // ────────────────── 攻击 ──────────────────
    [Header("=== 攻击 ===")]
    [Tooltip("攻击后暂停移动的时间（秒）")]
    [SerializeField] private float attackPauseDuration = 1f;
    [Tooltip("进入攻击距离")]
    [SerializeField] private float attackRange = 1.2f;
    [Tooltip("攻击伤害")]
    [SerializeField] private float attackDamage = 10f;

    // ────────────────── 经验 ──────────────────
    [Header("=== 掉落经验 ===")]
    [SerializeField] private float expReward = 800f;

    // ────────────────── 受击反馈 ──────────────────
    [Header("=== 受击反馈 ===")]
    [SerializeField] private float hitFlashDuration = 0.1f;

    // ────────────────── 死亡 ──────────────────
    [Header("=== 死亡 ===")]
    [Tooltip("死亡动画播放后等待多久再销毁物体")]
    [SerializeField] private float deathDestroyDelay = 1.5f;

    // ────────────────── 精灵朝向 ──────────────────
    [Header("=== 精灵朝向 ===")]
    [Tooltip("勾选此项表示素材默认朝向为左（即 dir>0 时需要 flipX=true）")]
    [SerializeField] private bool defaultFacingLeft = false;

    // ────────────────── 碰撞层 ──────────────────
    [Header("=== 环境碰撞 ===")]
    [Tooltip("墙壁/障碍物所在的图层，用于巡逻时碰壁转向")]
    [SerializeField] private LayerMask environmentLayer;

    // ────────────────── 内部状态 ──────────────────
    private enum State { Patrol, Wait, Chase, Attack, Dead }
    private State state = State.Patrol;

    private Vector2 startPos;
    private float patrolLeftX;
    private float patrolRightX;
    private int patrolDir = 1; // 1=右, -1=左
    private float waitTimer;
    private float attackPauseTimer; // 攻击暂停计时器

    private Transform playerTarget;
    private SpriteRenderer spriteRenderer;
    private Rigidbody2D rb;
    private Collider2D col; // 主碰撞体，用于 bounds.center
    private Animator animator;

    /// <summary>
    /// 获取碰撞体中心位置，避免因精灵锚点偏移导致的抖动。
    /// 如果碰撞体不存在则回退到 transform.position。
    /// </summary>
    private Vector2 Center => col != null ? (Vector2)col.bounds.center : (Vector2)transform.position;

    // ══════════════════ 生命周期 ══════════════════

    void Start()
    {
        currentHP = maxHP;

        spriteRenderer = GetComponent<SpriteRenderer>();
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        animator = GetComponent<Animator>();

        // 用碰撞体中心作为起始位置，避免偏移
        startPos = Center;
        patrolLeftX = startPos.x - patrolLeftOffset;
        patrolRightX = startPos.x + patrolRightOffset;

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

            case State.Attack:
                HandleAttackPause();
                break;

            case State.Dead:
                // 死亡状态下不做任何事，等待销毁
                break;
        }
    }

    // ══════════════════ 巡逻 ══════════════════

    void Patrol()
    {
        float centerX = Center.x; // 使用碰撞体中心
        float targetX = patrolDir > 0 ? patrolRightX : patrolLeftX;
        float step = patrolSpeed * Time.deltaTime;

        Vector2 pos = transform.position;
        pos.x = Mathf.MoveTowards(pos.x, targetX + (pos.x - centerX), step);
        // 注释：加上 (pos.x - centerX) 偏移量，使 transform 移动时碰撞体中心到达目标位置
        transform.position = pos;

        FaceDirection(patrolDir);

        // 使用碰撞体中心判断是否到达端点
        if (Mathf.Abs(centerX - targetX) < 0.05f)
        {
            patrolDir *= -1;
            state = State.Wait;
            waitTimer = patrolWaitTime;
        }
    }

    // ══════════════════ 索敌 ══════════════════

    void TryDetectPlayer()
    {
        // 使用碰撞体中心作为检测原点
        Collider2D hit = Physics2D.OverlapCircle(Center, detectRadius, playerLayer);
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

        Vector2 center = Center;
        float dist = Vector2.Distance(center, playerTarget.position);

        // 脱离追击
        if (dist > loseChasingDistance)
        {
            playerTarget = null;
            state = State.Patrol;
            return;
        }

        // 进入攻击范围 → 发起攻击
        if (dist <= attackRange)
        {
            StartAttack();
            return;
        }

        // 只在水平方向追击，不跳跃
        float dir = playerTarget.position.x > center.x ? 1f : -1f;
        float step = chaseSpeed * Time.deltaTime;

        Vector2 pos = transform.position;
        pos.x += dir * step;
        transform.position = pos;

        FaceDirection(dir > 0 ? 1 : -1);
    }

    // ══════════════════ 攻击 ══════════════════

    /// <summary>
    /// 发起攻击：触发动画、造成伤害，然后进入暂停状态。
    /// </summary>
    void StartAttack()
    {
        state = State.Attack;
        attackPauseTimer = attackPauseDuration;

        // 触发攻击动画（假设 Animator 有 "Attack" 触发器）
        if (animator != null)
            animator.SetTrigger("Attack");

        // 对玩家造成伤害（如果玩家在范围内）
        if (playerTarget != null)
        {
            // 尝试获取玩家的生命组件并造成伤害
            PlayerStats playerStats = playerTarget.GetComponent<PlayerStats>();
            if (playerStats != null)
            {
                playerStats.TakeDamage(attackDamage);
            }
        }

        Debug.Log($"{gameObject.name} 发起攻击！暂停 {attackPauseDuration} 秒");
    }

    /// <summary>
    /// 攻击暂停期间：敌人不移动也不检测玩家。
    /// 暂停结束后根据距离决定返回 Patrol 或 Chase。
    /// </summary>
    void HandleAttackPause()
    {
        attackPauseTimer -= Time.deltaTime;

        if (attackPauseTimer <= 0f)
        {
            // 暂停结束，重新评估状态
            if (playerTarget != null)
            {
                float dist = Vector2.Distance(Center, playerTarget.position);
                if (dist <= detectRadius)
                {
                    state = State.Chase;
                }
                else
                {
                    playerTarget = null;
                    state = State.Patrol;
                }
            }
            else
            {
                state = State.Patrol;
            }
        }
        // 暂停期间什么都不做（不移动、不检测玩家）
    }

    // ══════════════════ 墙壁碰撞转向 ══════════════════

    /// <summary>
    /// 当巡逻时碰到墙壁/障碍物，立即转向，避免卡住抖动。
    /// </summary>
    void OnCollisionEnter2D(Collision2D collision)
    {
        // 死亡状态不处理碰撞
        if (state == State.Dead) return;

        // 检查碰撞物是否在环境层
        if (((1 << collision.gameObject.layer) & environmentLayer) != 0)
        {
            // 判断碰撞方向：如果碰撞法线的水平分量与巡逻方向相反，说明撞墙了
            foreach (ContactPoint2D contact in collision.contacts)
            {
                // 法线指向远离墙面的方向；如果法线 x 与巡逻方向符号相反，则碰到前方的墙
                if ((patrolDir > 0 && contact.normal.x < -0.5f) ||
                    (patrolDir < 0 && contact.normal.x > 0.5f))
                {
                    patrolDir *= -1;
                    FaceDirection(patrolDir);

                    // 如果在巡逻/等待状态中碰壁，直接进入巡逻状态
                    if (state == State.Patrol || state == State.Wait)
                    {
                        state = State.Patrol;
                    }
                    break;
                }
            }
        }
    }

    // ══════════════════ 受击与死亡 ══════════════════

    /// <summary>
    /// 受到伤害
    /// </summary>
    public void TakeDamage(float damage)
    {
        // 已经死亡则忽略
        if (state == State.Dead) return;

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
        // 防止重复触发
        if (state == State.Dead) return;
        state = State.Dead;

        Debug.Log($"{gameObject.name} 被击杀！掉落经验 {expReward}");

        // ① 立即禁用碰撞体和刚体，防止死后继续物理交互
        if (col != null) col.enabled = false;
        // 如果有多个碰撞体，全部禁用
        Collider2D[] allCols = GetComponents<Collider2D>();
        foreach (var c in allCols)
            c.enabled = false;

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.bodyType = RigidbodyType2D.Kinematic;
        }

        // ② 触发死亡动画
        if (animator != null)
            animator.SetTrigger("Die");

        // ③ 通知玩家角色的 PlayerStats
        PlayerStats stats = FindAnyObjectByType<PlayerStats>();
        if (stats != null)
        {
            stats.OnEnemyKilled(expReward);
        }

        // ④ 如果被突刺击杀，重置玩家冲刺 CD
        PlayerController pc = FindAnyObjectByType<PlayerController>();
        if (pc != null && pc.IsDashing)
        {
            pc.InstantResetDash();
        }

        // ⑤ 延迟销毁，等待死亡动画播放完毕
        StartCoroutine(DestroyAfterDelay(deathDestroyDelay));
    }

    /// <summary>
    /// 延迟销毁协程，让死亡动画有时间播放。
    /// </summary>
    private IEnumerator DestroyAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        Destroy(gameObject);
    }

    // ══════════════════ 工具方法 ══════════════════

    void FaceDirection(int dir)
    {
        if (spriteRenderer == null) return;

        // 若素材默认朝左，朝右时需要翻转；反之亦然
        if (defaultFacingLeft)
            spriteRenderer.flipX = dir > 0;  // 素材朝左：向右走时才 flip
        else
            spriteRenderer.flipX = dir < 0;  // 素材朝右（默认）：向左走时才 flip
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

        // 攻击范围
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(center, attackRange);
    }
}
