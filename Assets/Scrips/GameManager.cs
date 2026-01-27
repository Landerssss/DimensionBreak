using UnityEngine;
using UnityEngine.Events;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("=== RPG 数值系统 ===")]
    public int currentLevel = 1;
    public float currentExp = 0;
    public float expToNextLevel = 100f; 
    public float expMultiplier = 1.0f;  

    [Header("=== 技能解锁门槛 ===")]
    public int dashUnlockLevel = 20; 
    public int diveUnlockLevel = 50; 

    [Header("=== 游戏阶段状态 ===")]
    // 【关键修复】确保这行代码存在！
    public bool isPhase1 = true;      
    public bool isTransitioning = false; 

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void AddExp(float amount)
    {
        float finalExp = amount * expMultiplier;
        currentExp += finalExp;

        while (currentExp >= expToNextLevel)
        {
            currentExp -= expToNextLevel;
            LevelUp();
        }
    }

    void LevelUp()
    {
        currentLevel++;
        Debug.Log($"升级！当前等级: {currentLevel}");

        if (currentLevel >= 8 && expMultiplier == 1.0f)
        {
            expMultiplier = 3.0f; 
            Debug.Log("获得被动：经验获取速度 +200%！");
        }
    }

    public bool CanUseDash() => currentLevel >= dashUnlockLevel;
    public bool CanUseDive() => currentLevel >= diveUnlockLevel;
}