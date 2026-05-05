using UnityEngine;
using System.Collections;
using TealFalconEnemySeries;

/// <summary>
/// DarkKnightAI.cs - 黑暗骑士 AI 大脑脚本
/// 实现了攻击与技能期间完全静止，且两者互不打断的严格逻辑。
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
    [SerializeField] private float attackRange = 1.9f;
    [SerializeField] private float beamAttackRange = 8f;
    [SerializeField] private float attackDamage = 35f;
    [SerializeField] private float attackCooldown = 1.6f; 
    [SerializeField] private float beamCooldown = 6.0f;   
    [SerializeField] private float damageDelay = 0.45f;   
    [SerializeField] private float runDistance = 4.5f; 

    // ────────────────── 生态属性 ──────────────────
    [Header("=== 生态属性 ===")]
    [SerializeField] private float expReward = 2000f;
    [SerializeField] private GameObject healthPotionPrefab;
    [SerializeField] private float dropPotionChance = 1.0f;
    [SerializeField] private float deathDestroyDelay = 3.5f;

    // ────────────────── 受击反馈 ──────────────────
    [Header("=== 受击反馈 ===")]
    [SerializeField] private float hitFlashDuration = 0.1f;

    // ────────────────── 状态定义 ──────────────────
    private enum AIState { Patrol, Chase, Attack, Cooldown, Dead }
    [SerializeField] private AIState currentState = AIState.Patrol;

    private DarkKnightController body;
    private Rigidbody2D rb;
    private Collider2D col;
    private Animator animator;
    
    private Vector2 startPos;
    private float patrolLeftX;
    private float patrolRightX;
    private bool isMovingRight = true;
    private float stateTimer;
    private float beamTimer; 
    private Transform targetPlayer;
    private bool isDoingAction = false; // 动作互斥锁

    // ══════════════════ 生命周期 ══════════════════

    private void Awake()
    {
        body = GetComponent<DarkKnightController>();
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        animator = body.transform.Find("Root")?.GetComponent<Animator>();
    }

    private void Start()
    {
        currentHP = maxHP;
        startPos = transform.position;
        patrolLeftX = startPos.x - patrolLeftOffset;
        patrolRightX = startPos.x + patrolRightOffset;
        currentState = AIState.Patrol;
        beamTimer = beamCooldown; // 开局技能进入CD，防止立即释放
    }

    private void Update()
    {
        if (currentState == AIState.Dead) return;

        if (stateTimer > 0) stateTimer -= Time.deltaTime;
        if (beamTimer > 0) beamTimer -= Time.deltaTime;

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
                // 攻击中完全交给协程控制，Update 不做任何移动
                break;
            case AIState.Cooldown:
                HandleCooldown();
                break;
        }
    }

    // ══════════════════ AI 逻辑方法 ══════════════════

    private void HandlePatrol()
    {
        if (isDoingAction) return;
        EnsureIdleFightingState();
        body.ActivateWalk();

        float targetX = isMovingRight ? patrolRightX : patrolLeftX;
        CheckFlip(isMovingRight);

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
        if (isDoingAction) return;
        if (targetPlayer == null)
        {
            currentState = AIState.Patrol;
            return;
        }

        float distance = Vector2.Distance(transform.position, targetPlayer.position);

        if (distance > loseChasingDistance)
        {
            targetPlayer = null;
            currentState = AIState.Patrol;
            return;
        }

        // 攻击判断：互斥检查
        if (distance <= attackRange && stateTimer <= 0)
        {
            StartCoroutine(MeleeAttackRoutine());
            return;
        }
        else if (distance <= beamAttackRange && beamTimer <= 0 && stateTimer <= 0)
        {
            StartCoroutine(BeamAttackRoutine());
            return;
        }

        // 追逐移动逻辑
        EnsureIdleFightingState();
        if (distance > runDistance)
            body.ActivateRun();
        else
            body.ActivateWalk();
            
        CheckFlip(targetPlayer.position.x > transform.position.x);
    }

    private void HandleCooldown()
    {
        if (targetPlayer != null)
        {
            float distance = Vector2.Distance(transform.position, targetPlayer.position);
            EnsureIdleFightingState();
            if (distance > runDistance) body.ActivateRun();
            else body.ActivateWalk();
            CheckFlip(targetPlayer.position.x > transform.position.x);
        }

        if (stateTimer <= 0)
        {
            currentState = targetPlayer != null ? AIState.Chase : AIState.Patrol;
            return;
        }
    }

    private void EnsureIdleFightingState()
    {
        if (body.CurrentFightingState == DarkKnightController.FightingState.OnGuard)
            body.ActivateGuard(); 
        else if (body.CurrentFightingState != DarkKnightController.FightingState.Idle)
            body.ActivateIdle();
    }

    // ══════════════════ 攻击执行 (静止 & 互斥) ══════════════════

    private IEnumerator MeleeAttackRoutine()
    {
        // 1. 严格互斥检查
        if (isDoingAction) yield break;
        isDoingAction = true;
        currentState = AIState.Attack;
        
        // 2. 强行停止所有物理位移
        if (rb != null) rb.linearVelocity = Vector2.zero;
        body.currentSpeed = 0;
        body.ActivateIdle();
        
        if (animator != null) {
            animator.SetFloat("Speed", 0);
            animator.SetBool("Busy", true);
            animator.Play("Attack", 0, 0f); 
        }

        if (body._Channel != null && body.SwordSound != null)
            body._Channel.PlayOneShot(body.SwordSound);

        // 注意：此处不再添加 AddForce，实现完全原地攻击
        body.CurrentFightingState = DarkKnightController.FightingState.Attacking;
        StartCoroutine(DamageCheckRoutine(damageDelay));
        
        yield return new WaitForSeconds(1.1f); 
        
        if (animator != null) animator.SetBool("Busy", false);
        body.CurrentFightingState = DarkKnightController.FightingState.Idle;
        
        stateTimer = attackCooldown;
        currentState = AIState.Cooldown;
        
        // 3. 动作彻底结束才解锁
        isDoingAction = false;
    }

    private IEnumerator BeamAttackRoutine()
    {
        // 1. 严格互斥检查
        if (isDoingAction) yield break;
        isDoingAction = true;
        currentState = AIState.Attack;
        beamTimer = beamCooldown;
        
        // 2. 强行停止所有物理位移
        if (rb != null) rb.linearVelocity = Vector2.zero;
        body.currentSpeed = 0;
        body.ActivateIdle();
        
        body.ActivateBeamAttack();
        
        Transform shotRef = body.transform.Find("Root/Head_Pivot/BeamAttackRef");
        float trackTime = 0;
        while (trackTime < 2.5f) {
            // 蓄力期间仅允许转身，不准移动
            if (rb != null) rb.linearVelocity = Vector2.zero;
            
            if (targetPlayer != null && body.DarkBall != null) {
                CheckFlip(targetPlayer.position.x > transform.position.x);
                BeamShot bs = body.DarkBall.GetComponent<BeamShot>();
                if (bs != null && shotRef != null) {
                    Vector2 dir = (targetPlayer.position - shotRef.position).normalized;
                    bs.direction = dir * (transform.localScale.x > 0 ? 1f : -1f);
                }
            }
            trackTime += Time.deltaTime;
            yield return null;
        }
        
        yield return new WaitForSeconds(1.0f); 
        currentState = AIState.Cooldown;
        stateTimer = 0.5f;
        
        // 3. 动作彻底结束才解锁
        isDoingAction = false;
    }

    private IEnumerator DamageCheckRoutine(float delay)
    {
        yield return new WaitForSeconds(delay);
        float lookDir = transform.localScale.x > 0 ? 1f : -1f;
        Vector2 attackPos = (Vector2)transform.position + new Vector2(lookDir * 1.5f, 0);

        Collider2D[] hits = Physics2D.OverlapCircleAll(attackPos, 1.5f, playerLayer);
        foreach (var hit in hits)
        {
            if (hit.TryGetComponent(out PlayerStats stats))
                stats.TakeDamage(attackDamage);
        }
    }

    // ══════════════════ 战斗与死亡 ══════════════════

    public void TakeDamage(float damage)
    {
        if (currentState == AIState.Dead) return;
        currentHP -= damage;

        // 受击时立即清零速度，防止惯性冲刺
        if (rb != null) rb.linearVelocity = Vector2.zero;
        body.currentSpeed = 0;

        // 闪烁反馈：遍历 DarkKnightController 已收集的所有子 SpriteRenderer
        if (body.SRList != null && body.SRList.Count > 0)
        {
            foreach (var sr in body.SRList)
            {
                sr.color = Color.red;
            }
            Invoke(nameof(ResetColor), hitFlashDuration);
        }

        body.ActivateHurt();
        if (currentHP <= 0) Die();
    }

    private void ResetColor()
    {
        if (body != null && body.SRList != null)
        {
            foreach (var sr in body.SRList)
            {
                sr.color = Color.white;
            }
        }
    }

    private void Die()
    {
        currentState = AIState.Dead;
        body.ActivateDeath();
        foreach (var c in GetComponents<Collider2D>()) c.enabled = false;
        if (rb != null) { rb.linearVelocity = Vector2.zero; rb.bodyType = RigidbodyType2D.Kinematic; }
        
        PlayerStats stats = FindAnyObjectByType<PlayerStats>();
        if (stats != null) stats.OnEnemyKilled(expReward);

        if (healthPotionPrefab != null && Random.value <= dropPotionChance)
            Instantiate(healthPotionPrefab, transform.position, Quaternion.identity);

        StartCoroutine(DestroyProcess());
    }

    private IEnumerator DestroyProcess()
    {
        yield return new WaitForSeconds(deathDestroyDelay);
        Destroy(gameObject);
    }

    private void CheckFlip(bool faceRight)
    {
        if (faceRight != (transform.localScale.x > 0)) body.Flip();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Vector3 start = Application.isPlaying ? (Vector3)startPos : transform.position;
        Gizmos.DrawLine(start + Vector3.left * patrolLeftOffset, start + Vector3.right * patrolRightOffset);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
