using UnityEngine;

/// <summary>
/// 6×4 网格管理器：坐标系、世界坐标映射、实体占位检测。
/// 网格坐标 (0,0) 在左下角，(5,3) 在右上角。
/// 新增：运行时 GL 绘制网格线（Game 视图可见）。
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

    // ────────────────── 运行时网格绘制 ──────────────────
    [Header("=== 运行时网格绘制 ===")]
    [Tooltip("网格线颜色")]
    [SerializeField] private Color gridLineColor = new Color(1f, 1f, 1f, 0.5f);
    [Tooltip("炮台区域底色（左半）")]
    [SerializeField] private Color turretZoneColor = new Color(0.3f, 0.6f, 1f, 0.08f);
    [Tooltip("敌人区域底色（右半）")]
    [SerializeField] private Color enemyZoneColor = new Color(1f, 0.3f, 0.3f, 0.08f);

    // GL 绘制用材质
    private Material glMaterial;

    // ────────────────── 占位数据 ──────────────────
    private GridEntity[,] grid = new GridEntity[WIDTH, HEIGHT];

    // ══════════════════ 生命周期 ══════════════════

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        // 创建 GL 绘制材质
        Shader shader = Shader.Find("Hidden/Internal-Colored");
        if (shader != null)
        {
            glMaterial = new Material(shader);
            glMaterial.hideFlags = HideFlags.HideAndDontSave;
            glMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            glMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            glMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            glMaterial.SetInt("_ZWrite", 0);
        }
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
        RemoveEntity(entity);
        if (IsInBounds(coord))
            grid[coord.x, coord.y] = entity;
    }

    /// <summary>
    /// 从网格中移除实体
    /// </summary>
    public void RemoveEntity(GridEntity entity)
    {
        for (int x = 0; x < WIDTH; x++)
            for (int y = 0; y < HEIGHT; y++)
                if (grid[x, y] == entity)
                    grid[x, y] = null;
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

    // ══════════════════ 运行时 GL 绘制（Game 视图可见） ══════════════════

    void OnRenderObject()
    {
        if (glMaterial == null) return;

        glMaterial.SetPass(0);

        GL.PushMatrix();
        GL.LoadOrtho();

        Camera cam = Camera.main;
        if (cam == null) { GL.PopMatrix(); return; }

        // 半格偏移：让方格线围绕格子中心
        float halfW = cellSize.x * 0.5f;
        float halfH = cellSize.y * 0.5f;

        // 炮台区域底色 (列 0~2)
        GL.Begin(GL.QUADS);
        GL.Color(turretZoneColor);
        for (int x = 0; x <= 2; x++)
        {
            for (int y = 0; y < HEIGHT; y++)
            {
                Vector3 center = GridToWorld(new Vector2Int(x, y));
                DrawQuadGL(cam, center, halfW * 0.95f, halfH * 0.95f);
            }
        }
        GL.End();

        // 敌人区域底色 (列 3~5)
        GL.Begin(GL.QUADS);
        GL.Color(enemyZoneColor);
        for (int x = 3; x <= 5; x++)
        {
            for (int y = 0; y < HEIGHT; y++)
            {
                Vector3 center = GridToWorld(new Vector2Int(x, y));
                DrawQuadGL(cam, center, halfW * 0.95f, halfH * 0.95f);
            }
        }
        GL.End();

        // 绘制网格线
        GL.Begin(GL.LINES);
        GL.Color(gridLineColor);

        for (int x = 0; x < WIDTH; x++)
        {
            for (int y = 0; y < HEIGHT; y++)
            {
                Vector3 center = GridToWorld(new Vector2Int(x, y));
                DrawWireCellGL(cam, center, halfW, halfH);
            }
        }

        GL.End();
        GL.PopMatrix();
    }

    /// <summary>
    /// 用 GL.QUADS 绘制一个填充四边形
    /// </summary>
    void DrawQuadGL(Camera cam, Vector3 worldCenter, float hw, float hh)
    {
        Vector3 bl = cam.WorldToViewportPoint(worldCenter + new Vector3(-hw, -hh, 0));
        Vector3 br = cam.WorldToViewportPoint(worldCenter + new Vector3( hw, -hh, 0));
        Vector3 tr = cam.WorldToViewportPoint(worldCenter + new Vector3( hw,  hh, 0));
        Vector3 tl = cam.WorldToViewportPoint(worldCenter + new Vector3(-hw,  hh, 0));

        GL.Vertex3(bl.x, bl.y, 0);
        GL.Vertex3(br.x, br.y, 0);
        GL.Vertex3(tr.x, tr.y, 0);
        GL.Vertex3(tl.x, tl.y, 0);
    }

    /// <summary>
    /// 用 GL.LINES 绘制一个格子的四条边线
    /// </summary>
    void DrawWireCellGL(Camera cam, Vector3 worldCenter, float hw, float hh)
    {
        Vector3 bl = cam.WorldToViewportPoint(worldCenter + new Vector3(-hw, -hh, 0));
        Vector3 br = cam.WorldToViewportPoint(worldCenter + new Vector3( hw, -hh, 0));
        Vector3 tr = cam.WorldToViewportPoint(worldCenter + new Vector3( hw,  hh, 0));
        Vector3 tl = cam.WorldToViewportPoint(worldCenter + new Vector3(-hw,  hh, 0));

        GL.Vertex3(bl.x, bl.y, 0); GL.Vertex3(br.x, br.y, 0);
        GL.Vertex3(br.x, br.y, 0); GL.Vertex3(tr.x, tr.y, 0);
        GL.Vertex3(tr.x, tr.y, 0); GL.Vertex3(tl.x, tl.y, 0);
        GL.Vertex3(tl.x, tl.y, 0); GL.Vertex3(bl.x, bl.y, 0);
    }

    // ══════════════════ Gizmos（Scene 视图用，保留） ══════════════════

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
