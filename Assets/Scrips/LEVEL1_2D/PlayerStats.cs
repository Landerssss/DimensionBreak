using UnityEngine;
using System;

/// <summary>
/// 玩家数值系统：经验 / 等级 / 攻击力 / 暴击。
/// 严格按照设计文档：击杀不同敌人获得不同经验，特定等级解锁突刺。
/// </summary>
public class PlayerStats : MonoBehaviour
{
    [Header("=== 等级与经验 ===")]
    [SerializeField] private int currentLevel = 1;
    [SerializeField] private float currentExp = 0f;
    [SerializeField] private float baseExpToNextLevel = 100f;
    [SerializeField] private float expGrowthRate = 1.15f;

    [Header("=== 攻击力 ===")]
    [SerializeField] private float baseAttack = 10f;
    [SerializeField] private float attackPerLevel = 2f;

    [Header("=== 暴击 ===")]
    [SerializeField, Range(0f, 1f)] private float critChance = 0.1f;
    [SerializeField] private float critMultiplier = 1.5f;

    [Header("=== 技能解锁条件 ===")]
    [Tooltip("达到此等级时，Shift键会变成突刺（带有伤害）")]
    [SerializeField] private int dashAttackUnlockLevel = 5;

    // ────────────────── 事件 ──────────────────
    public event Action<int> OnLevelUp;
    public event Action<string> OnSkillUnlocked;
    public event Action<float> OnExpChanged;

    public int CurrentLevel => currentLevel;
    public float GetExpProgress() => currentExp / GetExpToNextLevel();

    float GetExpToNextLevel()
    {
        return baseExpToNextLevel * Mathf.Pow(expGrowthRate, currentLevel - 1);
    }

    public void AddExp(float amount)
    {
        currentExp += amount;
        Debug.Log($"获得经验: {amount}，当前总经验: {currentExp}");

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

        // 严格按照文档：达到某等级解锁突刺
        if (currentLevel == dashAttackUnlockLevel)
        {
            OnSkillUnlocked?.Invoke("次元突刺");
        }
    }

    // 由 EnemyAI 死亡时调用
    public void OnEnemyKilled(float baseExp)
    {
        AddExp(baseExp);
    }

    // 计算最终伤害（修复了 Random 报错）
    public float GetFinalDamage(float skillMultiplier = 1f)
    {
        float atk = baseAttack + attackPerLevel * (currentLevel - 1);
        // 明确使用 UnityEngine 的 Random
        float crit = UnityEngine.Random.value <= critChance ? critMultiplier : 1f;
        return atk * crit * skillMultiplier;
    }
}