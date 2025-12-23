using UnityEngine;

public class TowerGridSystem : MonoBehaviour
{
    [Header("=== 塔防设置 ===")]
    public GameObject tilePrefab; // 格子预制体（做一个半透明的方块）
    public GameObject towerPrefab; // 塔/植物预制体（做一个圆柱体代替）
    public int gridWidth = 8;     // 宽多少个格子
    public int gridHeight = 5;    // 高多少个格子
    public float cellSize = 1.5f; // 格子大小
    public Transform gridOrigin;  // 网格生成的起始点（放在塔防地图的左下角）

    [Header("=== 控制开关 ===")]
    public bool isTDActive = false; // 只有切换视角后才开启

    private GameObject[,] gridArray;

    void Start()
    {
        // 游戏开始时先不生成，或者生成了先隐藏
        // GenerateGrid(); // 如果想一开始就生成测试，可以取消注释
    }

    void Update()
    {
        if (!isTDActive) return;

        // 鼠标点击放置逻辑
        if (Input.GetMouseButtonDown(0))
        {
            HandleClick();
        }
    }

    // 1. 生成网格 (在进入塔防模式时调用)
    public void GenerateGrid()
    {
        isTDActive = true;
        gridArray = new GameObject[gridWidth, gridHeight];

        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                // 计算位置
                Vector3 pos = gridOrigin.position + new Vector3(x * cellSize, 0, y * cellSize); // 注意这里用X和Z轴，因为是平铺在地上

                // 生成格子显示
                GameObject tile = Instantiate(tilePrefab, pos, Quaternion.identity);
                tile.transform.SetParent(gridOrigin);
                gridArray[x, y] = tile;
            }
        }
    }

    // 2. 处理点击
    void HandleClick()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit))
        {
            // 如果点到了格子 (确保格子预制体有Collider)
            // 这里做一个简单的近似计算找到最近的格子中心
            Vector3 clickPos = hit.point - gridOrigin.position;
            int x = Mathf.FloorToInt(clickPos.x / cellSize);
            int y = Mathf.FloorToInt(clickPos.z / cellSize);

            // 检查边界
            if (x >= 0 && x < gridWidth && y >= 0 && y < gridHeight)
            {
                PlaceTower(x, y);
            }
        }
    }

    void PlaceTower(int x, int y)
    {
        // 计算放置坐标
        Vector3 placePos = gridOrigin.position + new Vector3(x * cellSize + cellSize / 2, 0, y * cellSize + cellSize / 2);

        // 简单防止重叠：这里应该加个逻辑判断该格子是否已有塔（毕设暂时忽略或后续加）
        Instantiate(towerPrefab, placePos, Quaternion.identity);
        Debug.Log($"在 [{x},{y}] 种植了防御塔！");
    }
}
