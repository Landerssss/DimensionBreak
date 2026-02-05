using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 技能解锁和等级UI提示系统
/// 挂载到Canvas下的空物体上
/// </summary>
public class SkillUnlockUI : MonoBehaviour
{
    [Header("=== UI 引用 ===")]
    public TextMeshProUGUI levelText;           // 等级显示
    public Slider expBar;                        // 经验条
    public GameObject skillUnlockPanel;          // 技能解锁提示面板
    public TextMeshProUGUI skillUnlockText;      // 技能解锁文字
    public GameObject itemObtainedPanel;         // 道具获取提示面板
    public TextMeshProUGUI itemObtainedText;     // 道具获取文字

    [Header("=== 提示显示时间 ===")]
    public float skillUnlockDisplayTime = 3f;
    public float itemObtainedDisplayTime = 2f;

    void Start()
    {
        // 隐藏提示面板
        if (skillUnlockPanel) skillUnlockPanel.SetActive(false);
        if (itemObtainedPanel) itemObtainedPanel.SetActive(false);

        // 订阅GameManager事件
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnLevelUp += HandleLevelUp;
            GameManager.Instance.OnSkillUnlocked += HandleSkillUnlocked;
            GameManager.Instance.OnItemObtained += HandleItemObtained;
        }

        // 初始化UI
        UpdateLevelDisplay();
    }

    void Update()
    {
        // 持续更新经验条
        UpdateExpBar();
    }

    void OnDestroy()
    {
        // 取消订阅事件（防止内存泄漏）
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnLevelUp -= HandleLevelUp;
            GameManager.Instance.OnSkillUnlocked -= HandleSkillUnlocked;
            GameManager.Instance.OnItemObtained -= HandleItemObtained;
        }
    }

    void UpdateLevelDisplay()
    {
        if (levelText != null && GameManager.Instance != null)
        {
            levelText.text = $"Lv.{GameManager.Instance.currentLevel}";
        }
    }

    void UpdateExpBar()
    {
        if (expBar != null && GameManager.Instance != null)
        {
            expBar.value = GameManager.Instance.GetExpProgress();
        }
    }

    void HandleLevelUp(int newLevel)
    {
        UpdateLevelDisplay();
        
        // 可以在这里添加升级特效
        Debug.Log($"[UI] 升级到 Lv.{newLevel}");
    }

    void HandleSkillUnlocked(string skillName)
    {
        if (skillUnlockPanel != null && skillUnlockText != null)
        {
            skillUnlockText.text = $"技能解锁：{skillName}！";
            skillUnlockPanel.SetActive(true);
            
            // 延迟隐藏
            Invoke(nameof(HideSkillUnlockPanel), skillUnlockDisplayTime);
        }
    }

    void HideSkillUnlockPanel()
    {
        if (skillUnlockPanel) skillUnlockPanel.SetActive(false);
    }

    void HandleItemObtained(string itemName)
    {
        if (itemObtainedPanel != null && itemObtainedText != null)
        {
            itemObtainedText.text = $"获得道具：【{itemName}】";
            itemObtainedPanel.SetActive(true);
            
            // 延迟隐藏
            Invoke(nameof(HideItemObtainedPanel), itemObtainedDisplayTime);
        }
    }

    void HideItemObtainedPanel()
    {
        if (itemObtainedPanel) itemObtainedPanel.SetActive(false);
    }
}
