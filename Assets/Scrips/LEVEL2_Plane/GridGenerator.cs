using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Phase 2 地图生成器：根据 clearedCount 递增难度，BFS 验证可通关，失败则重试。
/// 所有数值均通过 [SerializeField] 暴露到面板。
/// </summary>
public class GridGenerator : MonoBehaviour
{
    // ────────────────── 实体 Prefab ──────────────────
    [Header("=== Prefab 引用 ===")]
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private GameObject exitPrefab;
    [SerializeField] private GameObject turretPrefab;
    [SerializeField] private GameObject enemyPrefab;

    // ────────────────── 玩家 / 出口固定行 ──────────────────
    [Header("=== 玩家与出口 ===")]
    [Tooltip("玩家固定在第0列，行号从此处随机")]
    [SerializeField] private int playerColumn = 0;
    [SerializeField] private int exitColumn = 5;

    // ────────────────── 难度曲线 ──────────────────
    [Header("=== 难度曲线 ===")]
    [Tooltip("基础炮台数量（clearedCount=0 时）")]
    [SerializeField] private int baseTurretCount = 1;
    [Tooltip("每通关一次额外增加的炮台数")]
    [SerializeField] private int turretPerClear = 1;
    [SerializeField] private int maxTurretCount = 5;

    [Tooltip("基础敌人数量")]
    [SerializeField] private int baseEnemyCount = 1;
    [Tooltip("每通关一次额外增加的敌人数")]
    [SerializeField] private int enemyPerClear = 1;
    [SerializeField] private int maxEnemyCount = 6;

    // ────────────────── 生成安全 ──────────────────
    [Header("=== 生成安全 ===")]
    [Tooltip("单次生成最大重试次数")]
    [SerializeField] private int maxRetries = 100;

    // ────────────────── 生成的实例引用 ──────────────────
    private readonly List<GameObject> spawnedObjects = new List<GameObject>();

    // ══════════════════ 公开接口 ══════════════════

    /// <summary>
    /// 生成一张保证可通关的 Phase2 地图。
    /// 调用前请确保 GridManager 已就绪。
    /// </summary>
    public void GenerateLevel()
    {
        int clearedCount = 0;
        if (GameManager.Instance != null)
            clearedCount = GameManager.Instance.Phase2ClearedCount;

        int turretCount = Mathf.Min(baseTurretCount + turretPerClear * clearedCount, maxTurretCount);
        int enemyCount  = Mathf.Min(baseEnemyCount  + enemyPerClear  * clearedCount, maxEnemyCount);

        bool success = false;
        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            ClearSpawned();
            GridManager.Instance.ClearGrid();

            // 1. 放置玩家
            int playerRow = Random.Range(0, GridManager.HEIGHT);
            Vector2Int playerPos = new Vector2Int(playerColumn, playerRow);
            SpawnEntity(playerPrefab, playerPos);

            // 2. 放置出口
            int exitRow = Random.Range(0, GridManager.HEIGHT);
            Vector2Int exitPos = new Vector2Int(exitColumn, exitRow);
            SpawnEntity(exitPrefab, exitPos);

            // 3. 放置炮台（X ∈ [0,2]，不重叠，不与玩家重叠）
            PlaceRandom(turretPrefab, turretCount, 0, 2, new HashSet<Vector2Int> { playerPos });

            // 4. 放置敌人（X ∈ [3,5]，不重叠，不与出口重叠）
            PlaceRandom(enemyPrefab, enemyCount, 3, 5, new HashSet<Vector2Int> { exitPos });

            // 5. BFS 验证
            if (BFSValidate(playerPos, exitPos))
            {
                success = true;
                Debug.Log($"[GridGenerator] 第 {attempt + 1} 次尝试生成成功（炮台 {turretCount}, 敌人 {enemyCount}）");
                break;
            }
        }

        if (!success)
        {
            Debug.LogWarning("[GridGenerator] 达到最大重试次数，强制清空障碍生成最简地图");
            ClearSpawned();
            GridManager.Instance.ClearGrid();

            Vector2Int pPos = new Vector2Int(playerColumn, 0);
            Vector2Int ePos = new Vector2Int(exitColumn, 0);
            SpawnEntity(playerPrefab, pPos);
            SpawnEntity(exitPrefab, ePos);
        }
    }

    /// <summary>
    /// 清除所有已生成的实体
    /// </summary>
    public void ClearSpawned()
    {
        foreach (var obj in spawnedObjects)
        {
            if (obj != null) Destroy(obj);
        }
        spawnedObjects.Clear();
    }
    void Start()
    {
        // 延迟0.1秒生成，确保各种 Manager 都已经初始化完毕
        Invoke(nameof(GenerateLevel), 0.1f);
    }

    // ══════════════════ 内部方法 ══════════════════

    void SpawnEntity(GameObject prefab, Vector2Int gridPos)
    {
        if (prefab == null || GridManager.Instance == null) return;

        Vector3 worldPos = GridManager.Instance.GridToWorld(gridPos);
        GameObject obj = Instantiate(prefab, worldPos, Quaternion.identity);
        spawnedObjects.Add(obj);

        // 让实体的 gridPosition 同步（GridEntity 的 Start 会自动注册）
        GridEntity entity = obj.GetComponent<GridEntity>();
        if (entity != null)
        {
            // 通过反射或公开方法设置位置（GridEntity.gridPosition 是 protected）
            // 这里使用一个初始化方法
            entity.InitGridPosition(gridPos);
        }
    }

    void PlaceRandom(GameObject prefab, int count, int minX, int maxX, HashSet<Vector2Int> occupied)
    {
        List<Vector2Int> candidates = new List<Vector2Int>();
        for (int x = minX; x <= maxX; x++)
        {
            for (int y = 0; y < GridManager.HEIGHT; y++)
            {
                Vector2Int pos = new Vector2Int(x, y);
                if (!occupied.Contains(pos))
                    candidates.Add(pos);
            }
        }

        // Fisher-Yates 洗牌
        for (int i = candidates.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
        }

        int toPlace = Mathf.Min(count, candidates.Count);
        for (int i = 0; i < toPlace; i++)
        {
            SpawnEntity(prefab, candidates[i]);
            occupied.Add(candidates[i]);
        }
    }

    // ══════════════════ BFS 通关验证 ══════════════════

    /// <summary>
    /// 将炮台和敌人视为静态障碍，BFS 检测玩家是否能走到出口。
    /// </summary>
    bool BFSValidate(Vector2Int start, Vector2Int goal)
    {
        // 构建障碍集合
        HashSet<Vector2Int> blocked = new HashSet<Vector2Int>();
        for (int x = 0; x < GridManager.WIDTH; x++)
        {
            for (int y = 0; y < GridManager.HEIGHT; y++)
            {
                Vector2Int pos = new Vector2Int(x, y);
                GridEntity entity = GridManager.Instance.GetEntityAt(pos);
                if (entity == null) continue;

                // 炮台和敌人都算障碍
                if (entity is Turret || entity is PaperEnemy)
                    blocked.Add(pos);
            }
        }

        // BFS
        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        HashSet<Vector2Int> visited = new HashSet<Vector2Int>();

        queue.Enqueue(start);
        visited.Add(start);

        Vector2Int[] dirs = {
            Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right
        };

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();

            if (current == goal) return true;

            foreach (var d in dirs)
            {
                Vector2Int next = current + d;
                if (!GridManager.Instance.IsInBounds(next)) continue;
                if (visited.Contains(next)) continue;
                if (blocked.Contains(next)) continue;

                visited.Add(next);
                queue.Enqueue(next);
            }
        }

        return false;
    }
}
