using UnityEngine;
using UnityEngine.SceneManagement;
using System;

/// <summary>
/// 全局游戏管理器：控制游戏状态、阶段切换、场景过渡。
/// Phase 2 奖惩结算 + clearedCount + 武器解锁。
/// 跨场景持久存在 (DontDestroyOnLoad)。
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    // ────────────────── 游戏阶段 ──────────────────
    public enum GamePhase
    {
        MainMenu,
        Phase1_2D,
        Phase2_GridPuzzle,
        Phase3_BossFight,
        GameOver
    }

    [Header("=== 游戏状态 ===")]
    [SerializeField] private GamePhase currentPhase = GamePhase.MainMenu;
    public GamePhase CurrentPhase => currentPhase;

    [Header("=== 场景名称（面板可配） ===")]
    [SerializeField] private string mainMenuScene = "MainMenu";
    [SerializeField] private string phase1Scene = "Level1_2D";
    [SerializeField] private string phase2Scene = "Level2_GridPuzzle";
    [SerializeField] private string phase3Scene = "Level3_Boss";

    // ────────────────── 过渡 ──────────────────
    [HideInInspector] public bool isTransitioning = false;

    [Header("=== 转场参数 ===")]
    [SerializeField] private float transitionDelay = 1f;

    // ────────────────── Phase 2 结算 ──────────────────
    [Header("=== Phase 2 结算 ===")]
    [Tooltip("Phase 2 已通关次数")]
    [SerializeField] private int phase2ClearedCount = 0;
    public int Phase2ClearedCount => phase2ClearedCount;

    [Tooltip("第1次通关奖励经验")]
    [SerializeField] private float firstClearExp = 500f;
    [Tooltip("第3次通关奖励经验")]
    [SerializeField] private float thirdClearExp = 2000f;

    // ────────────────── 武器解锁 ──────────────────
    [Header("=== 武器解锁状态 ===")]
    [SerializeField] private bool bowUnlocked = false;
    [SerializeField] private bool waterBombUnlocked = false;
    public bool BowUnlocked => bowUnlocked;
    public bool WaterBombUnlocked => waterBombUnlocked;

    // ────────────────── Phase 1 坐标保存（用于 Phase 2 失败后返回原地） ──────────────────
    [HideInInspector] public Vector3 savedPhase1Position;
    [HideInInspector] public bool hasSavedPhase1Position = false;

    public void SavePhase1Position(Vector3 pos)
    {
        savedPhase1Position = pos;
        hasSavedPhase1Position = true;
        Debug.Log($"[GameManager] 已保存 Phase 1 坐标: {pos}");
    }

    // ────────────────── 当前武器 ──────────────────
    public enum WeaponType { Melee, Bow, WaterBomb }
    private WeaponType currentWeapon = WeaponType.Melee;
    public WeaponType CurrentWeapon => currentWeapon;

    // ────────────────── 事件 ──────────────────
    public event Action<WeaponType> OnWeaponChanged;
    public event Action<string> OnWeaponUnlocked;   // 参数: 武器名
    public event Action<float> OnExpRewarded;        // 参数: 经验量

    // ══════════════════ 生命周期 ══════════════════

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // ══════════════════ 阶段切换 ══════════════════

    public void GoToPhase(GamePhase phase)
    {
        if (isTransitioning) return;
        currentPhase = phase;

        string sceneName = phase switch
        {
            GamePhase.MainMenu       => mainMenuScene,
            GamePhase.Phase1_2D      => phase1Scene,
            GamePhase.Phase2_GridPuzzle => phase2Scene,
            GamePhase.Phase3_BossFight  => phase3Scene,
            _ => mainMenuScene
        };

        StartCoroutine(TransitionToScene(sceneName));
    }

    System.Collections.IEnumerator TransitionToScene(string sceneName)
    {
        isTransitioning = true;
        yield return new WaitForSeconds(transitionDelay);
        SceneManager.LoadScene(sceneName);
        isTransitioning = false;
    }

    // ══════════════════ Phase 2 结算 ══════════════════

    /// <summary>
    /// Phase 2 失败：回到 Phase 1（钩锁触发点之前）
    /// </summary>
    public void OnPhase2Failed()
    {
        Debug.Log("[GameManager] Phase 2 失败，返回第一阶段。");
        GoToPhase(GamePhase.Phase1_2D);
    }

    /// <summary>
    /// Phase 2 胜利：根据 clearedCount 发放奖励，然后返回 Phase 1
    /// </summary>
    public void OnPhase2Victory()
    {
        phase2ClearedCount++;
        Debug.Log($"[GameManager] Phase 2 第 {phase2ClearedCount} 次通关！");

        switch (phase2ClearedCount)
        {
            case 1:
                Debug.Log($"奖励：普通经验 {firstClearExp}");
                OnExpRewarded?.Invoke(firstClearExp);
                break;
            case 2:
                bowUnlocked = true;
                Debug.Log("奖励：解锁武器【弓箭】！");
                OnWeaponUnlocked?.Invoke("弓箭");
                break;
            case 3:
                Debug.Log($"奖励：大量经验 {thirdClearExp}");
                OnExpRewarded?.Invoke(thirdClearExp);
                break;
            case 4:
                waterBombUnlocked = true;
                Debug.Log("奖励：解锁武器【水魔爆】！");
                OnWeaponUnlocked?.Invoke("水魔爆");
                break;
            default:
                // 第5次及以上：给一些经验
                float bonusExp = firstClearExp * phase2ClearedCount;
                Debug.Log($"奖励：经验 {bonusExp}");
                OnExpRewarded?.Invoke(bonusExp);
                break;
        }

        // 返回 Phase 1
        GoToPhase(GamePhase.Phase1_2D);
    }

    // ══════════════════ 武器切换 ══════════════════

    public void SwitchWeapon(WeaponType weapon)
    {
        // 检查是否已解锁
        if (weapon == WeaponType.Bow && !bowUnlocked) return;
        if (weapon == WeaponType.WaterBomb && !waterBombUnlocked) return;

        currentWeapon = weapon;
        Debug.Log($"切换武器 → {weapon}");
        OnWeaponChanged?.Invoke(weapon);
    }

    // ══════════════════ 快捷方法 ══════════════════

    public void StartPhase1() => GoToPhase(GamePhase.Phase1_2D);
    public void StartPhase2() => GoToPhase(GamePhase.Phase2_GridPuzzle);
    public void StartPhase3() => GoToPhase(GamePhase.Phase3_BossFight);
    public void ReturnToMenu() => GoToPhase(GamePhase.MainMenu);
}