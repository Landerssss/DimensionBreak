using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using TMPro;

/// <summary>
/// 主菜单管理器 — "时间流转 (Time Cycle)" 版本
/// 
/// 三张全屏图片 (Day / Dusk / Night) 水平拼接在 slidingContainer 中，
/// 通过移动容器的 anchoredPosition.x 实现平滑状态切换。
/// 
/// 状态对应功能：
///   Night — 标题画面（游戏启动默认状态）
///   Day   — 设置 + 开始游戏
///   Dusk  — 退出游戏 + 确认弹窗
/// 
/// 所有数值均通过 [SerializeField] 暴露到 Inspector，绝不硬编码。
/// </summary>
public class MainMenuManager : MonoBehaviour
{
    // ═══════════════════════════════════════════════
    //  状态定义
    // ═══════════════════════════════════════════════

    public enum MenuState
    {
        Night,  // 标题画面（初始）
        Day,    // 设置 + 开始游戏
        Dusk    // 退出确认
    }

    // ═══════════════════════════════════════════════
    //  序列化字段 — 全部暴露到 Inspector
    // ═══════════════════════════════════════════════

    // ────────────────── 滑动容器 ──────────────────
    [Header("=== 滑动容器 ===")]
    [Tooltip("三张图片共同的父级 RectTransform，通过移动它来切换界面")]
    [SerializeField] private RectTransform slidingContainer;

    // ────────────────── 三张图片 ──────────────────
    [Header("=== 三张图片 RectTransform ===")]
    [SerializeField] private RectTransform dayImage;
    [SerializeField] private RectTransform duskImage;
    [SerializeField] private RectTransform nightImage;

    // ────────────────── 拼接位置 ──────────────────
    [Header("=== 图片拼接位置 (X 轴) ===")]
    [Tooltip("Night 状态时 slidingContainer 的 X 坐标")]
    [SerializeField] private float nightPositionX = 0f;
    [Tooltip("Day 状态时 slidingContainer 的 X 坐标")]
    [SerializeField] private float dayPositionX = -1920f;
    [Tooltip("Dusk 状态时 slidingContainer 的 X 坐标")]
    [SerializeField] private float duskPositionX = 1920f;

    // ────────────────── 过渡效果 ──────────────────
    [Header("=== 滑动过渡 ===")]
    [Tooltip("状态切换的滑动时长（秒）")]
    [SerializeField] private float transitionDuration = 0.8f;
    [Tooltip("过渡的缓动曲线")]
    [SerializeField] private AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    // ────────────────── 导航按钮（三角） ──────────────────
    [Header("=== 导航三角按钮 ===")]
    [Tooltip("右侧大三角按钮")]
    [SerializeField] private Button rightTriangleButton;
    [Tooltip("左侧大三角按钮")]
    [SerializeField] private Button leftTriangleButton;
    [Tooltip("右侧三角上的文字标签（可选）")]
    [SerializeField] private TMP_Text rightTriangleLabel;
    [Tooltip("左侧三角上的文字标签（可选）")]
    [SerializeField] private TMP_Text leftTriangleLabel;

    // ────────────────── 退出确认弹窗 (Dusk) ──────────────────
    [Header("=== 退出确认弹窗 (Dusk 状态) ===")]
    [SerializeField] private GameObject quitConfirmPanel;
    [SerializeField] private Button quitYesButton;
    [SerializeField] private Button quitNoButton;

    // ────────────────── 功能按钮 (Day) ──────────────────
    [Header("=== 开始游戏 (Day 状态) ===")]
    [SerializeField] private Button startGameButton;

    // ────────────────── 转场效果（开始游戏） ──────────────────
    [Header("=== 开始游戏转场 ===")]
    [SerializeField] private Camera mainCamera;
    [Tooltip("景深放大的目标 Orthographic Size")]
    [SerializeField] private float zoomTargetSize = 2f;
    [Tooltip("景深放大速度")]
    [SerializeField] private float zoomSpeed = 3f;
    [Tooltip("放大完成后等待多少秒再切换场景")]
    [SerializeField] private float delayAfterZoom = 0.5f;

    // ────────────────── 设置控件 (Day) ──────────────────
    [Header("=== 设置面板 (Day 状态) ===")]
    [Tooltip("设置控件的父容器，仅在 Day 状态显示")]
    [SerializeField] private GameObject settingsContainer;
    [SerializeField] private Toggle fullscreenToggle;
    [SerializeField] private Toggle vsyncToggle;
    [SerializeField] private Slider volumeSlider;
    [SerializeField] private TMP_Dropdown resolutionDropdown;

    // ────────────────── Night 状态 UI ──────────────────
    [Header("=== 标题 UI (Night 状态) ===")]
    [Tooltip("标题界面的 UI 容器，仅在 Night 状态显示")]
    [SerializeField] private GameObject titleContainer;

    // ────────────────── 音频 ──────────────────
    [Header("=== 音频 ===")]
    [SerializeField] private AudioSource bgmSource;
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioClip transitionSFX;
    [SerializeField] private AudioClip buttonClickSFX;

    // ────────────────── 三角按钮标签配置 ──────────────────
    [Header("=== 三角标签文字 ===")]
    [Tooltip("Night 状态时右侧三角显示的文字")]
    [SerializeField] private string nightRightLabel = "Setting";
    [Tooltip("Night 状态时左侧三角显示的文字")]
    [SerializeField] private string nightLeftLabel = "Exit";
    [Tooltip("Day 状态时右侧三角显示的文字")]
    [SerializeField] private string dayRightLabel = "Exit";
    [Tooltip("Day 状态时左侧三角显示的文字")]
    [SerializeField] private string dayLeftLabel = "Title";
    [Tooltip("Dusk 状态时右侧三角显示的文字")]
    [SerializeField] private string duskRightLabel = "Title";
    [Tooltip("Dusk 状态时左侧三角显示的文字")]
    [SerializeField] private string duskLeftLabel = "Setting";

    // ═══════════════════════════════════════════════
    //  内部状态
    // ═══════════════════════════════════════════════

    private MenuState currentState = MenuState.Night;
    private bool isTransitioning = false;
    private float originalCameraSize;
    private Resolution[] availableResolutions;
    private Coroutine activeTransition;

    /// <summary>当前菜单状态（只读）</summary>
    public MenuState CurrentState => currentState;

    // ═══════════════════════════════════════════════
    //  循环顺序定义
    // ═══════════════════════════════════════════════
    //  顺时针（→）: Night → Day → Dusk → Night
    //  逆时针（←）: Night → Dusk → Day → Night

    private static readonly MenuState[] clockwiseOrder = { MenuState.Night, MenuState.Day, MenuState.Dusk };

    // ═══════════════════════════════════════════════
    //  生命周期
    // ═══════════════════════════════════════════════

    void Start()
    {
        // 记录相机原始大小
        if (mainCamera != null)
            originalCameraSize = mainCamera.orthographicSize;

        // 初始状态：Night
        currentState = MenuState.Night;
        SetContainerPositionImmediate(GetStatePositionX(MenuState.Night));
        UpdateUIForState(MenuState.Night);

        // ── 绑定导航按钮 ──
        if (rightTriangleButton != null)
            rightTriangleButton.onClick.AddListener(OnRightTriangleClicked);
        if (leftTriangleButton != null)
            leftTriangleButton.onClick.AddListener(OnLeftTriangleClicked);

        // ── 绑定退出确认弹窗 ──
        if (quitYesButton != null)
            quitYesButton.onClick.AddListener(OnQuitConfirmed);
        if (quitNoButton != null)
            quitNoButton.onClick.AddListener(OnQuitCancelled);

        // ── 绑定开始游戏 ──
        if (startGameButton != null)
            startGameButton.onClick.AddListener(OnStartGame);

        // ── 初始化设置控件 ──
        InitFullscreenToggle();
        InitVsyncToggle();
        InitVolumeSlider();
        InitResolutionDropdown();

        // 退出确认弹窗默认隐藏
        if (quitConfirmPanel != null)
            quitConfirmPanel.SetActive(false);
    }

    // ═══════════════════════════════════════════════
    //  导航按钮回调
    // ═══════════════════════════════════════════════

    /// <summary>右侧三角：顺时针切换 Night→Day→Dusk→Night</summary>
    void OnRightTriangleClicked()
    {
        if (isTransitioning) return;
        PlayClickSFX();

        MenuState next = GetNextState(currentState, +1);
        StartTransition(next);
    }

    /// <summary>左侧三角：逆时针切换 Night→Dusk→Day→Night</summary>
    void OnLeftTriangleClicked()
    {
        if (isTransitioning) return;
        PlayClickSFX();

        MenuState prev = GetNextState(currentState, -1);
        StartTransition(prev);
    }

    // ═══════════════════════════════════════════════
    //  状态切换核心
    // ═══════════════════════════════════════════════

    /// <summary>启动向目标状态的滑动过渡</summary>
    public void StartTransition(MenuState targetState)
    {
        if (isTransitioning) return;
        if (targetState == currentState) return;

        if (activeTransition != null)
            StopCoroutine(activeTransition);

        activeTransition = StartCoroutine(TransitionTo(targetState));
    }

    /// <summary>
    /// 核心过渡协程：平滑移动 slidingContainer 到目标状态的 X 位置
    /// </summary>
    private IEnumerator TransitionTo(MenuState targetState)
    {
        isTransitioning = true;

        // 播放过渡音效
        PlayTransitionSFX();

        float targetX = GetStatePositionX(targetState);
        float startX = slidingContainer.anchoredPosition.x;
        float elapsed = 0f;

        while (elapsed < transitionDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / transitionDuration);
            float curveT = transitionCurve.Evaluate(t);
            float newX = Mathf.Lerp(startX, targetX, curveT);

            slidingContainer.anchoredPosition = new Vector2(
                newX,
                slidingContainer.anchoredPosition.y
            );

            yield return null;
        }

        // 确保精确到位
        slidingContainer.anchoredPosition = new Vector2(
            targetX,
            slidingContainer.anchoredPosition.y
        );

        currentState = targetState;
        UpdateUIForState(targetState);

        isTransitioning = false;
        activeTransition = null;
    }

    // ═══════════════════════════════════════════════
    //  UI 状态联动
    // ═══════════════════════════════════════════════

    /// <summary>根据当前状态切换各 UI 容器的显示/隐藏并更新三角标签</summary>
    private void UpdateUIForState(MenuState state)
    {
        // ── 容器显隐 ──
        if (titleContainer != null)
            titleContainer.SetActive(state == MenuState.Night);

        if (settingsContainer != null)
            settingsContainer.SetActive(state == MenuState.Day);

        if (startGameButton != null)
            startGameButton.gameObject.SetActive(state == MenuState.Day);

        if (quitConfirmPanel != null)
            quitConfirmPanel.SetActive(state == MenuState.Dusk);

        // ── 更新三角标签文字 ──
        UpdateTriangleLabels(state);
    }

    /// <summary>根据状态更新左右三角按钮上的文字</summary>
    private void UpdateTriangleLabels(MenuState state)
    {
        string rightText = "";
        string leftText = "";

        switch (state)
        {
            case MenuState.Night:
                rightText = nightRightLabel;  // "Setting"
                leftText = nightLeftLabel;    // "Exit"
                break;
            case MenuState.Day:
                rightText = dayRightLabel;    // "Exit"
                leftText = dayLeftLabel;      // "Title"
                break;
            case MenuState.Dusk:
                rightText = duskRightLabel;   // "Title"
                leftText = duskLeftLabel;     // "Setting"
                break;
        }

        if (rightTriangleLabel != null)
            rightTriangleLabel.text = rightText;
        if (leftTriangleLabel != null)
            leftTriangleLabel.text = leftText;
    }

    // ═══════════════════════════════════════════════
    //  功能回调
    // ═══════════════════════════════════════════════

    // ────────────────── 开始游戏 (Day) ──────────────────

    void OnStartGame()
    {
        if (isTransitioning) return;
        PlayClickSFX();
        StartCoroutine(StartGameTransition());
    }

    /// <summary>景深放大转场 → 加载 Phase 1</summary>
    private IEnumerator StartGameTransition()
    {
        isTransitioning = true;

        // 景深放大效果
        if (mainCamera != null)
        {
            while (Mathf.Abs(mainCamera.orthographicSize - zoomTargetSize) > 0.05f)
            {
                mainCamera.orthographicSize = Mathf.Lerp(
                    mainCamera.orthographicSize,
                    zoomTargetSize,
                    Time.deltaTime * zoomSpeed
                );
                yield return null;
            }
            mainCamera.orthographicSize = zoomTargetSize;
        }

        yield return new WaitForSeconds(delayAfterZoom);

        // 通过 GameManager 启动 Phase 1
        if (GameManager.Instance != null)
        {
            GameManager.Instance.StartPhase1();
        }
        else
        {
            Debug.LogWarning("[MainMenuManager] GameManager.Instance 为 null，无法启动 Phase 1");
        }

        isTransitioning = false;
    }

    // ────────────────── 退出游戏 (Dusk) ──────────────────

    void OnQuitConfirmed()
    {
        PlayClickSFX();
        Debug.Log("[MainMenuManager] 玩家确认退出游戏");

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void OnQuitCancelled()
    {
        PlayClickSFX();
        // 取消退出 → 滑回 Night（标题画面）
        StartTransition(MenuState.Night);
    }

    // ═══════════════════════════════════════════════
    //  设置功能（保留自原版）
    // ═══════════════════════════════════════════════

    // --- 全屏 ---
    void InitFullscreenToggle()
    {
        if (fullscreenToggle == null) return;
        fullscreenToggle.isOn = Screen.fullScreen;
        fullscreenToggle.onValueChanged.AddListener(SetFullscreen);
    }

    void SetFullscreen(bool isFullscreen)
    {
        Screen.fullScreen = isFullscreen;
    }

    // --- 垂直同步 ---
    void InitVsyncToggle()
    {
        if (vsyncToggle == null) return;
        vsyncToggle.isOn = QualitySettings.vSyncCount > 0;
        vsyncToggle.onValueChanged.AddListener(SetVSync);
    }

    void SetVSync(bool enabled)
    {
        QualitySettings.vSyncCount = enabled ? 1 : 0;
    }

    // --- 音量 ---
    void InitVolumeSlider()
    {
        if (volumeSlider == null) return;
        volumeSlider.value = AudioListener.volume;
        volumeSlider.onValueChanged.AddListener(SetVolume);
    }

    void SetVolume(float value)
    {
        AudioListener.volume = value;
        if (bgmSource != null)
            bgmSource.volume = value;
    }

    // --- 分辨率 ---
    void InitResolutionDropdown()
    {
        if (resolutionDropdown == null) return;

        availableResolutions = Screen.resolutions;
        resolutionDropdown.ClearOptions();

        List<string> options = new List<string>();
        int currentIndex = 0;

        for (int i = 0; i < availableResolutions.Length; i++)
        {
            Resolution r = availableResolutions[i];
            string label = $"{r.width} x {r.height} @ {r.refreshRateRatio.value:F0}Hz";
            options.Add(label);

            if (r.width == Screen.currentResolution.width &&
                r.height == Screen.currentResolution.height)
            {
                currentIndex = i;
            }
        }

        resolutionDropdown.AddOptions(options);
        resolutionDropdown.value = currentIndex;
        resolutionDropdown.RefreshShownValue();
        resolutionDropdown.onValueChanged.AddListener(SetResolution);
    }

    void SetResolution(int index)
    {
        if (availableResolutions == null || index >= availableResolutions.Length) return;
        Resolution r = availableResolutions[index];
        Screen.SetResolution(r.width, r.height, Screen.fullScreen);
    }

    // ═══════════════════════════════════════════════
    //  工具方法
    // ═══════════════════════════════════════════════

    /// <summary>获取指定状态对应的 slidingContainer X 轴坐标</summary>
    private float GetStatePositionX(MenuState state)
    {
        return state switch
        {
            MenuState.Night => nightPositionX,
            MenuState.Day   => dayPositionX,
            MenuState.Dusk  => duskPositionX,
            _ => nightPositionX
        };
    }

    /// <summary>按方向获取下一个循环状态 (+1=顺时针, -1=逆时针)</summary>
    private MenuState GetNextState(MenuState current, int direction)
    {
        int currentIndex = System.Array.IndexOf(clockwiseOrder, current);
        int nextIndex = (currentIndex + direction + clockwiseOrder.Length) % clockwiseOrder.Length;
        return clockwiseOrder[nextIndex];
    }

    /// <summary>立即设置 slidingContainer 位置（无动画）</summary>
    private void SetContainerPositionImmediate(float x)
    {
        if (slidingContainer != null)
        {
            slidingContainer.anchoredPosition = new Vector2(
                x,
                slidingContainer.anchoredPosition.y
            );
        }
    }

    /// <summary>播放按钮点击音效</summary>
    private void PlayClickSFX()
    {
        if (sfxSource != null && buttonClickSFX != null)
            sfxSource.PlayOneShot(buttonClickSFX);
    }

    /// <summary>播放界面切换过渡音效</summary>
    private void PlayTransitionSFX()
    {
        if (sfxSource != null && transitionSFX != null)
            sfxSource.PlayOneShot(transitionSFX);
    }

    // ═══════════════════════════════════════════════
    //  公开 API（供外部 / Timeline 调用）
    // ═══════════════════════════════════════════════

    /// <summary>直接跳转到指定状态（无动画）</summary>
    public void SetStateImmediate(MenuState state)
    {
        currentState = state;
        SetContainerPositionImmediate(GetStatePositionX(state));
        UpdateUIForState(state);
    }

    /// <summary>当前是否正在过渡中</summary>
    public bool IsTransitioning => isTransitioning;
}
