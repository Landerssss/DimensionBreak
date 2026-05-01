using System.Collections;
using UnityEngine;

namespace TealFalconEnemySeries
{
    /// <summary>
    /// DarkKnightAI —— 暗黑骑士的"大脑（Brain）"脚本。
    /// 负责状态机逻辑（巡逻 → 追击 → 攻击 → 冷却 → 死亡），
    /// 所有动画与物理由"身体（Body）"DarkKnightController 负责。
    /// 本脚本只调用 DarkKnightController 的 Public 方法，绝不修改 Body 内部逻辑。
    /// </summary>
    [RequireComponent(typeof(DarkKnightController))]
    public class DarkKnightAI : MonoBehaviour
    {
        // ══════════════════ Inspector 面板变量 ══════════════════

        [Header("=== 基础属性 ===")]
        [SerializeField] private float maxHP = 200f;
        [SerializeField] private float currentHP;

        [Header("=== 巡逻属性 ===")]
        [Tooltip("相对于初始位置的左侧巡逻距离")]
        [SerializeField] private float patrolLeftOffset  = 4f;
        [Tooltip("相对于初始位置的右侧巡逻距离")]
        [SerializeField] private float patrolRightOffset = 4f;

        [Header("=== 索敌属性 ===")]
        [Tooltip("检测玩家的半径")]
        [SerializeField] private float detectRadius       = 6f;
        [Tooltip("超过此距离则丢失追击目标（应大于 detectRadius）")]
        [SerializeField] private float loseChasingDistance = 10f;
        [Tooltip("玩家所在 LayerMask")]
        [SerializeField] private LayerMask playerLayer;

        [Header("=== 攻击属性 ===")]
        [Tooltip("近战触发距离：玩家进入此范围时使用近战攻击")]
        [SerializeField] private float attackRange      = 1.5f;
        [Tooltip("远程触发距离：玩家在此范围内但超出近战范围时使用光束攻击")]
        [SerializeField] private float beamAttackRange  = 5f;
        [Tooltip("近战攻击伤害")]
        [SerializeField] private float attackDamage     = 20f;
        [Tooltip("攻击冷却时间（秒）")]
        [SerializeField] private float attackCooldown   = 2.5f;
        [Tooltip("近战伤害检测延迟（对应攻击动画的出招帧，秒）")]
        [SerializeField] private float meleeDamageDelay = 0.4f;

        [Header("=== 生态属性 ===")]
        [Tooltip("击杀奖励经验值")]
        [SerializeField] private float expReward         = 800f;
        [Tooltip("血瓶掉落预制体")]
        [SerializeField] private GameObject healthPotionPrefab;
        [Tooltip("血瓶掉落概率（0 ~ 1）")]
        [SerializeField] private float dropPotionChance  = 0.3f;
        [Tooltip("死亡溶解完毕后销毁 GameObject 的等待时间（秒）")]
        [SerializeField] private float deathDestroyDelay = 4f;

        // ══════════════════ 内部状态定义 ══════════════════

        private enum AIState
        {
            Patrol,     // 在起点附近来回巡逻
            Chase,      // 发现玩家，向玩家冲刺
            PreAttack,  // 准备攻击：先进入 Guard 状态
            Attack,     // 已触发攻击动画，等待 Body 完成
            Cooldown,   // 攻击冷却期间：随机守卫/后撤步
            Dead        // 死亡，不再做任何事
        }

        private AIState currentState = AIState.Patrol;

        // ══════════════════ 内部运行变量 ══════════════════

        private DarkKnightController body;      // 身体引用
        private Transform playerTarget;          // 当前追击目标
        private Collider2D col;                  // 主碰撞体（用于 bounds.center）

        private Vector2 startPos;               // 初始位置
        private float patrolLeftX;              // 巡逻左边界 X
        private float patrolRightX;             // 巡逻右边界 X
        private int   patrolDir = 1;            // 巡逻方向：1=右，-1=左

        private float cooldownTimer    = 0f;    // 攻击冷却计时
        private bool  isBeamCooldown   = false; // 远程攻击冷却标志
        private float beamCooldown     = 5f;    // 远程攻击冷却时间（秒）
        private float beamCooldownTimer = 0f;

        private bool isDead = false;            // 死亡防重入标志

        /// <summary>获取碰撞体中心，避免精灵锚点偏移导致位置偏差。</summary>
        private Vector2 Center => col != null ? (Vector2)col.bounds.center : (Vector2)transform.position;

        // ══════════════════ 生命周期 ══════════════════

        void Awake()
        {
            body = GetComponent<DarkKnightController>();
            col  = GetComponent<Collider2D>();
        }

        void Start()
        {
            currentHP = maxHP;

            // 记录初始位置与巡逻边界
            startPos       = Center;
            patrolLeftX    = startPos.x - patrolLeftOffset;
            patrolRightX   = startPos.x + patrolRightOffset;
            patrolDir      = 1; // 默认向右巡逻

            // 初始进入巡逻状态
            EnterPatrol();
        }

        void Update()
        {
            if (isDead) return;

            // 冷却计时器（始终递减）
            if (cooldownTimer > 0f)
                cooldownTimer -= Time.deltaTime;

            if (beamCooldownTimer > 0f)
                beamCooldownTimer -= Time.deltaTime;

            // 主状态机
            switch (currentState)
            {
                case AIState.Patrol:
                    UpdatePatrol();
                    TryDetectPlayer();
                    break;

                case AIState.Chase:
                    UpdateChase();
                    break;

                case AIState.PreAttack:
                    // PreAttack 是单帧过渡态，由 EnterPreAttack 处理，不在 Update 做事
                    break;

                case AIState.Attack:
                    // 等待 Body 完成攻击动画（Body 内部协程会将 FightingState 改回 OnGuard）
                    WaitForAttackFinish();
                    break;

                case AIState.Cooldown:
                    UpdateCooldown();
                    break;
            }
        }
        private void UpdateCooldown()
        {
            // 当总冷却时间归零时，判断下一步动作
            if (cooldownTimer <= 0f)
            {
                if (playerTarget != null)
                {
                    // 如果玩家还在，切换回追击状态并让身体跑起来[cite: 3]
                    currentState = AIState.Chase;
                    body.ActivateRun();
                }
                else
                {
                    // 否则返回巡逻状态[cite: 2]
                    EnterPatrol();
                }
            }
        }

        // ══════════════════ 巡逻逻辑 ══════════════════

        /// <summary>进入巡逻状态：告知 Body 切换到 Walk 动画。</summary>
        private void EnterPatrol()
        {
            currentState = AIState.Patrol;
            body.ActivateWalk();
        }

        /// <summary>
        /// 巡逻 Update：直接驱动 Transform 水平位移（Body 内部已处理 Rigidbody 速度，
        /// 但在 Patrol 状态下 AI 接管位移，让 Body 处于 Walk 显示状态即可）。
        /// 注意：Body 的 Update 会通过 currentSpeed 驱动 RB.velocity，
        /// 所以这里我们不再手动修 position，而是用 Flip() 来控制朝向，
        /// Body 的 MaxWalkSpeed 会决定实际移动速度。
        /// </summary>
        private void UpdatePatrol()
        {
            float centerX = Center.x;
            float targetX = patrolDir > 0 ? patrolRightX : patrolLeftX;

            // 确认 Body 是 Walk 状态（如果被打断后恢复）
            if (body.CurrentMovementState != DarkKnightController.MovementState.Walking &&
                body.CurrentFightingState == DarkKnightController.FightingState.Idle)
            {
                body.ActivateWalk();
            }

            // 根据巡逻方向决定 Body 的朝向（movingRight 为 true 表示面向右）
            bool shouldFaceRight = patrolDir > 0;
            if (body.movingRight != shouldFaceRight)
            {
                // 调用 Flip() 翻转朝向（Body 内部 currentSpeed 也会随之取反）
                body.Flip();
            }

            // 到达巡逻端点则转向
            if (Mathf.Abs(centerX - targetX) < 0.15f)
            {
                patrolDir *= -1; // 转向
            }
        }

        // ══════════════════ 索敌逻辑 ══════════════════

        /// <summary>尝试检测玩家；检测到则进入追击状态。</summary>
        private void TryDetectPlayer()
        {
            Collider2D hit = Physics2D.OverlapCircle(Center, detectRadius, playerLayer);
            if (hit != null)
            {
                playerTarget = hit.transform;
                EnterChase();
            }
        }

        // ══════════════════ 追击逻辑 ══════════════════

        /// <summary>进入追击状态：告知 Body 切换到 Run 动画。</summary>
        private void EnterChase()
        {
            currentState = AIState.Chase;
            body.ActivateRun();
        }

        private void UpdateChase()
        {
            // 目标丢失 → 返回巡逻
            if (playerTarget == null)
            {
                EnterPatrol();
                return;
            }

            float dist = Vector2.Distance(Center, playerTarget.position);

            // 超出追击距离 → 丢失目标，返回巡逻
            if (dist > loseChasingDistance)
            {
                playerTarget = null;
                EnterPatrol();
                return;
            }

            // 进入近战范围 → 触发近战攻击
            if (dist <= attackRange && cooldownTimer <= 0f)
            {
                EnterPreAttack(isMelee: true);
                return;
            }

            // 进入远程范围但不在近战范围内 → 触发光束攻击
            if (dist <= beamAttackRange && dist > attackRange && beamCooldownTimer <= 0f && cooldownTimer <= 0f)
            {
                EnterBeamAttack();
                return;
            }

            // 追击：控制 Body 朝向目标方向
            bool shouldFaceRight = playerTarget.position.x > Center.x;
            if (body.movingRight != shouldFaceRight)
            {
                body.Flip();
            }

            // 确保 Body 保持 Run 状态
            if (body.CurrentMovementState != DarkKnightController.MovementState.Running &&
                body.CurrentFightingState == DarkKnightController.FightingState.Idle)
            {
                body.ActivateRun();
            }
        }

        // ══════════════════ 攻击逻辑 ══════════════════

        /// <summary>
        /// 进入预攻击过渡态：
        /// DarkKnightController.ActivateAttack() 要求 FightingState == OnGuard，
        /// 所以需要先调 ActivateGuard()，再在下一帧或延迟后调 ActivateAttack()。
        /// </summary>
        private void EnterPreAttack(bool isMelee)
        {
            currentState = AIState.PreAttack;
            // 先让 Body 进入 Guard（OnGuard 状态），再触发攻击
            body.ActivateIdle(); // 先停下来
            StartCoroutine(MeleeAttackSequence());
        }

        /// <summary>
        /// 近战攻击序列协程：
        /// 1. 进入 Guard 态（让 Body 切到 OnGuard）
        /// 2. 等一帧确保状态生效
        /// 3. 调 ActivateAttack()（Body 内部会播攻击动画并在 30% 后回到 OnGuard）
        /// 4. 延迟 meleeDamageDelay 秒后做伤害检测
        /// 5. 进入冷却状态
        /// </summary>
        private IEnumerator MeleeAttackSequence()
        {
            // 步骤 1：进入 Guard 状态
            body.ActivateGuard();

            // 等一帧确保状态切换生效
            yield return null;

            // 步骤 2：触发攻击（需要 FightingState == OnGuard）
            if (body.CurrentFightingState == DarkKnightController.FightingState.OnGuard)
            {
                currentState = AIState.Attack;
                body.ActivateAttack();

                // 步骤 3：延迟后检测伤害（对应攻击动画出招帧）
                yield return new WaitForSeconds(meleeDamageDelay);
                DealMeleeDamage();
            }
            else
            {
                // Guard 状态未正常进入，直接回到追击
                EnterChase();
                yield break;
            }

            // 攻击后等 Body 完成动画（Body 内部协程在 normalizedTime > 0.3 后把 FightingState 改回 OnGuard）
            // 等待 Body 回到可以接受新指令的状态
            float waitTime = 0f;
            while (body.CurrentFightingState == DarkKnightController.FightingState.Attacking && waitTime < 3f)
            {
                waitTime += Time.deltaTime;
                yield return null;
            }

            // 步骤 4：进入冷却
            EnterCooldown();
        }

        /// <summary>
        /// 近战伤害检测：在攻击动画出招帧附近，用 OverlapCircle 检测前方玩家。
        /// </summary>
        private void DealMeleeDamage()
        {
            // 检测攻击前方的玩家
            Vector2 attackOrigin = Center + (Vector2)(transform.right * transform.localScale.x * (attackRange * 0.5f));
            Collider2D hit = Physics2D.OverlapCircle(attackOrigin, attackRange, playerLayer);

            if (hit != null)
            {
                PlayerStats ps = hit.GetComponent<PlayerStats>();
                if (ps != null)
                {
                    ps.TakeDamage(attackDamage);
                    Debug.Log($"[DarkKnightAI] 近战攻击命中 {hit.gameObject.name}，造成 {attackDamage} 伤害。");
                }
            }
        }

        /// <summary>
        /// 触发光束攻击。
        /// ActivateBeamAttack() 内部会自动退出 Guard，无需预处理。
        /// </summary>
        private void EnterBeamAttack()
        {
            currentState = AIState.Attack;
            body.ActivateBeamAttack();
            StartCoroutine(WaitForBeamFinish());
        }

        private IEnumerator WaitForBeamFinish()
        {
            // 等待 Body 进入 BeamAttack 动画状态
            yield return new WaitForSeconds(0.1f); // 等一小会让触发器生效

            // 等待攻击态结束（BeamShootRoutine 结束后 FightingState 不会自动归 Idle，
            // Busy=false 后 Animator 回到 Idle，FightingState 还是 Attacking 直到下次调用）
            // 保守等待光束动画时长（约 3.5 秒）
            yield return new WaitForSeconds(3.5f);

            // 重置 Body 到 Idle 让后续指令可以生效
            body.ActivateIdle();

            // 设置远程冷却
            beamCooldownTimer = beamCooldown;

            // 进入冷却
            EnterCooldown();
        }

        /// <summary>等待 Body 完成攻击动画（用于非协程场景检查）。</summary>
        private void WaitForAttackFinish()
        {
            // MeleeAttackSequence 协程会自己转换状态，这里是保险检测
            // 如果 Body 因某种原因卡住，超时后强制回到追击
        }

        // ══════════════════ 冷却逻辑 ══════════════════

        /// <summary>进入冷却状态：随机进行守卫或后撤步动作，丰富表现。</summary>
        private void EnterCooldown()
        {
            currentState  = AIState.Cooldown;
            cooldownTimer = attackCooldown;

            // 进入 Guard 状态作为冷却期间的默认姿势
            if (body.CurrentFightingState != DarkKnightController.FightingState.OnGuard)
            {
                body.ActivateGuard();
            }

            // 随机在冷却中段执行后撤步
            StartCoroutine(CooldownBehavior());
        }

        private IEnumerator CooldownBehavior()
        {
            // 等待冷却时间的一半
            yield return new WaitForSeconds(attackCooldown * 0.4f);

            if (currentState != AIState.Cooldown) yield break;

            // 50% 概率执行后撤步（需要 Body 处于 OnGuard 才能触发）
            float roll = Random.value;
            if (roll < 0.5f && body.CurrentFightingState == DarkKnightController.FightingState.OnGuard)
            {
                body.ActivateBackStep();
                Debug.Log("[DarkKnightAI] 冷却期间执行后撤步。");
            }

            // 等待剩余冷却时间
            yield return new WaitForSeconds(attackCooldown * 0.6f);

            if (currentState != AIState.Cooldown) yield break;

            // 冷却结束，退出 Guard，决定下一步
            if (body.CurrentFightingState == DarkKnightController.FightingState.OnGuard)
            {
                body.ActivateGuard(); // 再次调用 ActivateGuard() 会切换回 Idle（它是 Toggle）
            }

            TransitionFromCooldown();
        }

        /// <summary>冷却结束后，根据玩家距离决定下一状态。</summary>
        private void TransitionFromCooldown()
        {
            if (playerTarget != null)
            {
                float dist = Vector2.Distance(Center, playerTarget.position);
                if (dist <= loseChasingDistance)
                {
                    EnterChase(); // 玩家仍在范围内 → 继续追击
                    return;
                }
            }
            // 玩家丢失或超距 → 返回巡逻
            playerTarget = null;
            EnterPatrol();
        }

        // ══════════════════ 受击与死亡接口 ══════════════════

        /// <summary>
        /// 受到伤害接口（与框架中其他敌人保持一致的签名）。
        /// 由玩家攻击检测组件调用。
        /// </summary>
        public void TakeDamage(float damage)
        {
            // 死亡状态忽略
            if (isDead) return;

            currentHP -= damage;
            Debug.Log($"[DarkKnightAI] 受到 {damage:F0} 伤害，剩余 HP：{currentHP:F0}");

            if (currentHP <= 0f)
            {
                Die();
                return;
            }

            // 受伤动画（Body 内部协程会在动画结束后把 FightingState 设回 OnGuard）
            body.ActivateHurt();

            // 受伤后如果不在冷却/攻击状态，强制进入追击（被打了要反击）
            if (currentState == AIState.Patrol || currentState == AIState.Chase)
            {
                // 尝试索敌，如果没有目标则维持现状
                if (playerTarget == null)
                    TryDetectPlayer();
            }
        }

        /// <summary>死亡处理：触发 Body 死亡动画、禁用物理、掉落奖励、延迟销毁。</summary>
        private void Die()
        {
            if (isDead) return;
            isDead       = true;
            currentState = AIState.Dead;

            Debug.Log($"[DarkKnightAI] {gameObject.name} 已死亡！");

            // ① 触发 Body 的死亡效果（溶解材质 + 音效 + 爆炸特效）
            body.ActivateDeath();

            // ② 禁用所有 Collider2D，防止鞭尸
            Collider2D[] allCols = GetComponents<Collider2D>();
            foreach (var c in allCols)
                c.enabled = false;

            // ③ 主刚体设为 Kinematic
            Rigidbody2D rb = GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                rb.bodyType = RigidbodyType2D.Kinematic;
            }

            // ④ 经验结算
            PlayerStats stats = FindAnyObjectByType<PlayerStats>();
            if (stats != null)
                stats.OnEnemyKilled(expReward);

            // ⑤ 概率掉落血瓶
            if (healthPotionPrefab != null && Random.value <= dropPotionChance)
            {
                Instantiate(healthPotionPrefab, transform.position, Quaternion.identity);
                Debug.Log($"[DarkKnightAI] {gameObject.name} 掉落了血瓶！");
            }

            // ⑥ 延迟销毁（等待溶解动画播完）
            StartCoroutine(DestroyAfterDelay(deathDestroyDelay));
        }

        private IEnumerator DestroyAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            Destroy(gameObject);
        }

        // ══════════════════ Gizmos 调试可视化 ══════════════════

        private void OnDrawGizmosSelected()
        {
            Vector3 center = Application.isPlaying ? (Vector3)(Vector2)Center : transform.position;

            // 巡逻范围（黄色横线）
            Gizmos.color = Color.yellow;
            float left  = Application.isPlaying ? patrolLeftX  : center.x - patrolLeftOffset;
            float right = Application.isPlaying ? patrolRightX : center.x + patrolRightOffset;
            Gizmos.DrawLine(new Vector3(left, center.y), new Vector3(right, center.y));

            // 索敌范围（红色圆）
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(center, detectRadius);

            // 近战攻击范围（洋红色圆）
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(center, attackRange);

            // 远程攻击范围（青色圆）
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(center, beamAttackRange);

            // 丢失追击范围（白色圆）
            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(center, loseChasingDistance);
        }
    }
}