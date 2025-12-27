using UnityEngine;
using System.Collections.Generic;

public class FusionGridSystem : MonoBehaviour
{
    [System.Serializable]
    public class FusionRecipe
    {
        public string basePlantTag; // 地上原本的植物 (比如 "Peashooter")
        public string cardPlantTag; // 手里的卡片植物 (比如 "WallNut")
        public GameObject resultPrefab; // 融合结果 (比如 "NutShooter")
    }

    public List<FusionRecipe> recipes; // 在面板里配置你的融合配方
    public LayerMask gridLayer;
    
    // 当前选中的卡片植物Tag (由UI按钮设置)
    private string selectedPlantTag = ""; 
    private GameObject selectedPlantPrefab; 

    void Update()
    {
        if (Input.GetMouseButtonDown(0) && selectedPlantTag != "")
        {
            HandleClick();
        }
    }

    // UI按钮调用这个方法
    public void SelectCard(string tag, GameObject prefab)
    {
        selectedPlantTag = tag;
        selectedPlantPrefab = prefab;
        Debug.Log("选中卡片: " + tag);
    }

    void HandleClick()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit2D hit = Physics2D.Raycast(ray.origin, ray.direction, 100f, gridLayer);

        if (hit.collider != null)
        {
            // 获取这个格子脚本
            GridCell cell = hit.collider.GetComponent<GridCell>();
            if (cell == null) return;

            if (cell.currentPlant == null)
            {
                // === 情况A: 空格子，直接种植 ===
                GameObject newPlant = Instantiate(selectedPlantPrefab, cell.transform.position, Quaternion.identity);
                cell.SetPlant(newPlant, selectedPlantTag);
                // TODO: 扣除冷却和阳光
            }
            else
            {
                // === 情况B: 有植物，尝试融合 ===
                TryFuse(cell);
            }
        }
    }

    void TryFuse(GridCell cell)
    {
        string baseTag = cell.currentPlantTag;
        string cardTag = selectedPlantTag;

        // 查找配方
        foreach (var recipe in recipes)
        {
            // 配方匹配 (豌豆+坚果 或 坚果+豌豆)
            bool match = (recipe.basePlantTag == baseTag && recipe.cardPlantTag == cardTag);
            
            if (match)
            {
                Debug.Log("融合成功！生成: " + recipe.resultPrefab.name);
                
                // 1. 销毁旧植物
                Destroy(cell.currentPlant);
                
                // 2. 生成融合植物
                GameObject fusedPlant = Instantiate(recipe.resultPrefab, cell.transform.position, Quaternion.identity);
                
                // 3. 更新格子数据
                cell.SetPlant(fusedPlant, "Fused_" + baseTag + "_" + cardTag);
                return;
            }
        }

        Debug.Log("无法融合这两个植物！");
    }
}
