using UnityEngine;

/// <summary>
/// 纸片敌人：每回合向玩家方向移动1格（曼哈顿距离寻路），不进入出口列(5)。
/// 3 HP，被炮台击中 -1 HP，受击时缩小 + 颜色变暗。HP 归零化为泡沫销毁。
/// </summary>
public class PaperEnemy : GridEntity
{
    // ────────────────── 生命值 ──────────────────
    [Header("=== 生命值 ===")]
    [SerializeField] private int maxHP = 3;
    private int currentHP;
    public bool IsDead => currentHP <= 0;

    // ────────────────── 受击视觉 ──────────────────
    [Header("=== 受击缩小 ===")]
    [Tooltip("每次受击 Scale 缩小的比例")]
    [SerializeField] private float shrinkPerHit = 0.15f;

    [Header("=== 受击变暗（烧焦感） ===")]
    [Tooltip("每次受击颜色变暗的程度 (RGB 各减)")]
    [SerializeField] private float darkenPerHit = 0.2f;

    // ────────────────── 禁入列 ──────────────────
    [Header("=== 移动限制 ===")]
    [Tooltip("敌人不会踏入的列号（出口列）")]
    [SerializeField] private int forbiddenColumn = 5;

    // ────────────────── 内部 ──────────────────
    private SpriteRenderer spriteRenderer;
    private int hitsTaken = 0;

    // ══════════════════ 生命周期 ══════════════════

    protected override void Awake()
    {
        base.Awake();
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    protected override void Start()
    {
        base.Start();
        currentHP = maxHP;

        if (TurnManager.Instance != null)
            TurnManager.Instance.RegisterEnemy(this);
    }

    // ══════════════════ 回合行动 ══════════════════

    /// <summary>
    /// 由 TurnManager 在敌人阶段调用：向玩家移动1格
    /// </summary>
    public void DoTurn()
    {
        if (IsDead) return;
        if (TurnManager.Instance == null || TurnManager.Instance.player == null) return;

        Vector2Int playerPos = TurnManager.Instance.player.GridPosition;
        Vector2Int bestMove = ChooseBestMove(playerPos);

        if (bestMove != GridPosition)
            MoveToCell(bestMove);
    }

    /// <summary>
    /// 简单曼哈顿寻路：挑一个更接近玩家的相邻格，且不超出边界 / 不进出口列
    /// </summary>
    Vector2Int ChooseBestMove(Vector2Int target)
    {
        Vector2Int[] directions = {
            Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right
        };

        int currentDist = ManhattanDistance(GridPosition, target);
        Vector2Int best = GridPosition; // 默认原地不动
        int bestDist = currentDist;

        foreach (var d in directions)
        {
            Vector2Int candidate = GridPosition + d;

            // 边界 + 出口列限制
            if (!GridManager.Instance.IsInBounds(candidate)) continue;
            if (candidate.x >= forbiddenColumn) continue;

            // 不能踩到炮台或其他敌人
            GridEntity occupant = GridManager.Instance.GetEntityAt(candidate);
            if (occupant != null && !(occupant is Turret)) continue;
            // 注意：踩到炮台是允许的（会在结算时炸毁炮台）

            int dist = ManhattanDistance(candidate, target);
            if (dist < bestDist)
            {
                bestDist = dist;
                best = candidate;
            }
        }

        return best;
    }

    int ManhattanDistance(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }

    // ══════════════════ 受击 ══════════════════

    /// <summary>
    /// 被炮台子弹击中，扣 1 HP
    /// </summary>
    public void TakeHit()
    {
        if (IsDead) return;

        currentHP--;
        hitsTaken++;
        Debug.Log($"{gameObject.name} 被击中！剩余 HP: {currentHP}");

        // 缩小
        baseScale -= Vector3.one * shrinkPerHit;
        if (baseScale.x < 0.2f) baseScale = Vector3.one * 0.2f; // 最小限制
        transform.localScale = baseScale;

        // 变暗
        if (spriteRenderer != null)
        {
            Color c = spriteRenderer.color;
            c.r = Mathf.Max(0.1f, c.r - darkenPerHit);
            c.g = Mathf.Max(0.1f, c.g - darkenPerHit);
            c.b = Mathf.Max(0.1f, c.b - darkenPerHit);
            spriteRenderer.color = c;
        }
    }

    // ══════════════════ 死亡结算 ══════════════════

    /// <summary>
    /// 由 TurnManager 在结算阶段调用
    /// </summary>
    public void OnDeathResolve()
    {
        Debug.Log($"{gameObject.name} 化为泡沫销毁！");
        // TODO: 粒子特效（纸片碎裂/泡沫）
        Destroy(gameObject);
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        if (TurnManager.Instance != null)
            TurnManager.Instance.UnregisterEnemy(this);
    }
}
