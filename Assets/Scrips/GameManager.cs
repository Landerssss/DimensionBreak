using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 全局游戏管理器：控制游戏状态、阶段切换和场景过渡。
/// 跨场景持久存在 (DontDestroyOnLoad)。
/// 数值系统已迁移至 PlayerStats。
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    // ────────────────── 游戏阶段 ──────────────────
    public enum GamePhase
    {
        MainMenu,
        Phase1_2D,
        Phase2_GravityRun,
        Phase3_BossFight,
        GameOver
    }

    [Header("=== 游戏状态 ===")]
    [SerializeField] private GamePhase currentPhase = GamePhase.MainMenu;
    public GamePhase CurrentPhase => currentPhase;

    [Header("=== 场景名称（面板可配） ===")]
    [SerializeField] private string mainMenuScene = "MainMenu";
    [SerializeField] private string phase1Scene = "Level1_2D";
    [SerializeField] private string phase2Scene = "Level2_Gravity";
    [SerializeField] private string phase3Scene = "Level3_Boss";

    // ────────────────── 过渡状态 ──────────────────
    [HideInInspector] public bool isTransitioning = false;

    // ────────────────── 转场参数 ──────────────────
    [Header("=== 转场参数 ===")]
    [SerializeField] private float transitionDelay = 1f;

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

    /// <summary>
    /// 切换到指定阶段并加载对应场景
    /// </summary>
    public void GoToPhase(GamePhase phase)
    {
        if (isTransitioning) return;
        currentPhase = phase;

        string sceneName = phase switch
        {
            GamePhase.MainMenu => mainMenuScene,
            GamePhase.Phase1_2D => phase1Scene,
            GamePhase.Phase2_GravityRun => phase2Scene,
            GamePhase.Phase3_BossFight => phase3Scene,
            _ => mainMenuScene
        };

        StartCoroutine(TransitionToScene(sceneName));
    }

    System.Collections.IEnumerator TransitionToScene(string sceneName)
    {
        isTransitioning = true;

        // TODO: 在这里播放转场动画 / 遮罩淡入
        yield return new WaitForSeconds(transitionDelay);

        SceneManager.LoadScene(sceneName);
        isTransitioning = false;
    }

    // ══════════════════ 快捷方法 ══════════════════

    public void StartPhase1() => GoToPhase(GamePhase.Phase1_2D);
    public void StartPhase2() => GoToPhase(GamePhase.Phase2_GravityRun);
    public void StartPhase3() => GoToPhase(GamePhase.Phase3_BossFight);
    public void ReturnToMenu() => GoToPhase(GamePhase.MainMenu);
}