using UnityEngine;
using System;
using System.Collections;

/// <summary>
/// 玩家数值系统：生命值 / 经验 / 等级 / 攻击力 / 暴击。
/// 严格按照设计文档：击杀不同敌人获得不同经验，特定等级解锁突刺。
/// </summary>
public class PlayerStats : MonoBehaviour
{
    // ────────────────── 生命值 ──────────────────
    [Header("=== 生命值 ===")]
    [SerializeField] private float maxHP = 100f;
    private float currentHP;

    [Tooltip("受击时颜色闪红的持续时间")]
    [SerializeField] private float hitFlashDuration = 0.15f;

    // ────────────────── 等级与经验 ──────────────────
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

    // 生命值属性（供 UI 读取）
    public float CurrentHP => currentHP;
    public float MaxHP => maxHP;
    public float GetHPProgress() => maxHP > 0 ? currentHP / maxHP : 0f;

    public int CurrentLevel => currentLevel;
    public float GetExpProgress() => currentExp / GetExpToNextLevel();

    // ────────────────── 私有引用 ──────────────────
    private SpriteRenderer spriteRenderer;

    // ══════════════════ 生命周期 ══════════════════

    void Awake()
    {
        // 初始化生命值
        currentHP = maxHP;
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    float GetExpToNextLevel()
    {
        return baseExpToNextLevel * Mathf.Pow(expGrowthRate, currentLevel - 1);
    }

    // ══════════════════ 生命值 ══════════════════

    /// <summary>
    /// 玩家受到伤害：减少 HP，闪红反馈，HP 归零时触发死亡重来逻辑。
    /// 由 EnemyAI 攻击时调用。
    /// </summary>
    public void TakeDamage(float damage)
    {
        if (damage <= 0) return;

        currentHP -= damage;
        currentHP = Mathf.Max(currentHP, 0f);
        Debug.Log($"玩家受到 {damage:F0} 伤害，剩余 HP: {currentHP:F0}");

        // 颜色闪红反馈
        if (spriteRenderer != null)
        {
            spriteRenderer.color = Color.red;
            StartCoroutine(ResetColorAfter(hitFlashDuration));
        }

        if (currentHP <= 0f)
        {
            OnPlayerDied();
        }
    }

    private IEnumerator ResetColorAfter(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (spriteRenderer != null)
            spriteRenderer.color = Color.white;
    }

    /// <summary>
    /// 玩家死亡逻辑：可在此扩展重载关卡、显示 UI 等。
    /// </summary>
    private void OnPlayerDied()
    {
        Debug.Log("玩家死亡！触发重来逻辑...");
        // TODO: 触发死亡动画、显示死亡 UI、重载场景等
        // 示例：UnityEngine.SceneManagement.SceneManager.LoadScene(
        //     UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
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