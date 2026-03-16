using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 炮台：不可移动，生成在第0~2列。
/// 每回合炮台阶段向距离最近的敌人发射子弹，命中扣 1 HP。
/// 被敌人踩到则爆炸销毁。
/// </summary>
public class Turret : GridEntity
{
    // ────────────────── 射击配置 ──────────────────
    [Header("=== 射击表现 ===")]
    [Tooltip("炮弹 Prefab（挂载 TurretProjectile 脚本的空物体，可为空则自动创建）")]
    [SerializeField] private GameObject projectilePrefab;
    [Tooltip("炮弹飞行速度")]
    [SerializeField] private float projectileSpeed = 5f;
    [Tooltip("射击时炮台的缩放反馈幅度")]
    [SerializeField] private float recoilScale = 0.1f;
    [SerializeField] private float recoilDuration = 0.15f;

    // ────────────────── 内部 ──────────────────
    private SpriteRenderer spriteRenderer;

    // ══════════════════ 生命周期 ══════════════════

    protected override void Awake()
    {
        base.Awake();
        spriteRenderer = GetComponent<SpriteRenderer>();
        enableBreathing = false; // 炮台不呼吸
    }

    protected override void Start()
    {
        base.Start();

        if (TurnManager.Instance != null)
            TurnManager.Instance.RegisterTurret(this);
    }

    // ══════════════════ 回合行动 ══════════════════

    /// <summary>
    /// 由 TurnManager 在炮台阶段调用：瞄准最近的敌人射击
    /// </summary>
    public void DoTurn()
    {
        PaperEnemy target = FindClosestEnemy();
        if (target == null) return;

        Debug.Log($"炮台 {gameObject.name} 向 {target.gameObject.name} 开火！");

        // 生成炮弹（伤害由炮弹到达后执行）
        SpawnProjectile(target);

        // 后坐力视觉反馈
        StartCoroutine(RecoilVisual());
    }

    /// <summary>
    /// 查找曼哈顿距离最近的敌人
    /// </summary>
    PaperEnemy FindClosestEnemy()
    {
        if (TurnManager.Instance == null) return null;

        List<PaperEnemy> enemies = TurnManager.Instance.GetEnemies();
        PaperEnemy closest = null;
        int closestDist = int.MaxValue;

        foreach (var enemy in enemies)
        {
            if (enemy == null || enemy.IsDead) continue;
            int dist = Mathf.Abs(enemy.GridPosition.x - GridPosition.x)
                     + Mathf.Abs(enemy.GridPosition.y - GridPosition.y);
            if (dist < closestDist)
            {
                closestDist = dist;
                closest = enemy;
            }
        }

        return closest;
    }

    // ══════════════════ 炮弹生成 ══════════════════

    void SpawnProjectile(PaperEnemy target)
    {
        GameObject projObj;

        if (projectilePrefab != null)
        {
            projObj = Instantiate(projectilePrefab, transform.position, Quaternion.identity);
        }
        else
        {
            // 没有 Prefab 时自动创建一个空物体挂 TurretProjectile
            projObj = new GameObject("TurretProjectile_Auto");
            projObj.transform.position = transform.position;
            projObj.AddComponent<TurretProjectile>();
        }

        TurretProjectile proj = projObj.GetComponent<TurretProjectile>();
        if (proj == null)
            proj = projObj.AddComponent<TurretProjectile>();

        proj.Init(target, projectileSpeed);
    }

    // ══════════════════ 后坐力视觉 ══════════════════

    System.Collections.IEnumerator RecoilVisual()
    {
        Vector3 original = baseScale;
        transform.localScale = baseScale - Vector3.one * recoilScale;
        yield return new WaitForSeconds(recoilDuration);
        transform.localScale = original;
    }

    // ══════════════════ 被碾压销毁 ══════════════════

    /// <summary>
    /// 被敌人踩到时由 TurnManager 调用
    /// </summary>
    public void OnDestroyed()
    {
        Debug.Log($"炮台 {gameObject.name} 爆炸！");

        // TODO: 爆炸特效
        if (spriteRenderer != null)
            spriteRenderer.color = Color.clear;

        Destroy(gameObject);
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        if (TurnManager.Instance != null)
            TurnManager.Instance.UnregisterTurret(this);
    }

    // ══════════════════ Gizmos ══════════════════

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, 0.3f);
    }
}
