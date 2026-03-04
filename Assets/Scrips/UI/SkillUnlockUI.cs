using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 技能解锁和等级 UI 提示系统。
/// 经验条使用 Mathf.Lerp 缓动动画而非瞬间赋值。
/// </summary>
public class SkillUnlockUI : MonoBehaviour
{
    [Header("=== UI 引用 ===")]
    [SerializeField] private TextMeshProUGUI levelText;
    [SerializeField] private Slider expBar;
    [SerializeField] private GameObject skillUnlockPanel;
    [SerializeField] private TextMeshProUGUI skillUnlockText;

    [Header("=== 经验条缓动 ===")]
    [Tooltip("经验条从当前值插值到目标值的速度")]
    [SerializeField] private float expLerpSpeed = 4f;

    [Header("=== 提示显示时间 ===")]
    [SerializeField] private float skillUnlockDisplayTime = 3f;

    // 引用
    private PlayerStats playerStats;

    // 缓动目标
    private float displayedExpProgress;
    private float targetExpProgress;

    void Start()
    {
        if (skillUnlockPanel != null)
            skillUnlockPanel.SetActive(false);

        playerStats = FindAnyObjectByType<PlayerStats>();

        if (playerStats != null)
        {
            playerStats.OnLevelUp      += HandleLevelUp;
            playerStats.OnSkillUnlocked += HandleSkillUnlocked;
            playerStats.OnExpChanged    += HandleExpChanged;
        }

        // 监听 GameManager 的 Phase 2 经验奖励
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnExpRewarded += HandlePhase2ExpReward;
        }

        // 初始化
        UpdateLevelDisplay();
        if (playerStats != null)
        {
            displayedExpProgress = playerStats.GetExpProgress();
            targetExpProgress = displayedExpProgress;
        }
        if (expBar != null)
            expBar.value = displayedExpProgress;
    }

    void Update()
    {
        // 缓动插值经验条
        if (expBar != null)
        {
            displayedExpProgress = Mathf.Lerp(displayedExpProgress, targetExpProgress, Time.deltaTime * expLerpSpeed);

            // 防止无限逼近不到位
            if (Mathf.Abs(displayedExpProgress - targetExpProgress) < 0.001f)
                displayedExpProgress = targetExpProgress;

            expBar.value = displayedExpProgress;
        }
    }

    void OnDestroy()
    {
        if (playerStats != null)
        {
            playerStats.OnLevelUp      -= HandleLevelUp;
            playerStats.OnSkillUnlocked -= HandleSkillUnlocked;
            playerStats.OnExpChanged    -= HandleExpChanged;
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnExpRewarded -= HandlePhase2ExpReward;
        }
    }

    // ══════════════════ 事件处理 ══════════════════

    void HandleLevelUp(int newLevel)
    {
        UpdateLevelDisplay();
        // 升级时重置条到 0 再插值到新进度
        displayedExpProgress = 0f;
        targetExpProgress = playerStats != null ? playerStats.GetExpProgress() : 0f;
        Debug.Log($"[UI] 升级到 Lv.{newLevel}");
    }

    void HandleExpChanged(float progress01)
    {
        targetExpProgress = progress01;
    }

    void HandlePhase2ExpReward(float expAmount)
    {
        // Phase 2 奖励直接加给 PlayerStats
        if (playerStats != null)
            playerStats.AddExp(expAmount);
    }

    void HandleSkillUnlocked(string skillName)
    {
        if (skillUnlockPanel != null && skillUnlockText != null)
        {
            skillUnlockText.text = $"技能解锁：{skillName}！";
            skillUnlockPanel.SetActive(true);
            CancelInvoke(nameof(HideSkillUnlockPanel));
            Invoke(nameof(HideSkillUnlockPanel), skillUnlockDisplayTime);
        }
    }

    // ══════════════════ 显示 ══════════════════

    void UpdateLevelDisplay()
    {
        if (levelText != null && playerStats != null)
            levelText.text = $"Lv.{playerStats.CurrentLevel}";
    }

    void HideSkillUnlockPanel()
    {
        if (skillUnlockPanel != null)
            skillUnlockPanel.SetActive(false);
    }
}