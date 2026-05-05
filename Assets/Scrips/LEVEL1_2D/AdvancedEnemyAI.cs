using UnityEngine;
using System.Collections;

/// <summary>
/// AdvancedEnemyAI.cs - EnemyAI 高级变体
/// 适配拥有 Idle / Run / Attack / Hurt / Die / Cast / Spell 动画的怪物资源。
/// 
/// 与原 EnemyAI.cs 完全独立，不修改原脚本任何内容。
/// 
/// 新增能力：
///   - Cast：高伤害重击（可配置伤害倍率、攻击距离、冷却时间）
///   - Spell：短距离传送（可配置距离、冷却时间）
///   - Hurt：受击播放受伤动画（替代原来的全身闪红，改为手动动画）
///   - Run：追击时使用跑步动画（替代原 IsWalking）
/// </summary>
public class AdvancedEnemyAI : MonoBehaviour
{
    // ────────────────── 生命值 ──────────────────
    [Header("=== 生命值 ===")]
    [SerializeField] private float maxHP = 200f;
    private float currentHP;

    // ────────────────── 巡逻 ──────────────────
    [Header("=== 巡逻 ===")]
    [SerializeField] private float patrolSpeed = 2f;
    [Tooltip("相对于初始位置的左侧巡逻距离")]
    [SerializeField] private float patrolLeftOffset = 4f;
    [Tooltip("相对于初始位置的右侧巡逻距离")]
    [SerializeField] private float patrolRightOffset = 4f;
    [Tooltip("到达端点后等待的时间")]
    [SerializeField] private float patrolWaitTime = 0.5f;

    // ────────────────── 索敌与追击 ──────────────────
    [Header("=== 索敌与追击 ===")]
    [SerializeField] private float detectRadius = 6f;
    [SerializeField] private float chaseSpeed = 4.5f;
    [SerializeField] private LayerMask playerLayer;
    [Tooltip("脱离追击的距离（大于索敌半径）")]
    [SerializeField] private float loseChasingDistance = 10f;

    // ────────────────── 普通攻击 ──────────────────
    [Header("=== 普通攻击（Attack） ===")]
    [SerializeField] private float attackRange = 1.5f;
    [SerializeField] private float attackDamage = 15f;
    [Tooltip("普通攻击动画播放时长（攻击锁定时间）")]
    [SerializeField] private float attackDuration = 0.8f;
    [Tooltip("普通攻击冷却时间")]
    [SerializeField] private float attackCooldown = 1.5f;
    [Tooltip("普通攻击伤害延迟（动画中挥刀命中帧的大致时间）")]
    [SerializeField] private float attackDamageDelay = 0.35f;

    // ────────────────── 重击 Cast ──────────────────
    [Header("=== 重击（Cast） ===")]
    [Tooltip("Cast 伤害倍率（相对于普通攻击）")]
    [SerializeField] private float castDamageMultiplier = 2.5f;
    [Tooltip("Cast 施法距离（需在此范围内才会释放）")]
    [SerializeField] private float castRange = 2.0f;
    [Tooltip("Cast 动画播放时长")]
    [SerializeField] private float castDuration = 1.2f;
    [Tooltip("Cast 冷却时间")]
    [SerializeField] private float castCooldown = 6f;
    [Tooltip("Cast 伤害延迟")]
    [SerializeField] private float castDamageDelay = 0.5f;
    [Tooltip("Cast 伤害判定半径")]
    [SerializeField] private float castDamageRadius = 2.0f;

    // ────────────────── 传送 Spell ──────────────────
    [Header("=== 传送（Spell） ===")]
    [Tooltip("传送距离（正值表示朝面朝方向传送）")]
    [SerializeField] private float spellTeleportDistance = 3f;
    [Tooltip("Spell 动画播放时长")]
    [SerializeField] private float spellDuration = 0.8f;
    [Tooltip("传送冷却时间")]
    [SerializeField] private float spellCooldown = 8f;
    [Tooltip("传送时实际位移发生的延迟（动画中消失帧的时间）")]
    [SerializeField] private float spellTeleportDelay = 0.3f;
    [Tooltip("HP 百分比低于此值时才会使用传送（0-1）")]
    [SerializeField] private float spellHPThreshold = 0.5f;

    // ────────────────── 经验 ──────────────────
    [Header("=== 掉落经验 ===")]
    [SerializeField] private float expReward = 1500f;

    // ────────────────── 受击反馈 ──────────────────
    [Header("=== 受击反馈 ===")]
    [Tooltip("受击动画播放时长（受击硬直）")]
    [SerializeField] private float hurtDuration = 0.4f;
    [Tooltip("受击时手部闪红的持续时间")]
    [SerializeField] private float hitFlashDuration = 0.15f;

    // ────────────────── 死亡 ──────────────────
    [Header("=== 死亡 ===")]
    [Tooltip("死亡动画播放后等待多久再销毁物体")]
    [SerializeField] private float deathDestroyDelay = 2.0f;

    // ────────────────── 掉落物品 ──────────────────
    [Header("=== 掉落物品 ===")]
    [Tooltip("血瓶预制体")]
    [SerializeField] private GameObject healthPotionPrefab;
    [Tooltip("掉落血瓶的概率 (0.0 到 1.0)")]
    [SerializeField] private float dropPotionChance = 0.3f;

    // ────────────────── 精灵朝向 ──────────────────
    [Header("=== 精灵朝向 ===")]
    [Tooltip("勾选此项表示素材默认朝向为左（即 dir>0 时需要翻转）")]
    [SerializeField] private bool defaultFacingLeft = false;

    // ────────────────── 碰撞层 ──────────────────
    [Header("=== 环境碰撞 ===")]
    [Tooltip("墙壁/障碍物所在的图层，用于巡逻时碰壁转向")]
    [SerializeField] private LayerMask environmentLayer;

    // ────────────────── 受击闪红部位 ──────────────────
    [Header("=== 受击闪红部位 ===")]
    [Tooltip("拖入需要闪红的 SpriteRenderer（例如手臂/武器部件），不填则不闪红")]
    [SerializeField] private SpriteRenderer[] flashRenderers;

    // ────────────────── 内部状态 ──────────────────
    private enum State { Patrol, Wait, Chase, Action, Hurt, Dead }
    private State state = State.Patrol;

    private Vector2 startPos;
    private float patrolLeftX;
    private float patrolRightX;
    private int patrolDir = 1; // 1=右, -1=左
    private float waitTimer;

    // 冷却计时器
    private float attackCDTimer;
    private float castCDTimer;
    private float spellCDTimer;

    private Transform playerTarget;
    private SpriteRenderer spriteRenderer;
    private SpriteRenderer[] allSpriteRenderers;
    private Rigidbody2D rb;
    private Collider2D col;

    [Header("=== 动画与视觉 ===")]
    [Tooltip("如果不指定，将自动在物体或子物体中查找")]
    public Animator animator;

    /// <summary>
    /// 动作互斥锁：任何攻击/施法/受伤动画期间为 true，防止状态被打断。
    /// </summary>
    private bool isDoingAction = false;

    /// <summary>
    /// 获取碰撞体中心位置，避免因精灵锚点偏移导致的抖动。
    /// </summary>
    private Vector2 Center => col != null ? (Vector2)col.bounds.center : (Vector2)transform.position;

    // ══════════════════ 生命周期 ══════════════════

    void Start()
    {
        currentHP = maxHP;

        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        allSpriteRenderers = GetComponentsInChildren<SpriteRenderer>();
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        // 如果没有手动指定闪红部位，回退到空数组（不闪红）
        if (flashRenderers == null || flashRenderers.Length == 0)
        {
            flashRenderers = new SpriteRenderer[0];
        }

        startPos = Center;
        patrolLeftX = startPos.x - patrolLeftOffset;
        patrolRightX = startPos.x + patrolRightOffset;
        patrolDir = 1;

        // 初始化冷却：开局 Cast 和 Spell 进入 CD，防止立即释放
        castCDTimer = castCooldown * 0.5f;
        spellCDTimer = spellCooldown * 0.5f;
    }

    void Update()
    {
        if (state == State.Dead) return;

        // 递减冷却计时器
        if (attackCDTimer > 0) attackCDTimer -= Time.deltaTime;
        if (castCDTimer > 0) castCDTimer -= Time.deltaTime;
        if (spellCDTimer > 0) spellCDTimer -= Time.deltaTime;

        switch (state)
        {
            case State.Patrol:
                Patrol();
                TryDetectPlayer();
                break;

            case State.Wait:
                waitTimer -= Time.deltaTime;
                SetAnimIdle();
                if (waitTimer <= 0) state = State.Patrol;
                TryDetectPlayer();
                break;

            case State.Chase:
                ChasePlayer();
                break;

            case State.Action:
                // 动作中完全由协程控制，Update 不做任何处理
                break;

            case State.Hurt:
                // 受击硬直中，等待协程恢复
                break;
        }
    }

    // ══════════════════ 巡逻 ══════════════════

    void Patrol()
    {
        if (isDoingAction) return;

        SetAnimRun();
        float centerX = Center.x;
        float targetX = patrolDir > 0 ? patrolRightX : patrolLeftX;
        float step = patrolSpeed * Time.deltaTime;

        Vector2 pos = transform.position;
        pos.x = Mathf.MoveTowards(pos.x, targetX + (pos.x - centerX), step);
        transform.position = pos;

        FaceDirection(patrolDir);

        if (Mathf.Abs(centerX - targetX) < 0.05f)
        {
            patrolDir *= -1;
            state = State.Wait;
            waitTimer = patrolWaitTime;
            SetAnimIdle();
        }
    }

    // ══════════════════ 索敌 ══════════════════

    void TryDetectPlayer()
    {
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
        if (isDoingAction) return;

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
            SetAnimIdle();
            return;
        }

        // ──────── 技能优先级判断（从高到低） ────────

        // 1. Spell 传送：HP 低于阈值时优先使用（脱战/拉距离）
        if (spellCDTimer <= 0 && currentHP / maxHP <= spellHPThreshold)
        {
            StartCoroutine(SpellRoutine());
            return;
        }

        // 2. Cast 重击：在 Cast 范围内且 CD 就绪
        if (dist <= castRange && castCDTimer <= 0 && attackCDTimer <= 0)
        {
            StartCoroutine(CastRoutine());
            return;
        }

        // 3. 普通攻击：在攻击范围内且 CD 就绪
        if (dist <= attackRange && attackCDTimer <= 0)
        {
            StartCoroutine(AttackRoutine());
            return;
        }

        // 追击移动
        float dir = playerTarget.position.x > center.x ? 1f : -1f;
        float step = chaseSpeed * Time.deltaTime;

        SetAnimRun();

        Vector2 pos = transform.position;
        pos.x += dir * step;
        transform.position = pos;

        FaceDirection(dir > 0 ? 1 : -1);
    }

    // ══════════════════ 普通攻击 ══════════════════

    private IEnumerator AttackRoutine()
    {
        if (isDoingAction) yield break;
        isDoingAction = true;
        state = State.Action;

        // 停止移动
        StopMovement();
        FacePlayer();

        // 触发攻击动画
        if (animator != null)
        {
            animator.SetBool("IsRunning", false);
            animator.SetTrigger("Attack");
        }

        // 延迟造成伤害（配合动画命中帧）
        StartCoroutine(DealDamageAfterDelay(attackDamageDelay, attackDamage, attackRange));

        // 等待攻击动画播放完毕
        yield return new WaitForSeconds(attackDuration);

        // 设置冷却
        attackCDTimer = attackCooldown;

        // 恢复状态
        FinishAction();
    }

    // ══════════════════ 重击 Cast ══════════════════

    private IEnumerator CastRoutine()
    {
        if (isDoingAction) yield break;
        isDoingAction = true;
        state = State.Action;

        StopMovement();
        FacePlayer();

        // 触发 Cast 动画
        if (animator != null)
        {
            animator.SetBool("IsRunning", false);
            animator.SetTrigger("Cast");
        }

        // 延迟造成高伤害
        float castDamage = attackDamage * castDamageMultiplier;
        StartCoroutine(DealDamageAfterDelay(castDamageDelay, castDamage, castDamageRadius));

        Debug.Log($"{gameObject.name} 释放重击 Cast！伤害: {castDamage:F0}");

        // 等待动画播放完毕
        yield return new WaitForSeconds(castDuration);

        // 设置冷却（Cast 也同时触发普通攻击的 CD，防止连续攻击）
        castCDTimer = castCooldown;
        attackCDTimer = attackCooldown;

        FinishAction();
    }

    // ══════════════════ 传送 Spell ══════════════════

    private IEnumerator SpellRoutine()
    {
        if (isDoingAction) yield break;
        isDoingAction = true;
        state = State.Action;

        StopMovement();

        // 触发 Spell 动画
        if (animator != null)
        {
            animator.SetBool("IsRunning", false);
            animator.SetTrigger("Spell");
        }

        Debug.Log($"{gameObject.name} 释放传送 Spell！传送距离: {spellTeleportDistance}");

        // 等待传送时机（动画中消失的帧）
        yield return new WaitForSeconds(spellTeleportDelay);

        // 执行传送：朝远离玩家的方向传送
        PerformTeleport();

        // 等待剩余动画播放
        yield return new WaitForSeconds(spellDuration - spellTeleportDelay);

        spellCDTimer = spellCooldown;

        FinishAction();
    }

    /// <summary>
    /// 执行传送位移，朝远离玩家的方向移动。
    /// 如果传送后超出巡逻范围，则传送到巡逻边界。
    /// </summary>
    private void PerformTeleport()
    {
        if (playerTarget == null) return;

        // 计算传送方向：远离玩家
        float dirAway = transform.position.x > playerTarget.position.x ? 1f : -1f;
        float targetX = transform.position.x + dirAway * spellTeleportDistance;

        // 钳制在巡逻范围内（防止传送到地图外）
        // 使用较宽的范围容忍，允许略微超出巡逻范围
        float clampedX = Mathf.Clamp(targetX, patrolLeftX - 2f, patrolRightX + 2f);

        Vector2 pos = transform.position;
        pos.x = clampedX;
        transform.position = pos;

        // 传送后面朝玩家
        FacePlayer();

        Debug.Log($"{gameObject.name} 传送至 x={clampedX:F1}");
    }

    // ══════════════════ 伤害判定 ══════════════════

    /// <summary>
    /// 延迟伤害判定：在指定延迟后，对范围内的玩家造成伤害。
    /// </summary>
    private IEnumerator DealDamageAfterDelay(float delay, float damage, float range)
    {
        yield return new WaitForSeconds(delay);

        // 死亡后不再造成伤害
        if (state == State.Dead) yield break;

        // 计算攻击判定点（面朝方向偏移）
        float lookDir = GetFacingDirection();
        Vector2 attackPos = Center + new Vector2(lookDir * range * 0.6f, 0);

        Collider2D[] hits = Physics2D.OverlapCircleAll(attackPos, range, playerLayer);
        foreach (var hit in hits)
        {
            PlayerStats playerStats = hit.GetComponent<PlayerStats>();
            if (playerStats != null)
            {
                playerStats.TakeDamage(damage);
                Debug.Log($"{gameObject.name} 命中玩家！伤害: {damage:F0}");
            }
        }
    }

    // ══════════════════ 受击与死亡 ══════════════════

    /// <summary>
    /// 受到伤害 - 播放 Hurt 动画 + 手部闪红反馈
    /// </summary>
    public void TakeDamage(float damage)
    {
        if (state == State.Dead) return;

        currentHP -= damage;
        Debug.Log($"{gameObject.name} 受到 {damage:F0} 伤害，剩余 HP: {currentHP:F0}");

        // 手部闪红反馈（仅对指定的 SpriteRenderer 闪红）
        if (flashRenderers != null && flashRenderers.Length > 0)
        {
            foreach (var sr in flashRenderers)
            {
                if (sr != null) sr.color = Color.red;
            }
            Invoke(nameof(ResetFlashColor), hitFlashDuration);
        }

        // 如果当前不在动作中（攻击/施法），则播放受伤动画并进入硬直
        if (!isDoingAction)
        {
            StartCoroutine(HurtRoutine());
        }
        // 如果正在执行动作（攻击/施法），不打断当前动作，只闪红

        if (currentHP <= 0f)
        {
            Die();
        }
    }

    private IEnumerator HurtRoutine()
    {
        state = State.Hurt;
        StopMovement();

        if (animator != null)
        {
            animator.SetBool("IsRunning", false);
            animator.SetTrigger("Hurt");
        }

        yield return new WaitForSeconds(hurtDuration);

        // 受伤恢复后重新评估状态
        if (state == State.Dead) yield break;

        if (playerTarget != null)
        {
            float dist = Vector2.Distance(Center, playerTarget.position);
            state = dist <= detectRadius ? State.Chase : State.Patrol;
        }
        else
        {
            state = State.Patrol;
        }
    }

    private void ResetFlashColor()
    {
        if (flashRenderers != null)
        {
            foreach (var sr in flashRenderers)
            {
                if (sr != null) sr.color = Color.white;
            }
        }
    }

    void Die()
    {
        if (state == State.Dead) return;
        state = State.Dead;
        isDoingAction = false; // 解锁，防止协程残留

        // 停止所有协程
        StopAllCoroutines();

        Debug.Log($"{gameObject.name} 被击杀！掉落经验 {expReward}");

        // ① 禁用碰撞体和刚体
        if (col != null) col.enabled = false;
        Collider2D[] allCols = GetComponents<Collider2D>();
        foreach (var c in allCols) c.enabled = false;

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.bodyType = RigidbodyType2D.Kinematic;
        }

        // ② 触发死亡动画
        if (animator != null)
        {
            animator.SetBool("IsRunning", false);
            animator.SetTrigger("Die");
        }

        // ③ 通知玩家经验
        PlayerStats stats = FindAnyObjectByType<PlayerStats>();
        if (stats != null)
        {
            stats.OnEnemyKilled(expReward);
        }

        // ④ 掉落物品
        if (healthPotionPrefab != null && Random.value <= dropPotionChance)
        {
            Instantiate(healthPotionPrefab, transform.position, Quaternion.identity);
            Debug.Log($"{gameObject.name} 掉落了血瓶！");
        }

        // ⑤ 重置突刺 CD
        PlayerController pc = FindAnyObjectByType<PlayerController>();
        if (pc != null && pc.IsDashing)
        {
            pc.InstantResetDash();
        }

        // ⑥ 延迟销毁
        StartCoroutine(DestroyAfterDelay(deathDestroyDelay));
    }

    private IEnumerator DestroyAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        Destroy(gameObject);
    }

    // ══════════════════ 工具方法 ══════════════════

    /// <summary>
    /// 结束当前动作，恢复到追击或巡逻状态。
    /// </summary>
    private void FinishAction()
    {
        isDoingAction = false;

        if (state == State.Dead) return;

        if (playerTarget != null)
        {
            float dist = Vector2.Distance(Center, playerTarget.position);
            state = dist <= detectRadius ? State.Chase : State.Patrol;
        }
        else
        {
            state = State.Patrol;
        }
    }

    /// <summary>
    /// 完全停止物理移动。
    /// </summary>
    private void StopMovement()
    {
        if (rb != null) rb.linearVelocity = Vector2.zero;
    }

    /// <summary>
    /// 面朝玩家方向。
    /// </summary>
    private void FacePlayer()
    {
        if (playerTarget == null) return;
        int dir = playerTarget.position.x > Center.x ? 1 : -1;
        FaceDirection(dir);
    }

    /// <summary>
    /// 获取当前面朝方向：1=右，-1=左。
    /// </summary>
    private float GetFacingDirection()
    {
        if (defaultFacingLeft)
            return transform.localScale.x > 0 ? -1f : 1f;
        else
            return transform.localScale.x > 0 ? 1f : -1f;
    }

    void FaceDirection(int dir)
    {
        Vector3 localScale = transform.localScale;

        if (defaultFacingLeft)
        {
            localScale.x = dir > 0 ? -Mathf.Abs(localScale.x) : Mathf.Abs(localScale.x);
        }
        else
        {
            localScale.x = dir < 0 ? -Mathf.Abs(localScale.x) : Mathf.Abs(localScale.x);
        }

        transform.localScale = localScale;
    }

    // ────────────────── 动画辅助 ──────────────────

    private void SetAnimIdle()
    {
        if (animator != null) animator.SetBool("IsRunning", false);
    }

    private void SetAnimRun()
    {
        if (animator != null) animator.SetBool("IsRunning", true);
    }

    // ────────────────── 墙壁碰撞 ──────────────────

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (state == State.Dead) return;

        if (((1 << collision.gameObject.layer) & environmentLayer) != 0)
        {
            foreach (ContactPoint2D contact in collision.contacts)
            {
                if ((patrolDir > 0 && contact.normal.x < -0.5f) ||
                    (patrolDir < 0 && contact.normal.x > 0.5f))
                {
                    patrolDir *= -1;
                    FaceDirection(patrolDir);

                    if (state == State.Patrol || state == State.Wait)
                    {
                        state = State.Patrol;
                    }
                    break;
                }
            }
        }
    }

    // ────────────────── Gizmos ──────────────────

    void OnDrawGizmosSelected()
    {
        Vector3 center = Application.isPlaying ? (Vector3)startPos : transform.position;
        float left = Application.isPlaying ? patrolLeftX : center.x - patrolLeftOffset;
        float right = Application.isPlaying ? patrolRightX : center.x + patrolRightOffset;

        // 巡逻范围（黄色）
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(new Vector3(left, center.y, 0), new Vector3(right, center.y, 0));

        // 索敌范围（红色）
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(center, detectRadius);

        // 攻击范围（品红）
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(center, attackRange);

        // Cast 范围（青色）
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(center, castRange);
    }
}
