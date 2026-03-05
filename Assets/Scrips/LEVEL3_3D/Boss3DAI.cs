using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Phase 3 巨型石头人 Boss AI。
/// 固定在场景 Z 轴极值处不移动，按时间间隔随机释放三种"赛道灾难"攻击。
/// 所有数值全部 [SerializeField] 暴露到面板。
/// </summary>
public class Boss3DAI : MonoBehaviour
{
    // ────────────────── 攻击模式引用 ──────────────────
    [Header("=== 攻击模式脚本引用 ===")]
    [SerializeField] private BossAttack_Projectiles projectilesAttack;
    [SerializeField] private BossAttack_SandPits sandPitsAttack;
    [SerializeField] private BossAttack_Sandstorm sandstormAttack;

    // ────────────────── 攻击节奏 ──────────────────
    [Header("=== 攻击节奏 ===")]
    [Tooltip("两次攻击之间的最小间隔")]
    [SerializeField] private float minAttackInterval = 2f;
    [Tooltip("两次攻击之间的最大间隔")]
    [SerializeField] private float maxAttackInterval = 4f;
    [Tooltip("Boss 低血量时攻击间隔缩短的比例")]
    [SerializeField] private float enrageSpeedMultiplier = 0.5f;
    [Tooltip("低于此血量比进入狂暴")]
    [SerializeField] private float enrageHPThreshold = 0.3f;

    // ────────────────── 玩家引用 ──────────────────
    [Header("=== 玩家引用 ===")]
    [SerializeField] private Transform playerTransform;
    public Transform PlayerTransform => playerTransform;

    // ────────────────── 视觉反馈 ──────────────────
    [Header("=== 受击视觉 ===")]
    [Tooltip("受击闪白持续时间")]
    [SerializeField] private float hitFlashDuration = 0.08f;
    private Renderer bossRenderer;
    private Color originalColor;
    private MaterialPropertyBlock mpb;

    // ────────────────── 内部 ──────────────────
    private bool isFighting;
    private Coroutine attackLoop;

    // ══════════════════ 生命周期 ══════════════════

    void Start()
    {
        bossRenderer = GetComponentInChildren<Renderer>();
        mpb = new MaterialPropertyBlock();

        if (bossRenderer != null)
        {
            bossRenderer.GetPropertyBlock(mpb);
            if (mpb.HasColor("_BaseColor"))
                originalColor = mpb.GetColor("_BaseColor");
            else
                originalColor = Color.white;
        }

        // 自动查找玩家
        if (playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) playerTransform = player.transform;
        }

        // 订阅 BossSceneManager 的回合事件
        if (BossSceneManager.Instance != null)
        {
            StartCoroutine(WaitForFightPhase());
        }
    }

    void OnDestroy()
    {
        if (attackLoop != null)
            StopCoroutine(attackLoop);
    }

    // ══════════════════ 等待战斗开始 ══════════════════

    IEnumerator WaitForFightPhase()
    {
        // 等到 BossSceneManager 切换到 Fighting
        while (BossSceneManager.Instance == null ||
               BossSceneManager.Instance.CurrentPhase != BossSceneManager.BossPhase.Fighting)
        {
            yield return null;
        }

        isFighting = true;
        attackLoop = StartCoroutine(AttackLoop());
    }

    // ══════════════════ 攻击循环 ══════════════════

    IEnumerator AttackLoop()
    {
        // 初始延迟，让玩家准备一下
        yield return new WaitForSeconds(1.5f);

        while (isFighting)
        {
            // 检查 Boss 是否已死
            if (BossSceneManager.Instance != null &&
                BossSceneManager.Instance.CurrentPhase == BossSceneManager.BossPhase.End)
            {
                isFighting = false;
                yield break;
            }

            // 随机选择攻击
            int attackIndex = Random.Range(0, 3);
            yield return StartCoroutine(ExecuteAttack(attackIndex));

            // 计算间隔（狂暴时加速）
            float interval = Random.Range(minAttackInterval, maxAttackInterval);
            if (BossSceneManager.Instance != null &&
                BossSceneManager.Instance.BossHPRatio < enrageHPThreshold)
            {
                interval *= enrageSpeedMultiplier;
            }

            yield return new WaitForSeconds(interval);
        }
    }

    IEnumerator ExecuteAttack(int index)
    {
        switch (index)
        {
            case 0:
                if (projectilesAttack != null)
                    yield return StartCoroutine(projectilesAttack.Execute(this));
                break;
            case 1:
                if (sandPitsAttack != null)
                    yield return StartCoroutine(sandPitsAttack.Execute(this));
                break;
            case 2:
                if (sandstormAttack != null)
                    yield return StartCoroutine(sandstormAttack.Execute(this));
                break;
        }
    }

    // ══════════════════ 受击反馈 ══════════════════

    /// <summary>
    /// 被玩家子弹击中时的视觉反馈（由外部或 Projectile3D 间接触发）。
    /// </summary>
    public void OnHitVisual()
    {
        if (bossRenderer != null)
            StartCoroutine(HitFlash());
    }

    IEnumerator HitFlash()
    {
        if (bossRenderer == null) yield break;

        bossRenderer.GetPropertyBlock(mpb);
        mpb.SetColor("_BaseColor", Color.white);
        bossRenderer.SetPropertyBlock(mpb);

        yield return new WaitForSeconds(hitFlashDuration);

        mpb.SetColor("_BaseColor", originalColor);
        bossRenderer.SetPropertyBlock(mpb);
    }

    // ══════════════════ 工具 ══════════════════

    /// <summary>
    /// 获取玩家当前世界坐标（供攻击脚本使用）
    /// </summary>
    public Vector3 GetPlayerPosition()
    {
        return playerTransform != null ? playerTransform.position : Vector3.zero;
    }

    /// <summary>
    /// 获取 Boss 自身位置
    /// </summary>
    public Vector3 GetBossPosition()
    {
        return transform.position;
    }
}
