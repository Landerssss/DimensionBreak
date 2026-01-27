using UnityEngine;
using UnityEngine.Events;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("=== RPG 属性 ===")]
    public int currentLevel = 1;
    public float currentExp = 0;
    public float expMultiplier = 1.0f; // 经验倍率（初始1倍）

    [Header("=== 技能解锁阈值 ===")]
    public int dashUnlockLevel = 20;   // 20级解锁突刺
    public int diveUnlockLevel = 50;   // 50级解锁下坠

    [Header("=== 游戏状态 ===")]
    public bool isPhase1Complete = false;
    public bool isTransitioning = false;

    // 事件：当升级时通知UI更新
    public UnityEvent<int> onLevelUp;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    // 增加经验（核心爽点逻辑）
    public void AddExp(float amount)
    {
        // 应用倍率
        float finalExp = amount * expMultiplier;
        currentExp += finalExp;

        // 简单粗暴的升级逻辑：每100经验升1级 (或者你可以写更复杂的公式)
        // 这里为了配合你的需求：打一个怪升8级。假设怪给800经验。
        while (currentExp >= 100) 
        {
            LevelUp();
            currentExp -= 100;
        }
    }

    void LevelUp()
    {
        currentLevel++;
        Debug.Log($"升级了！当前等级: {currentLevel}");

        // 第一次打怪后的特殊奖励：经验倍率永久+200%
        if (currentLevel >= 8 && expMultiplier == 1.0f) 
        {
            expMultiplier = 3.0f; // 1 + 200% = 3倍
            Debug.Log("获得被动：经验获取速度 +200%！");
        }

        onLevelUp?.Invoke(currentLevel);
    }

    // 查询技能是否解锁
    public bool IsDashUnlocked() => currentLevel >= dashUnlockLevel;
    public bool IsDiveUnlocked() => currentLevel >= diveUnlockLevel;
}