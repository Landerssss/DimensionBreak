using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 回合状态机：严格按顺序 —— 等待玩家输入 → 敌人移动 → 炮台射击 → 结算 → 循环。
/// </summary>
public class TurnManager : MonoBehaviour
{
    public static TurnManager Instance { get; private set; }

    // ────────────────── 回合阶段 ──────────────────
    public enum TurnPhase
    {
        WaitingForPlayer,
        EnemyMove,
        TurretFire,
        Resolve,
    }

    [Header("=== 回合节奏 ===")]
    [Tooltip("每个阶段之间的等待时间（秒）")]
    [SerializeField] private float phaseDelay = 0.35f;

    // ────────────────── 实体注册表 ──────────────────
    // 由各实体在 Start/OnDestroy 时自行注册/注销
    [HideInInspector] public PaperPlayer player;
    private readonly List<PaperEnemy> enemies = new List<PaperEnemy>();
    private readonly List<Turret> turrets = new List<Turret>();

    // ────────────────── 状态 ──────────────────
    public TurnPhase CurrentPhase { get; private set; } = TurnPhase.WaitingForPlayer;
    public bool IsBusy => CurrentPhase != TurnPhase.WaitingForPlayer;

    // ────────────────── 事件 ──────────────────
    public event System.Action OnTurnStart;
    public event System.Action OnTurnEnd;

    // ══════════════════ 生命周期 ══════════════════

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    // ══════════════════ 注册 / 注销 ══════════════════

    public void RegisterEnemy(PaperEnemy e)  { if (!enemies.Contains(e)) enemies.Add(e); }
    public void UnregisterEnemy(PaperEnemy e) { enemies.Remove(e); }
    public void RegisterTurret(Turret t)      { if (!turrets.Contains(t)) turrets.Add(t); }
    public void UnregisterTurret(Turret t)    { turrets.Remove(t); }

    public List<PaperEnemy> GetEnemies() => enemies;
    public List<Turret> GetTurrets() => turrets;

    // ══════════════════ 回合推进 ══════════════════

    /// <summary>
    /// 由 PaperPlayer 在完成移动后调用，启动后续回合流程。
    /// </summary>
    public void PlayerFinishedMove()
    {
        if (CurrentPhase != TurnPhase.WaitingForPlayer) return;
        StartCoroutine(RunTurnSequence());
    }

    IEnumerator RunTurnSequence()
    {
        OnTurnStart?.Invoke();

        // ─── 阶段 1：敌人移动 ───
        CurrentPhase = TurnPhase.EnemyMove;
        yield return new WaitForSeconds(phaseDelay);

        // 拷贝列表防止遍历时修改
        var enemySnapshot = new List<PaperEnemy>(enemies);
        foreach (var enemy in enemySnapshot)
        {
            if (enemy != null)
                enemy.DoTurn();
        }
        // 等待所有敌人移动动画完成
        yield return WaitForAllAnimations(enemySnapshot);

        // ─── 中途结算：敌人与炮台碰撞 ───
        ResolveEnemyTurretCollisions();

        // ─── 阶段 2：炮台射击 ───
        CurrentPhase = TurnPhase.TurretFire;
        yield return new WaitForSeconds(phaseDelay);

        var turretSnapshot = new List<Turret>(turrets);
        foreach (var turret in turretSnapshot)
        {
            if (turret != null)
                turret.DoTurn();
        }
        yield return new WaitForSeconds(phaseDelay);

        // ─── 阶段 3：结算 ───
        CurrentPhase = TurnPhase.Resolve;
        yield return new WaitForSeconds(phaseDelay);

        ResolveDeaths();
        CheckWinLose();

        // ─── 回到等待玩家输入 ───
        CurrentPhase = TurnPhase.WaitingForPlayer;
        OnTurnEnd?.Invoke();
    }

    // ══════════════════ 结算逻辑 ══════════════════

    /// <summary>
    /// 检查是否有敌人和炮台在同一格 → 炮台爆炸销毁
    /// </summary>
    void ResolveEnemyTurretCollisions()
    {
        var turretSnapshot = new List<Turret>(turrets);
        var enemySnapshot = new List<PaperEnemy>(enemies);

        foreach (var turret in turretSnapshot)
        {
            if (turret == null) continue;
            foreach (var enemy in enemySnapshot)
            {
                if (enemy == null) continue;
                if (turret.GridPosition == enemy.GridPosition)
                {
                    Debug.Log($"炮台 {turret.name} 被敌人 {enemy.name} 碾压爆炸！");
                    turret.OnDestroyed();
                    break;
                }
            }
        }
    }

    /// <summary>
    /// 清理 HP ≤ 0 的敌人
    /// </summary>
    void ResolveDeaths()
    {
        var snapshot = new List<PaperEnemy>(enemies);
        foreach (var enemy in snapshot)
        {
            if (enemy != null && enemy.IsDead)
            {
                enemy.OnDeathResolve();
            }
        }
    }

    /// <summary>
    /// 检查胜负条件
    /// </summary>
    void CheckWinLose()
    {
        // 败：玩家死亡（在 PaperPlayer 中自行检测）
        // 胜：玩家到达出口列（在 PaperPlayer 移动时检测）
        // 此处也可以做二次校验
        if (player == null) return;

        // 检查玩家是否和敌人重叠
        foreach (var enemy in enemies)
        {
            if (enemy != null && enemy.GridPosition == player.GridPosition)
            {
                Debug.Log("玩家与敌人重叠 —— 玩家死亡！");
                player.OnPlayerDeath();
                return;
            }
        }
    }

    // ══════════════════ 工具 ══════════════════

    IEnumerator WaitForAllAnimations(List<PaperEnemy> list)
    {
        bool anyMoving = true;
        while (anyMoving)
        {
            anyMoving = false;
            foreach (var e in list)
            {
                if (e != null && e.IsAnimating)
                {
                    anyMoving = true;
                    break;
                }
            }
            yield return null;
        }
    }
}
