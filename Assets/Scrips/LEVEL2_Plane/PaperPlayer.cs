using UnityEngine;

/// <summary>
/// 纸片玩家：键盘WASD移动（回合制，每次一格），仅在第0列初始生成。
/// 碰到敌人则死亡；到达第5列（出口列）则通关。
/// </summary>
public class PaperPlayer : GridEntity
{
    // ────────────────── 出口列 ──────────────────
    [Header("=== 玩家设置 ===")]
    [SerializeField] private int exitColumn = 5;

    // ────────────────── 失败回调场景 ──────────────────
    [Tooltip("死亡后加载的场景名")]
    [SerializeField] private string failSceneName = "Level1_2D";

    private bool isDead;

    // ══════════════════ 生命周期 ══════════════════

    protected override void Start()
    {
        base.Start();

        // 注册到 TurnManager
        if (TurnManager.Instance != null)
            TurnManager.Instance.player = this;
    }

    protected override void Update()
    {
        base.Update();

        if (isDead) return;
        if (TurnManager.Instance == null || TurnManager.Instance.IsBusy) return;
        if (IsAnimating) return;

        HandleInput();
    }

    // ══════════════════ 输入 ══════════════════

    void HandleInput()
    {
        Vector2Int dir = Vector2Int.zero;

        if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow))
            dir = Vector2Int.up;
        else if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow))
            dir = Vector2Int.down;
        else if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))
            dir = Vector2Int.left;
        else if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow))
            dir = Vector2Int.right;

        if (dir == Vector2Int.zero) return;

        Vector2Int target = GridPosition + dir;

        // 边界检查
        if (!GridManager.Instance.IsInBounds(target)) return;

        // 检查目标格是否有炮台（不可穿过）
        GridEntity occupant = GridManager.Instance.GetEntityAt(target);
        if (occupant is Turret) return;

        // 检查是否撞到敌人 → 死亡
        if (occupant is PaperEnemy)
        {
            MoveToCell(target);
            OnPlayerDeath();
            return;
        }

        // 正常移动
        MoveToCell(target);

        // 检查是否到达出口
        if (target.x >= exitColumn)
        {
            OnReachExit();
            return;
        }

        // 通知 TurnManager 玩家已完成移动
        TurnManager.Instance.PlayerFinishedMove();
    }

    // ══════════════════ 胜败 ══════════════════

    public void OnPlayerDeath()
    {
        if (isDead) return;
        isDead = true;
        Debug.Log("纸片玩家死亡！Phase 2 失败，退回第一阶段。");

        // TODO: 播放死亡特效（纸片撕裂/燃烧）
        // 延迟后回退
        StartCoroutine(DeathSequence());
    }

    System.Collections.IEnumerator DeathSequence()
    {
        yield return new WaitForSeconds(1f);

        if (GameManager.Instance != null)
            GameManager.Instance.GoToPhase(GameManager.GamePhase.Phase1_2D);
        else
            UnityEngine.SceneManagement.SceneManager.LoadScene(failSceneName);
    }

    void OnReachExit()
    {
        Debug.Log("纸片玩家到达出口！Phase 2 通关！");

        if (GameManager.Instance != null)
            GameManager.Instance.OnPhase2Victory();
        else
            UnityEngine.SceneManagement.SceneManager.LoadScene(failSceneName);
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        if (TurnManager.Instance != null && TurnManager.Instance.player == this)
            TurnManager.Instance.player = null;
    }
}
