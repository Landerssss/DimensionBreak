using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace TealFalconEnemySeries
{
    public class DarkKnightController : MonoBehaviour
    {
        // ────────────────── 移动 ──────────────────
        public float currentSpeed   = 0f;
        public float animationSpeed = 2f;
        public float acceleration   = 4.8f;
        public float MaxWalkSpeed   = 2f;
        public float MaxRunSpeed    = 7f;
        public float BackStepPower  = 200f;
        public bool  movingRight    = true;

        // ────────────────── 组件 ──────────────────
        private Animator            _animator        = null;
        private Rigidbody2D         _rigidBody       = null;
        public  Material            _matRef          = null;
        private Material            instanceMaterial = null;
        private Vector3             deathPlace;
        private MaterialPropertyBlock mpb;

        // ────────────────── AI ──────────────────
        [Header("=== AI Settings ===")]
        public bool      isAIEnabled    = true;
        public Transform playerTransform;
        public Vector2   patrolCenter;          // 活动中心，Start() 自动赋值
        public float     moveRange      = 20f;  // Boss 最大活动半径
        public float     detectionRange = 8f;   // 开始追击的视野距离
        public float     attackRange    = 2.5f; // 开始攻击的距离
        public float     attackCooldown = 2f;
        private float    lastAttackTime = -999f;

        // ────────────────── 生命值 ──────────────────
        [Header("=== 生命值 ===")]
        public float maxHP      = 300f;
        public float expReward  = 1000f;
        private float currentHP;

        // ────────────────── 掉落物品 ──────────────────
        [Header("=== 掉落物品 ===")]
        public GameObject healthPotionPrefab;
        [Range(0f, 1f)]
        public float dropPotionChance = 0.5f;

        // ────────────────── 其他配置 ──────────────────
        public bool block = false;

        public enum MovementState { Idle, Walking, Running }
        public enum FightingState { OnGuard, Attacking, Hurt, Death, Move, Idle }

        public MovementState CurrentMovementState = MovementState.Idle;
        public FightingState CurrentFightingState = FightingState.Idle;

        public UnityEvent OnHurt, OnDeath, OnCharged;

        // ────────────────── 颜色 / 死亡溶解 ──────────────────
        public Color GlowColor;
        public Color DissolveColor;
        public GameObject ExplosionEffect;
        public Transform ExplosionRef;
        public float DissolveSpeed  = 1f;
        private float DissolveStatus = 1f;
        public bool  destroy         = false;
        public float power           = 10f;

        // ────────────────── Beam 攻击 ──────────────────
        private Transform BeamAttackRef = null;
        public  GameObject DarkBall;

        // ────────────────── 音效 ──────────────────
        public List<SpriteRenderer> SRList = null;
        public List<Rigidbody2D>    RBList = null;
        public AudioSource  _Channel = null;
        public AudioClip    BeamSound, DeathExplosionSound, FootStepSound, PainSound, PowerLoadSound, SwordSound;

        private float stepTimer = 0f;
        public  float baseStepSpeed = 3f;
        public  float minStepSpeed  = 0.1f;

        // ────────────────── 内部状态 ──────────────────
        // 防止 ActivateGuard 在同帧被反复调用
        private bool _guardRequested = false;

        // ══════════════════ 生命周期 ══════════════════

        void Awake()  { SetComponents(); }
        void OnEnable(){ SetComponents(); }

        void Start()
        {
            currentHP    = maxHP;
            patrolCenter = transform.position;

            if (playerTransform == null)
            {
                GameObject p = GameObject.FindGameObjectWithTag("Player");
                if (p != null) playerTransform = p.transform;
            }
        }

        void SetComponents()
        {
            if (_animator  == null)
                _animator  = transform.Find("Root").GetComponent<Animator>();
            if (_rigidBody == null)
                _rigidBody = GetComponent<Rigidbody2D>();
            if (_matRef    == null)
                Debug.LogWarning("NO Material Ref Setted!!!");
            if (SRList == null || SRList.Count == 0)
                SRList = GetAllSpriteRenderersInChildren(transform);
            if (RBList == null || RBList.Count == 0)
                RBList = GetAllRigidbodiesInChildren(transform);
            if (BeamAttackRef == null)
                BeamAttackRef = transform.Find("Root/Head_Pivot/BeamAttackRef");

            if (IsBuiltIn(_matRef)) return;

            mpb = new MaterialPropertyBlock();
            if (_matRef.GetTexture("_MainTex") != null)
                mpb.SetTexture("_MainTex", _matRef.GetTexture("_MainTex"));
            mpb.SetTexture("_MagentaPNG",   _matRef.GetTexture("_MagentaPNG"));
            mpb.SetTexture("_NormalMap",    _matRef.GetTexture("_NormalMap"));
            mpb.SetTexture("_Emission",     _matRef.GetTexture("_Emission"));
            mpb.SetFloat("_DissolveScale",  _matRef.GetFloat("_DissolveScale"));
            mpb.SetColor("_Glow",           GlowColor);
            mpb.SetColor("_DissolveColor",  DissolveColor);
            ApplyChanges();
        }

        void ApplyChanges()
        {
            if (IsBuiltIn(_matRef)) return;
            foreach (SpriteRenderer sr in SRList)
                sr.SetPropertyBlock(mpb);
        }

        void Update()
        {
            // ── 死亡溶解 ──
            if (CurrentFightingState == FightingState.Death)
            {
                DissolveStatus = Mathf.MoveTowards(DissolveStatus, 0f, DissolveSpeed * Time.deltaTime);
                if (!IsBuiltIn(_matRef))
                    mpb.SetFloat("_Dissolve", DissolveStatus);
                ApplyChanges();
                if (DissolveStatus == 0 && destroy) Destroy(gameObject);
                return;
            }

            // ── AI 决策 ──
            if (isAIEnabled) UpdateAI();

            // ── 非移动/Idle 状态：速度归零，不处理移动 ──
            if (CurrentFightingState != FightingState.Idle &&
                CurrentFightingState != FightingState.Move  &&
                CurrentFightingState != FightingState.OnGuard)
            {
                currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, acceleration * Time.deltaTime);
                _rigidBody.linearVelocity = new Vector2(currentSpeed, _rigidBody.linearVelocity.y);
                return;
            }

            // ── 正常移动逻辑 ──
            float targetSpeed = 0f;
            switch (CurrentMovementState)
            {
                case MovementState.Walking: targetSpeed = MaxWalkSpeed; break;
                case MovementState.Running: targetSpeed = MaxRunSpeed;  break;
            }

            currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed * transform.localScale.x, acceleration * Time.deltaTime);
            _animator.SetFloat("Speed", Mathf.Abs(currentSpeed) / animationSpeed);
            _rigidBody.linearVelocity = new Vector2(currentSpeed, _rigidBody.linearVelocity.y);

            // 脚步声
            float stepSpeed = Mathf.Lerp(baseStepSpeed, minStepSpeed, Mathf.Abs(currentSpeed) / MaxRunSpeed);
            stepTimer += Time.deltaTime;
            if (stepTimer >= stepSpeed && Mathf.Abs(currentSpeed) > 0.1f)
            {
                PlaySound(FootStepSound);
                stepTimer = 0f;
            }
        }

        // ══════════════════ AI ══════════════════

        void UpdateAI()
        {
            // 只有 Idle 或 OnGuard 状态才允许 AI 做决策
            if (CurrentFightingState != FightingState.Idle &&
                CurrentFightingState != FightingState.OnGuard)
                return;

            if (playerTransform == null)
            {
                GameObject p = GameObject.FindGameObjectWithTag("Player");
                if (p != null) playerTransform = p.transform;
                if (playerTransform == null) return;
            }

            float distToPlayer  = Vector2.Distance(transform.position, playerTransform.position);
            float distToCenter  = Vector2.Distance(transform.position, patrolCenter);
            float playerDistToCenter = Vector2.Distance(playerTransform.position, patrolCenter);

            // 玩家在视野内 且 双方都在活动范围内
            bool canSeePlayer = distToPlayer      <= detectionRange
                             && playerDistToCenter <= moveRange
                             && distToCenter       <= moveRange;

            if (canSeePlayer)
            {
                if (distToPlayer <= attackRange)
                {
                    // ── 进入攻击范围：停下、面朝玩家 ──
                    SetIdle_NoGuardToggle();
                    FaceTarget(playerTransform.position);

                    if (Time.time - lastAttackTime > attackCooldown)
                    {
                        lastAttackTime = Time.time;
                        StartCoroutine(AIAttackSequence());
                    }
                    else
                    {
                        // 冷却中：进入防御（只在尚未防御时切换，避免抖动）
                        SetGuardOn();
                    }
                }
                else
                {
                    // ── 追击：先取消防御再跑 ──
                    SetGuardOff();
                    FaceTarget(playerTransform.position);
                    ActivateRun();
                }
            }
            else
            {
                // ── 玩家不在视野内：取消防御，回到中心点 ──
                SetGuardOff();

                if (distToCenter > 1f)
                {
                    FaceTarget(patrolCenter);
                    ActivateWalk();
                }
                else
                {
                    ActivateIdle();
                }
            }
        }

        /// <summary>设置 Idle 状态，但不触发 Guard 开关（防止抖动）</summary>
        void SetIdle_NoGuardToggle()
        {
            CurrentMovementState = MovementState.Idle;
            if (CurrentFightingState != FightingState.OnGuard)
                CurrentFightingState = FightingState.Idle;
        }

        /// <summary>强制进入 OnGuard（只在当前不是 OnGuard 时才切换）</summary>
        void SetGuardOn()
        {
            if (CurrentFightingState == FightingState.OnGuard) return;
            CurrentMovementState = MovementState.Idle;
            CurrentFightingState = FightingState.OnGuard;
            _animator.SetBool("Guard", true);
        }

        /// <summary>强制退出 OnGuard（只在当前是 OnGuard 时才切换）</summary>
        void SetGuardOff()
        {
            if (CurrentFightingState != FightingState.OnGuard) return;
            CurrentMovementState = MovementState.Idle;
            CurrentFightingState = FightingState.Idle;
            _animator.SetBool("Guard", false);
        }

        void FaceTarget(Vector3 targetPos)
        {
            float dir = targetPos.x - transform.position.x;
            if      (dir >  0.1f && transform.localScale.x < 0) Flip();
            else if (dir < -0.1f && transform.localScale.x > 0) Flip();
        }

        IEnumerator AIAttackSequence()
        {
            currentSpeed = 0f;
            _rigidBody.linearVelocity = new Vector2(0, _rigidBody.linearVelocity.y);

            bool useBeam = Random.value > 0.7f;

            if (useBeam)
            {
                SetGuardOff();
                ActivateBeamAttack();
            }
            else
            {
                SetGuardOn();
                yield return new WaitForSeconds(0.1f); // 等状态机稳定
                ActivateAttack();
            }
        }

        // ══════════════════ 受击 / 死亡（新增 TakeDamage 入口）══════════════════

        /// <summary>
        /// 供玩家攻击脚本调用的受伤入口（对应 EnemyAI.TakeDamage）
        /// </summary>
        public void TakeDamage(float damage)
        {
            if (CurrentFightingState == FightingState.Death) return;

            currentHP -= damage;
            Debug.Log($"{gameObject.name} 受到 {damage:F0} 伤害，剩余 HP: {currentHP:F0}");

            if (currentHP <= 0f)
                Die();
            else
                ActivateHurt();
        }

        void Die()
        {
            if (CurrentFightingState == FightingState.Death) return;

            Debug.Log($"{gameObject.name} 被击杀！掉落经验 {expReward}");

            // 通知玩家获得经验
            PlayerStats stats = FindAnyObjectByType<PlayerStats>();
            if (stats != null) stats.OnEnemyKilled(expReward);

            // 概率掉落血瓶
            if (healthPotionPrefab != null && Random.value <= dropPotionChance)
                Instantiate(healthPotionPrefab, transform.position, Quaternion.identity);

            // 如果被冲刺击杀，重置玩家冲刺 CD
            PlayerController pc = FindAnyObjectByType<PlayerController>();
            if (pc != null && pc.IsDashing) pc.InstantResetDash();

            ActivateDeath();
        }

        // ══════════════════ 原有公开方法（保持不变）══════════════════

        public void ActivateRun()
        {
            if (CurrentFightingState != FightingState.Idle) return;
            CurrentMovementState = MovementState.Running;
            _animator.SetBool("Busy", false);
        }

        public void ActivateIdle()
        {
            CurrentMovementState = MovementState.Idle;
            CurrentFightingState = FightingState.Idle;
        }

        public void ActivateWalk()
        {
            if (CurrentFightingState != FightingState.Idle) return;
            CurrentMovementState = MovementState.Walking;
            _animator.SetBool("Busy", false);
        }

        /// <summary>
        /// 原版 ActivateGuard 保留（供外部/编辑器手动调用），
        /// AI 内部统一使用 SetGuardOn / SetGuardOff 避免抖动。
        /// </summary>
        public void ActivateGuard()
        {
            if (CurrentFightingState == FightingState.OnGuard)
            {
                CurrentMovementState = MovementState.Idle;
                CurrentFightingState = FightingState.Idle;
                _animator.SetBool("Guard", false);
            }
            else
            {
                CurrentMovementState = MovementState.Idle;
                CurrentFightingState = FightingState.OnGuard;
                _animator.SetBool("Guard", true);
            }
        }

        public void ActivateBackStep()
        {
            if (CurrentFightingState != FightingState.OnGuard) return;
            _rigidBody.AddForce(transform.localScale.x * Vector2.left * BackStepPower, ForceMode2D.Impulse);
            _animator.SetTrigger("BackStep");
        }

        public void ActivateAttack()
        {
            if (CurrentFightingState != FightingState.OnGuard) return;
            CurrentMovementState = MovementState.Idle;
            CurrentFightingState = FightingState.Attacking;
            _rigidBody.AddForce(transform.localScale.x * Vector2.right * BackStepPower, ForceMode2D.Impulse);
            currentSpeed = 0f;
            StartCoroutine(AttackRoutine());
        }

        public void ActivateBeamAttack()
        {
            if (CurrentFightingState == FightingState.OnGuard) SetGuardOff();
            CurrentMovementState = MovementState.Idle;
            CurrentFightingState = FightingState.Attacking;
            _animator.SetBool("Busy", true);
            currentSpeed = 0f;
            _animator.SetTrigger("BeamAttack");
            StartCoroutine(BeamShootRoutine());
        }

        public void ActivateHurt()
        {
            CurrentMovementState = MovementState.Idle;
            CurrentFightingState = FightingState.Hurt;
            StartCoroutine(OnHurtRoutine());
            OnHurt.Invoke();
            PlaySound(PainSound);
        }

        public void ActivateDeath()
        {
            CurrentFightingState = FightingState.Death;
            _animator.enabled    = false;
            deathPlace           = transform.position;
            OnDeath.Invoke();
            PlaySound(DeathExplosionSound);
        }

        public void Explode()
        {
            if (ExplosionEffect != null)
                Instantiate(ExplosionEffect, transform.position, Quaternion.identity);

            foreach (Rigidbody2D rb in RBList)
            {
                if (rb == null) continue;
                Vector2 dir = new Vector2(
                    rb.transform.position.x - ExplosionRef.position.x,
                    rb.transform.position.y - ExplosionRef.position.y).normalized;
                rb.gravityScale = 0f;
                rb.AddForce(dir * power, ForceMode2D.Impulse);
            }
        }

        public void ResetState()
        {
            if (DissolveStatus > 0) return;
            _animator.enabled = true;
            DissolveStatus    = 1f;
            instanceMaterial.SetFloat("_Dissolve", DissolveStatus);
            transform.position           = deathPlace;
            _rigidBody.linearVelocity    = Vector2.zero;
            _rigidBody.angularVelocity   = 0f;
            foreach (Rigidbody2D rb in RBList)
            {
                rb.linearVelocity  = Vector2.zero;
                rb.angularVelocity = 0f;
            }
            currentHP            = maxHP;
            CurrentMovementState = MovementState.Idle;
            CurrentFightingState = FightingState.Idle;
        }

        public void Flip()
        {
            Vector3 s = transform.localScale;
            s.x *= -1;
            transform.localScale = s;
            currentSpeed *= -1;
        }

        // ══════════════════ 协程 ══════════════════

        IEnumerator AttackRoutine()
        {
            _animator.SetTrigger("Attack");
            PlaySound(SwordSound);

            while (!_animator.GetCurrentAnimatorStateInfo(0).IsName("Attack"))
                yield return null;

            while (_animator.GetCurrentAnimatorStateInfo(0).normalizedTime < 0.3f)
                yield return null;

            CurrentFightingState = FightingState.OnGuard;
        }

        IEnumerator OnHurtRoutine()
        {
            _animator.SetTrigger("Hurt");

            while (!_animator.GetCurrentAnimatorStateInfo(0).IsName("Hurt"))
                yield return null;

            while (_animator.GetCurrentAnimatorStateInfo(0).normalizedTime < 1.0f)
                yield return null;

            _animator.SetBool("Busy", false);
            CurrentFightingState = FightingState.OnGuard;
            CurrentMovementState = MovementState.Idle;
        }

        IEnumerator BeamShootRoutine()
        {
            while (!_animator.GetCurrentAnimatorStateInfo(0).IsName("BeamAttack"))
                yield return null;

            PlaySound(PowerLoadSound);
            yield return new WaitForSeconds(2.4f);
            PlaySound(BeamSound);

            if (DarkBall != null)
            {
                GameObject ball = Instantiate(DarkBall, BeamAttackRef.position, Quaternion.identity);
                ball.transform.localScale = transform.localScale;
            }

            _animator.SetBool("Busy", false);
            // BeamAttack 结束后回到 Idle，让 AI 重新决策
            CurrentFightingState = FightingState.Idle;
        }

        // ══════════════════ 工具 ══════════════════

        public List<Rigidbody2D> GetAllRigidbodiesInChildren(Transform parent)
        {
            var list  = new List<Rigidbody2D>();
            foreach (var rb in parent.GetComponentsInChildren<Rigidbody2D>())
                list.Add(rb);
            return list;
        }

        public List<SpriteRenderer> GetAllSpriteRenderersInChildren(Transform parent)
        {
            var list = new List<SpriteRenderer>();
            foreach (var sr in parent.GetComponentsInChildren<SpriteRenderer>())
                list.Add(sr);
            return list;
        }

        void PlaySound(AudioClip clip)
        {
            if (clip    == null) { Debug.LogWarning("Sound not set.");        return; }
            if (_Channel == null){ Debug.LogWarning("AudioSource not set."); return; }
            _Channel.PlayOneShot(clip);
        }

        public bool IsBuiltIn(Material material)
        {
            return material == null || material.shader.name != "DarkKnight/DarkKnightShader";
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(Application.isPlaying ? (Vector3)patrolCenter : transform.position, moveRange);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, detectionRange);
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(transform.position, attackRange);
        }
    }
}