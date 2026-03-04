using UnityEngine;

/// <summary>
/// 6×4 网格管理器：坐标系、世界坐标映射、实体占位检测。
/// 网格坐标 (0,0) 在左下角，(5,3) 在右上角。
/// </summary>
public class GridManager : MonoBehaviour
{
    public static GridManager Instance { get; private set; }

    // ────────────────── 网格尺寸 ──────────────────
    public const int WIDTH = 6;   // 列 0~5
    public const int HEIGHT = 4;  // 行 0~3

    // ────────────────── 世界坐标映射 ──────────────────
    [Header("=== 世界坐标映射 ===")]
    [Tooltip("网格 (0,0) 在世界空间中的位置")]
    [SerializeField] private Vector2 gridOrigin = Vector2.zero;
    [Tooltip("每个格子在世界空间中的尺寸")]
    [SerializeField] private Vector2 cellSize = new Vector2(1.5f, 1.5f);

    // ────────────────── 占位数据 ──────────────────
    // 每个格子存储占据它的实体引用，null 表示空
    private GridEntity[,] grid = new GridEntity[WIDTH, HEIGHT];

    // ══════════════════ 生命周期 ══════════════════

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    // ══════════════════ 坐标工具 ══════════════════

    /// <summary>
    /// 网格坐标 → 世界坐标（格子中心）
    /// </summary>
    public Vector3 GridToWorld(Vector2Int coord)
    {
        float x = gridOrigin.x + coord.x * cellSize.x;
        float y = gridOrigin.y + coord.y * cellSize.y;
        return new Vector3(x, y, 0f);
    }

    /// <summary>
    /// 世界坐标 → 最近的网格坐标（不做边界裁切）
    /// </summary>
    public Vector2Int WorldToGrid(Vector3 worldPos)
    {
        int x = Mathf.RoundToInt((worldPos.x - gridOrigin.x) / cellSize.x);
        int y = Mathf.RoundToInt((worldPos.y - gridOrigin.y) / cellSize.y);
        return new Vector2Int(x, y);
    }

    /// <summary>
    /// 坐标是否在网格范围内
    /// </summary>
    public bool IsInBounds(Vector2Int coord)
    {
        return coord.x >= 0 && coord.x < WIDTH &&
               coord.y >= 0 && coord.y < HEIGHT;
    }

    // ══════════════════ 占位管理 ══════════════════

    /// <summary>
    /// 获取指定格子的实体（可能为 null）
    /// </summary>
    public GridEntity GetEntityAt(Vector2Int coord)
    {
        if (!IsInBounds(coord)) return null;
        return grid[coord.x, coord.y];
    }

    /// <summary>
    /// 指定格子是否为空
    /// </summary>
    public bool IsCellEmpty(Vector2Int coord)
    {
        if (!IsInBounds(coord)) return false;
        return grid[coord.x, coord.y] == null;
    }

    /// <summary>
    /// 将实体放入格子（会清除旧位置）
    /// </summary>
    public void PlaceEntity(GridEntity entity, Vector2Int coord)
    {
        // 清除旧位置
        RemoveEntity(entity);

        if (IsInBounds(coord))
        {
            grid[coord.x, coord.y] = entity;
        }
    }

    /// <summary>
    /// 从网格中移除实体
    /// </summary>
    public void RemoveEntity(GridEntity entity)
    {
        for (int x = 0; x < WIDTH; x++)
        {
            for (int y = 0; y < HEIGHT; y++)
            {
                if (grid[x, y] == entity)
                {
                    grid[x, y] = null;
                }
            }
        }
    }

    /// <summary>
    /// 清空整个网格
    /// </summary>
    public void ClearGrid()
    {
        for (int x = 0; x < WIDTH; x++)
            for (int y = 0; y < HEIGHT; y++)
                grid[x, y] = null;
    }

    // ══════════════════ Gizmos ══════════════════

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 1f, 1f, 0.3f);

        for (int x = 0; x < WIDTH; x++)
        {
            for (int y = 0; y < HEIGHT; y++)
            {
                Vector3 center = GridToWorld(new Vector2Int(x, y));
                Gizmos.DrawWireCube(center, new Vector3(cellSize.x * 0.95f, cellSize.y * 0.95f, 0.01f));
            }
        }

        // 左半边（炮台区域）浅蓝色
        Gizmos.color = new Color(0.3f, 0.6f, 1f, 0.1f);
        for (int x = 0; x <= 2; x++)
            for (int y = 0; y < HEIGHT; y++)
                Gizmos.DrawCube(GridToWorld(new Vector2Int(x, y)),
                    new Vector3(cellSize.x * 0.9f, cellSize.y * 0.9f, 0.01f));

        // 右半边（敌人区域）浅红色
        Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.1f);
        for (int x = 3; x <= 5; x++)
            for (int y = 0; y < HEIGHT; y++)
                Gizmos.DrawCube(GridToWorld(new Vector2Int(x, y)),
                    new Vector3(cellSize.x * 0.9f, cellSize.y * 0.9f, 0.01f));
    }
}
