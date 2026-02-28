using UnityEngine;
using System;

/// <summary>
/// 玩家数值系统：经验 / 等级 / 攻击力 / 暴击。
/// 所有数值全部 [SerializeField] 暴露到面板。
/// </summary>
public class PlayerStats : MonoBehaviour
{
    // ────────────────── 等级与经验 ──────────────────
    [Header("=== 等级与经验 ===")]
    [SerializeField] private int currentLevel = 1;
    [SerializeField] private float currentExp = 0f;
    [SerializeField] private float baseExpToNextLevel = 100f;
    [Tooltip("每级所需经验的增长系数")]
    [SerializeField] private float expGrowthRate = 1.15f;
    [SerializeField] private float expMultiplier = 1f;

    // ────────────────── 攻击力 ──────────────────
    [Header("=== 攻击力 ===")]
    [SerializeField] private float baseAttack = 10f;
    [Tooltip("每级增加的攻击力")]
    [SerializeField] private float attackPerLevel = 2f;

    // ────────────────── 暴击 ──────────────────
    [Header("=== 暴击 ===")]
    [SerializeField, Range(0f, 1f)] private float critChance = 0.1f;
    [SerializeField] private float critMultiplier = 1.5f;

    // ────────────────── 永久道具加成 ──────────────────
    [Header("=== 永久道具加成 ===")]
    [SerializeField] private float expItemMultiplierBonus = 0f;

    // ────────────────── 首杀奖励 ──────────────────
    [Header("=== 首杀奖励 ===")]
    [SerializeField] private int firstKillLevelBoost = 8;
    [SerializeField] private float firstKillExpItemBonus = 2f;
    private bool firstKillApplied = false;
    private int killCount = 0;

    // ────────────────── 事件 ──────────────────
    public event Action<int> OnLevelUp;
    public event Action<string> OnSkillUnlocked;
    public event Action<float> OnExpChanged;  // 参数: 0‑1 进度

    // ────────────────── 公开属性 ──────────────────
    public int CurrentLevel => currentLevel;
    public float GetExpProgress() => currentExp / GetExpToNextLevel();

    // ══════════════════ 经验与升级 ══════════════════

    float GetExpToNextLevel()
    {
        return baseExpToNextLevel * Mathf.Pow(expGrowthRate, currentLevel - 1);
    }

    /// <summary>
    /// 添加经验（自动计算加成）
    /// </summary>
    public void AddExp(float amount)
    {
        float total = amount * (expMultiplier + expItemMultiplierBonus);
        currentExp += total;

        Debug.Log($"获得经验: {total:F0} (基础{amount} × {expMultiplier + expItemMultiplierBonus:F1}倍)");

        float needed = GetExpToNextLevel();
        while (currentExp >= needed)
        {
            currentExp -= needed;
            LevelUp();
            needed = GetExpToNextLevel();
        }

        OnExpChanged?.Invoke(GetExpProgress());
    }

    void LevelUp()
    {
        int prev = currentLevel;
        currentLevel++;
        Debug.Log($"★ 升级！Lv.{prev} → Lv.{currentLevel}");
        OnLevelUp?.Invoke(currentLevel);
    }

    // ══════════════════ 击杀与首杀 ══════════════════

    /// <summary>
    /// 击杀敌人后由 EnemyAI 调用
    /// </summary>
    public void OnEnemyKilled(float baseExp)
    {
        killCount++;

        if (!firstKillApplied)
        {
            ApplyFirstKillBonus(baseExp);
        }
        else
        {
            float bonus = 1f + (killCount - 1) * 0.5f;
            AddExp(baseExp * bonus);
            Debug.Log($"连杀 x{killCount}！经验加成 +{(bonus - 1) * 100}%");
        }
    }

    void ApplyFirstKillBonus(float baseExp)
    {
        firstKillApplied = true;
        Debug.Log("=== 首杀奖励触发！===");

        for (int i = 0; i < firstKillLevelBoost; i++)
            LevelUp();

        expItemMultiplierBonus = firstKillExpItemBonus;
        Debug.Log($"获得永久道具【次元碎片】：经验获取 +{firstKillExpItemBonus * 100}%！");

        AddExp(baseExp);
    }

    // ══════════════════ 伤害计算 ══════════════════

    /// <summary>
    /// 最终伤害 = 基础攻击力 × 暴击强度 × 技能倍率
    /// </summary>
    public float GetFinalDamage(float skillMultiplier = 1f)
    {
        float atk = baseAttack + attackPerLevel * (currentLevel - 1);
        float crit = Random.value <= critChance ? critMultiplier : 1f;
        float dmg = atk * crit * skillMultiplier;
        return dmg;
    }
}
