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
    public TextMeshProUGUI levelText;            // 等级显示
    public Slider expBar;                        // 经验条
    public GameObject skillUnlockPanel;          // 技能解锁提示面板
    public TextMeshProUGUI skillUnlockText;      // 技能解锁文字


    [Header("=== 提示显示时间 ===")]
    public float skillUnlockDisplayTime = 3f;

    // 新增：引用场景中玩家身上的数值中心
    private PlayerStats playerStats;

    void Start()
    {
        // 隐藏提示面板
        if (skillUnlockPanel) skillUnlockPanel.SetActive(false);

        // 动态获取当前场景里的玩家数值脚本
        playerStats = FindAnyObjectByType<PlayerStats>();

        // 订阅 PlayerStats 上的事件（而不是 GameManager）
        if (playerStats != null)
        {
            playerStats.OnLevelUp += HandleLevelUp;
            playerStats.OnSkillUnlocked += HandleSkillUnlocked;
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
        if (playerStats != null)
        {
            playerStats.OnLevelUp -= HandleLevelUp;
            playerStats.OnSkillUnlocked -= HandleSkillUnlocked;
        }
    }

    void UpdateLevelDisplay()
    {
        if (levelText != null && playerStats != null)
        {
            levelText.text = $"Lv.{playerStats.CurrentLevel}";
        }
    }

    void UpdateExpBar()
    {
        if (expBar != null && playerStats != null)
        {
            expBar.value = playerStats.GetExpProgress();
        }
    }

    void HandleLevelUp(int newLevel)
    {
        UpdateLevelDisplay();
        Debug.Log($"[UI] 升级到 Lv.{newLevel}");
    }

    void HandleSkillUnlocked(string skillName)
    {
        if (skillUnlockPanel != null && skillUnlockText != null)
        {
            skillUnlockText.text = $"技能解锁：{skillName}！";
            skillUnlockPanel.SetActive(true);
            
            // 取消上一次的延迟隐藏（防止连续解锁时UI闪烁）
            CancelInvoke(nameof(HideSkillUnlockPanel));
            Invoke(nameof(HideSkillUnlockPanel), skillUnlockDisplayTime);
        }
    }

    void HideSkillUnlockPanel()
    {
        if (skillUnlockPanel) skillUnlockPanel.SetActive(false);
    }

}