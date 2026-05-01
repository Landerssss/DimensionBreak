using UnityEngine;
using System.Collections;
using TealFalconEnemySeries;

/// <summary>
/// DarkKnightAI.cs - 黑暗骑士 AI 大脑脚本
/// 负责状态切换、索敌逻辑与伤害判定。
/// 驱动 DarkKnightController (身体) 执行具体动作。
/// </summary>
[RequireComponent(typeof(DarkKnightController))]
public class DarkKnightAI : MonoBehaviour
{
    // ────────────────── 基础属性 ──────────────────
    [Header("=== 基础属性 ===")]
    [SerializeField] private float maxHP = 500f;
    [SerializeField] private float currentHP;

    // ────────────────── 巡逻属性 ──────────────────
    [Header("=== 巡逻属性 ===")]
    [SerializeField] private float patrolLeftOffset = 5f;
    [SerializeField] private float patrolRightOffset = 5f;
    [SerializeField] private float patrolWaitTime = 1f;

    // ────────────────── 索敌属性 ──────────────────
    [Header("=== 索敌属性 ===")]
    [SerializeField] private float detectRadius = 7f;
    [SerializeField] private float loseChasingDistance = 12f;
    [SerializeField] private LayerMask playerLayer;

    // ────────────────── 攻击属性 ──────────────────
    [Header("=== 攻击属性 ===")]
    [Tooltip("近战触发距离")]
    [SerializeField] private float attackRange = 1.8f;
    [Tooltip("远程光束触发距离")]
    [SerializeField] private float beamAttackRange = 8f;
    [SerializeField] private float attackDamage = 35f;
    [SerializeField] private float attackCooldown = 3f;
    [SerializeField] private float damageDelay = 0.5f; // 动画播放到伤害产生的时间

    // ────────────────── 生态属性 ──────────────────
    [Header("=== 生态属性 ===")]
    [SerializeField] private float expReward = 2000f;
    [SerializeField] private GameObject healthPotionPrefab;
    [SerializeField] private float dropPotionChance = 1.0f;
    [SerializeField] private float deathDestroyDelay = 3.5f;

    // ────────────────── 状态定义 ──────────────────
    private enum AIState { Patrol, Chase, Attack, Cooldown, Dead }
    [SerializeField, ReadOnlyInspector] private AIState currentState = AIState.Patrol;

    private DarkKnightController body;
    private Rigidbody2D rb;
    private Collider2D col;
    
    private Vector2 startPos;
    private float patrolLeftX;
    private float patrolRightX;
    private bool isMovingRight = true;
    private float stateTimer;
    private Transform targetPlayer;

    // ══════════════════ 生命周期 ══════════════════

    private void Awake()
    {
        body = GetComponent<DarkKnightController>();
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
    }

    private void Start()
    {
        currentHP = maxHP;
        startPos = transform.position;
        patrolLeftX = startPos.x - patrolLeftOffset;
        patrolRightX = startPos.x + patrolRightOffset;

        // 默认进入巡逻状态
        currentState = AIState.Patrol;
    }

    private void Update()
    {
        if (currentState == AIState.Dead) return;

        switch (currentState)
        {
            case AIState.Patrol:
                HandlePatrol();
                TryDetectPlayer();
                break;
            case AIState.Chase:
                HandleChase();
                break;
            case AIState.Attack:
                // 攻击逻辑由 HandleChase 或状态切换触发，此处主要等待动画
                break;
            case AIState.Cooldown:
                HandleCooldown();
                break;
        }
    }

    // ══════════════════ AI 逻辑方法 ══════════════════

    private void HandlePatrol()
    {
        EnsureIdleFightingState();
        body.ActivateWalk();

        float targetX = isMovingRight ? patrolRightX : patrolLeftX;
        
        // 检查朝向是否正确
        CheckFlip(isMovingRight);

        // 如果到达边界
        if (Mathf.Abs(transform.position.x - targetX) < 0.2f)
        {
            isMovingRight = !isMovingRight;
            body.ActivateIdle();
            currentState = AIState.Cooldown;
            stateTimer = patrolWaitTime;
        }
    }

    private void TryDetectPlayer()
    {
        Collider2D hit = Physics2D.OverlapCircle(transform.position, detectRadius, playerLayer);
        if (hit != null)
        {
            targetPlayer = hit.transform;
            currentState = AIState.Chase;
        }
    }

    private void HandleChase()
    {
        if (targetPlayer == null)
        {
            currentState = AIState.Patrol;
            return;
        }

        float distance = Vector2.Distance(transform.position, targetPlayer.position);

        // 丢弃目标
        if (distance > loseChasingDistance)
        {
            targetPlayer = null;
            currentState = AIState.Patrol;
            return;
        }

        // 攻击判断
        if (distance <= attackRange)
        {
            StartMeleeAttack();
            return;
        }
        else if (distance <= beamAttackRange && stateTimer <= 0)
        {
            // 如果玩家在光束射程内且 CD 好了
            StartBeamAttack();
            return;
        }

        // 追逐移动
        EnsureIdleFightingState();
        body.ActivateRun();
        bool shouldMoveRight = targetPlayer.position.x > transform.position.x;
        CheckFlip(shouldMoveRight);
    }

    private void HandleCooldown()
    {
        stateTimer -= Time.deltaTime;

        if (stateTimer <= 0)
        {
            if (targetPlayer != null) currentState = AIState.Chase;
            else currentState = AIState.Patrol;
            return;
        }

        // CD 期间随机丰富动作（防御或后撤）
        // 注意：BackStep 只能在 OnGuard 状态下调用
        if (Random.value < 0.01f) 
        {
            if (body.CurrentFightingState == DarkKnightController.FightingState.Idle)
            {
                body.ActivateGuard();
            }
            else if (body.CurrentFightingState == DarkKnightController.FightingState.OnGuard && Random.value > 0.7f)
            {
                body.ActivateBackStep();
            }
        }
    }

    /// <summary>
    /// 确保身体处于 Idle 战斗状态，以便能够切换到行走/跑步。
    /// </summary>
    private void EnsureIdleFightingState()
    {
        if (body.CurrentFightingState == DarkKnightController.FightingState.OnGuard)
        {
            body.ActivateGuard(); // 再次调用会切换回 Idle 并取消动画 Bool
        }
        else if (body.CurrentFightingState != DarkKnightController.FightingState.Idle)
        {
            body.ActivateIdle();
        }
    }

    // ══════════════════ 攻击执行 ══════════════════

    private void StartMeleeAttack()
    {
        currentState = AIState.Attack;
        body.ActivateIdle();
        
        // DarkKnightController 要求在 OnGuard 状态下才能发动攻击
        if (body.CurrentFightingState != DarkKnightController.FightingState.OnGuard)
        {
            body.ActivateGuard();
        }
        
        body.ActivateAttack();
        
        // 开启伤害检测协程
        StartCoroutine(DamageCheckRoutine(damageDelay));
        
        // 进入 CD
        stateTimer = attackCooldown;
        StartCoroutine(SwitchToCooldownDelayed(1.5f)); // 等待动画播完
    }

    private void StartBeamAttack()
    {
        currentState = AIState.Attack;
        body.ActivateIdle();
        
        body.ActivateBeamAttack();
        
        // 进入 CD
        stateTimer = attackCooldown;
        StartCoroutine(SwitchToCooldownDelayed(4.0f)); // 光束动画较长
    }

    private IEnumerator DamageCheckRoutine(float delay)
    {
        yield return new WaitForSeconds(delay);

        // 伤害检测：在前方扇形或圆形区域内检测玩家
        Vector2 attackPos = transform.position;
        // 偏移一点到前方
        float lookDir = transform.localScale.x > 0 ? 1f : -1f;
        attackPos.x += lookDir * 1.5f;

        Collider2D[] hits = Physics2D.OverlapCircleAll(attackPos, 1.5f, playerLayer);
        foreach (var hit in hits)
        {
            PlayerStats stats = hit.GetComponent<PlayerStats>();
            if (stats != null)
            {
                stats.TakeDamage(attackDamage);
            }
        }
    }

    private IEnumerator SwitchToCooldownDelayed(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (currentState != AIState.Dead)
            currentState = AIState.Cooldown;
    }

    // ══════════════════ 战斗接口 ══════════════════

    public void TakeDamage(float damage)
    {
        if (currentState == AIState.Dead) return;

        currentHP -= damage;
        body.ActivateHurt();

        if (currentHP <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        currentState = AIState.Dead;
        
        // 1. 身体反馈
        body.ActivateDeath();

        // 2. 禁用物理
        if (col != null) col.enabled = false;
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.bodyType = RigidbodyType2D.Kinematic;
        }

        // 3. 经验结算
        PlayerStats stats = FindAnyObjectByType<PlayerStats>();
        if (stats != null)
        {
            stats.OnEnemyKilled(expReward);
        }

        // 4. 掉落
        if (healthPotionPrefab != null && Random.value <= dropPotionChance)
        {
            Instantiate(healthPotionPrefab, transform.position, Quaternion.identity);
        }

        // 5. 延迟销毁
        StartCoroutine(DestroyProcess());
    }

    private IEnumerator DestroyProcess()
    {
        yield return new WaitForSeconds(deathDestroyDelay);
        Destroy(gameObject);
    }

    // ══════════════════ 工具方法 ══════════════════

    private void CheckFlip(bool faceRight)
    {
        // DarkKnightController 的 Flip() 会反转 localScale.x
        // 我们根据 scale.x 的正负判断当前朝向
        bool currentFacingRight = transform.localScale.x > 0;
        
        if (faceRight != currentFacingRight)
        {
            body.Flip();
        }
    }

    private void OnDrawGizmosSelected()
    {
        // 巡逻范围
        Gizmos.color = Color.yellow;
        Vector3 start = Application.isPlaying ? (Vector3)startPos : transform.position;
        Gizmos.DrawLine(start + Vector3.left * patrolLeftOffset, start + Vector3.right * patrolRightOffset);

        // 索敌范围
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, detectRadius);

        // 丢失范围
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, loseChasingDistance);

        // 攻击范围
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
        
        // 伤害判定点预览
        float lookDir = transform.localScale.x > 0 ? 1f : -1f;
        Gizmos.DrawWireSphere(transform.position + new Vector3(lookDir * 1.5f, 0, 0), 1.5f);
    }
}

/// <summary>
/// 辅助特性，用于在面板上显示只读属性
/// </summary>
public class ReadOnlyInspectorAttribute : PropertyAttribute { }

#if UNITY_EDITOR
[UnityEditor.CustomPropertyDrawer(typeof(ReadOnlyInspectorAttribute))]
public class ReadOnlyInspectorDrawer : UnityEditor.PropertyDrawer
{
    public override void OnGUI(Rect position, UnityEditor.SerializedProperty property, GUIContent label)
    {
        GUI.enabled = false;
        UnityEditor.EditorGUI.PropertyField(position, property, label);
        GUI.enabled = true;
    }
}
#endif
