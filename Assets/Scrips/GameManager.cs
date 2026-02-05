using UnityEngine;
using UnityEngine.Events;
using System;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("=== RPG 数值系统 ===")]
    public int currentLevel = 1;
    public float currentExp = 0;
    public float expToNextLevel = 100f; 
    public float expMultiplier = 1.0f;  
    
    [Header("=== 永久道具加成 ===")]
    public float expItemMultiplierBonus = 0f;  // 永久道具加成 (叠加到expMultiplier)
    public bool firstKillBonusApplied = false; // 首杀奖励是否已触发
    public int killCount = 0;                   // 击杀计数

    [Header("=== 技能解锁门槛 ===")]
    public int dashUnlockLevel = 20; 
    public int diveUnlockLevel = 50; 
    
    [Header("=== 首杀奖励设置 ===")]
    public int firstKillLevelBoost = 8;        // 首杀升级数
    public float firstKillExpItemBonus = 2.0f; // 首杀获得的永久经验加成倍率

    [Header("=== 游戏阶段状态 ===")]
    public bool isPhase1 = true;      
    public bool isTransitioning = false; 
    
    // === 事件系统 ===
    public event Action<int> OnLevelUp;           // 升级事件 (参数: 新等级)
    public event Action<string> OnSkillUnlocked;  // 技能解锁事件 (参数: 技能名)
    public event Action<string> OnItemObtained;   // 获得道具事件 (参数: 道具名)

    // 技能解锁状态追踪
    private bool dashUnlocked = false;
    private bool diveUnlocked = false;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    /// <summary>
    /// 首杀奖励：升8级 + 获得200%经验永久道具
    /// </summary>
    public void ApplyFirstKillBonus(float baseExp)
    {
        if (firstKillBonusApplied) return;
        
        firstKillBonusApplied = true;
        killCount++;
        
        Debug.Log("=== 首杀奖励触发！===");
        
        // 1. 直接升级（首杀升8级）
        for (int i = 0; i < firstKillLevelBoost; i++)
        {
            LevelUp();
        }
        
        // 2. 获得永久200%经验加成道具
        expItemMultiplierBonus = firstKillExpItemBonus;
        expMultiplier += expItemMultiplierBonus;
        
        Debug.Log($"获得永久道具【次元碎片】：经验获取 +{firstKillExpItemBonus * 100}%！");
        OnItemObtained?.Invoke("次元碎片");
        
        // 3. 额外给一些基础经验
        AddExp(baseExp);
    }

    /// <summary>
    /// 添加经验值（会自动计算加成）
    /// </summary>
    public void AddExp(float amount)
    {
        float finalExp = amount * expMultiplier;
        currentExp += finalExp;
        
        Debug.Log($"获得经验: {finalExp:F0} (基础{amount} x {expMultiplier:F1}倍)");

        while (currentExp >= expToNextLevel)
        {
            currentExp -= expToNextLevel;
            LevelUp();
        }
    }
    
    /// <summary>
    /// 普通击杀（非首杀）的经验奖励
    /// </summary>
    public void OnEnemyKilled(float baseExp)
    {
        killCount++;
        
        if (!firstKillBonusApplied)
        {
            ApplyFirstKillBonus(baseExp);
        }
        else
        {
            // 后续击杀：经验递增 (基础 * (1 + 击杀数 * 0.5))
            float bonusMultiplier = 1f + (killCount - 1) * 0.5f;
            AddExp(baseExp * bonusMultiplier);
            Debug.Log($"连杀 x{killCount}！经验加成 +{(bonusMultiplier - 1) * 100}%");
        }
    }

    void LevelUp()
    {
        int previousLevel = currentLevel;
        currentLevel++;
        
        Debug.Log($"★ 升级！Lv.{previousLevel} → Lv.{currentLevel}");
        OnLevelUp?.Invoke(currentLevel);

        // 检查技能解锁
        CheckSkillUnlocks();
    }
    
    /// <summary>
    /// 检查并触发技能解锁
    /// </summary>
    void CheckSkillUnlocks()
    {
        // 次元突刺解锁
        if (!dashUnlocked && currentLevel >= dashUnlockLevel)
        {
            dashUnlocked = true;
            Debug.Log($"★★★ 技能解锁：【次元突刺】(Shift键) ★★★");
            OnSkillUnlocked?.Invoke("次元突刺");
        }
        
        // 裂口下坠解锁
        if (!diveUnlocked && currentLevel >= diveUnlockLevel)
        {
            diveUnlocked = true;
            Debug.Log($"★★★ 技能解锁：【裂口下坠】(空中按S键) ★★★");
            OnSkillUnlocked?.Invoke("裂口下坠");
        }
    }

    public bool CanUseDash() => currentLevel >= dashUnlockLevel;
    public bool CanUseDive() => currentLevel >= diveUnlockLevel;
    
    /// <summary>
    /// 获取当前经验进度 (0-1)
    /// </summary>
    public float GetExpProgress() => currentExp / expToNextLevel;
}