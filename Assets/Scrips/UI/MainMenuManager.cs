using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using TMPro;

/// <summary>
/// 主菜单管理器 — "基于径向遮罩（Radial Mask）的 UI 容器旋转轮转" 版本
///
/// 屏幕被视觉上划分为三个区域：
///   Left  (小三角) — 触发顺时针旋转
///   Center(主体)   — 当前激活状态
///   Right (大三角) — 触发逆时针旋转
///
/// 状态对应功能：
///   Night (index=2) — 标题画面（游戏启动默认状态）
///   Day   (index=0) — 设置
///   Dusk  (index=1) — 退出游戏确认
///
/// 核心逻辑：
/// - 废弃单纯图片淡入淡出，使用径向容器（zoneContainers）配合旋转。
/// - 对内层 UI（uiPanels）做反向旋转补偿，确保文字/按钮始终端正。
/// </summary>
public class MainMenuManager : MonoBehaviour
{
    // ═══════════════════════════════════════════════
    //  状态定义
    // ═══════════════════════════════════════════════

    public enum MenuState
    {
        Day   = 0,  // 设置 + 开始游戏
        Dusk  = 1,  // 退出确认
        Night = 2   // 标题画面（初始）
    }

    // ═══════════════════════════════════════════════
    //  序列化字段 — 全部暴露到 Inspector
    // ═══════════════════════════════════════════════

    // ────────────────── 径向轮转容器 ──────────────────
    [Header("=== 径向轮转容器 ===")]
    [Tooltip("对应三个旋转遮罩槽位 (Day, Dusk, Night)")]
    [SerializeField] private RectTransform[] zoneContainers;
    [Tooltip("对应 Day/Dusk/Night 下的实际交互 UI")]
    [SerializeField] private CanvasGroup[] uiPanels;

    // ────────────────── 初始状态 ──────────────────
    [Header("=== 初始状态 ===")]
    [Tooltip("游戏启动时的默认状态")]
    [SerializeField] private MenuState initialState = MenuState.Night;

    // ────────────────── 过渡效果 ──────────────────
    [Header("=== 轮转过渡 ===")]
    [Tooltip("轮转过渡的总时长（秒）")]
    [SerializeField] private float transitionDuration = 0.5f;
    [Tooltip("旋转缓动曲线")]
    [SerializeField] private AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    // ────────────────── 区域导航按钮 ──────────────────
    [Header("=== 区域按钮 ===")]
    [Tooltip("右侧大三角区域按钮 — 触发逆时针轮转")]
    [SerializeField] private Button rightZoneButton;
    [Tooltip("左侧小三角区域按钮 — 触发顺时针轮转")]
    [SerializeField] private Button leftZoneButton;

    // ────────────────── 区域标签 ──────────────────
    [Header("=== 区域标签文字 ===")]
    [Tooltip("右侧三角上的文字标签")]
    [SerializeField] private TMP_Text rightZoneLabel;
    [Tooltip("左侧三角上的文字标签")]
    [SerializeField] private TMP_Text leftZoneLabel;
    [Tooltip("中央区域的标题/状态文字")]
    [SerializeField] private TMP_Text centerLabel;

    // ────────────────── 退出确认弹窗 (Dusk) ──────────────────
    [Header("=== 退出确认弹窗 (Dusk 状态) ===")]
    [SerializeField] private Button quitYesButton;
    [SerializeField] private Button quitNoButton;

    // ────────────────── 功能按钮 (Day) ──────────────────
    [Header("=== 开始游戏 (Night 状态) ===")]
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
    [SerializeField] private Toggle fullscreenToggle;
    [SerializeField] private Toggle vsyncToggle;
    [SerializeField] private Slider volumeSlider;
    [SerializeField] private TMP_Dropdown resolutionDropdown;

    // ────────────────── 音频 ──────────────────
    [Header("=== 音频 ===")]
    [SerializeField] private AudioSource bgmSource;
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioClip transitionSFX;
    [SerializeField] private AudioClip buttonClickSFX;

    // ────────────────── 标签文字配置 ──────────────────
    [Header("=== 各状态标签文字 ===")]
    [SerializeField] private string labelDay   = "Setting";
    [SerializeField] private string labelDusk  = "Exit";
    [SerializeField] private string labelNight = "Title";

    // ═══════════════════════════════════════════════
    //  内部状态
    // ═══════════════════════════════════════════════

    /// <summary>当前中央区域显示的背景索引 (0=Day, 1=Dusk, 2=Night)</summary>
    private int centerIndex;
    private bool isTransitioning = false;
    private float originalCameraSize;
    private Resolution[] availableResolutions;
    private Coroutine activeTransition;

    /// <summary>当前菜单状态（只读）</summary>
    public MenuState CurrentState => (MenuState)centerIndex;
    /// <summary>当前是否正在过渡中</summary>
    public bool IsTransitioning => isTransitioning;

    // ═══════════════════════════════════════════════
    //  生命周期
    // ═══════════════════════════════════════════════

    void Start()
    {
        // 记录相机原始大小
        if (mainCamera != null)
            originalCameraSize = mainCamera.orthographicSize;

        // 设置初始索引
        centerIndex = (int)initialState;

        // 立即同步容器角度与交互状态
        SyncUIContainers();
        UpdateZoneLabels();

        // ── 绑定区域按钮 ──
        if (rightZoneButton != null)
            rightZoneButton.onClick.AddListener(OnRightZoneClicked);
        if (leftZoneButton != null)
            leftZoneButton.onClick.AddListener(OnLeftZoneClicked);

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
    }

    // ═══════════════════════════════════════════════
    //  区域按钮回调
    // ═══════════════════════════════════════════════

    /// <summary>
    /// 右区域大三角点击 → 逆时针轮转（索引 +1）
    /// </summary>
    void OnRightZoneClicked()
    {
        if (isTransitioning) return;
        PlayClickSFX();
        int targetIndex = (centerIndex + 1) % 3;
        RotateTo(targetIndex, +1);
    }

    /// <summary>
    /// 左区域小三角点击 → 顺时针轮转（索引 -1）
    /// </summary>
    void OnLeftZoneClicked()
    {
        if (isTransitioning) return;
        PlayClickSFX();
        int targetIndex = (centerIndex - 1 + 3) % 3;
        RotateTo(targetIndex, -1);
    }

    // ═══════════════════════════════════════════════
    //  轮转核心
    // ═══════════════════════════════════════════════

    private void RotateTo(int targetIndex, int direction)
    {
        if (activeTransition != null)
            StopCoroutine(activeTransition);

        activeTransition = StartCoroutine(TransitionRoutine(targetIndex, direction));
    }

    /// <summary>
    /// 径向旋转轮转协程：
    /// 在 transitionDuration 时间内，插值改变 zoneContainers 的 localRotation，
    /// 并对 uiPanels 应用反向旋转补偿。
    /// </summary>
    private IEnumerator TransitionRoutine(int targetIndex, int direction)
    {
        isTransitioning = true;
        PlayTransitionSFX();

        // 旋转开始：禁用所有交互，防止误操作
        if (uiPanels != null)
        {
            foreach (var panel in uiPanels)
            {
                if (panel != null) panel.blocksRaycasts = false;
            }
        }

        // 准备起止角度
        int containerCount = zoneContainers != null ? zoneContainers.Length : 0;
        float[] startAngles = new float[containerCount];
        float[] targetAngles = new float[containerCount];
        float rotationAmount = direction * 120f;

        for (int i = 0; i < containerCount; i++)
        {
            if (zoneContainers[i] != null)
            {
                startAngles[i] = zoneContainers[i].localEulerAngles.z;
                // 将 0~360 的角度调整为更连续的表示，防止 Lerp 时越界翻转
                if (startAngles[i] > 180f) startAngles[i] -= 360f;
                targetAngles[i] = startAngles[i] + rotationAmount;
            }
        }

        float elapsed = 0f;
        while (elapsed < transitionDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / transitionDuration);
            float curveT = transitionCurve.Evaluate(t);

            for (int i = 0; i < containerCount; i++)
            {
                if (zoneContainers[i] != null)
                {
                    float currentZ = Mathf.Lerp(startAngles[i], targetAngles[i], curveT);
                    zoneContainers[i].localRotation = Quaternion.Euler(0, 0, currentZ);
                    
                    // 补偿 UI 旋转：保持文字和按钮不随遮罩倾斜
                    if (uiPanels != null && i < uiPanels.Length && uiPanels[i] != null)
                    {
                        uiPanels[i].transform.localRotation = Quaternion.Euler(0, 0, -currentZ);
                    }
                }
            }

            yield return null;
        }

        // 最终修正对齐
        centerIndex = targetIndex;
        SyncUIContainers();
        UpdateZoneLabels();

        isTransitioning = false;
        activeTransition = null;
    }

    // ═══════════════════════════════════════════════
    //  UI 状态同步
    // ═══════════════════════════════════════════════

    /// <summary>
    /// 直接同步所有的容器角度，并根据当前 centerIndex 设置 UI 交互性
    /// </summary>
    private void SyncUIContainers()
    {
        if (zoneContainers == null || uiPanels == null) return;

        for (int i = 0; i < zoneContainers.Length; i++)
        {
            if (zoneContainers[i] == null) continue;

            // 根据中心索引计算目标角度，确保 centerIndex 对应的容器在 0 度
            float angle = (centerIndex - i) * 120f;
            zoneContainers[i].localRotation = Quaternion.Euler(0, 0, angle);

            if (i < uiPanels.Length && uiPanels[i] != null)
            {
                // 反向补偿
                uiPanels[i].transform.localRotation = Quaternion.Euler(0, 0, -angle);
                
                // 仅开启当前中心面板的交互权限
                uiPanels[i].blocksRaycasts = (i == centerIndex);
            }
        }
    }

    /// <summary>
    /// 根据当前中心状态更新标签文字。
    /// </summary>
    private void UpdateZoneLabels()
    {
        int count = 3;
        int leftIndex  = (centerIndex - 1 + count) % count;
        int rightIndex = (centerIndex + 1) % count;

        if (leftZoneLabel != null)
            leftZoneLabel.text = GetLabelForIndex(leftIndex);
        if (rightZoneLabel != null)
            rightZoneLabel.text = GetLabelForIndex(rightIndex);
        if (centerLabel != null)
            centerLabel.text = GetLabelForIndex(centerIndex);
    }

    private string GetLabelForIndex(int index)
    {
        MenuState state = (MenuState)index;
        return state switch
        {
            MenuState.Day   => labelDay,
            MenuState.Dusk  => labelDusk,
            MenuState.Night => labelNight,
            _ => ""
        };
    }

    // ═══════════════════════════════════════════════
    //  功能回调
    // ═══════════════════════════════════════════════

    // ────────────────── 开始游戏 (Night 状态) ──────────────────

    void OnStartGame()
    {
        if (isTransitioning) return;
        PlayClickSFX();
        StartCoroutine(StartGameTransition());
    }

    /// <summary>景深放大转场 → 通过 GameManager 加载 Phase 1</summary>
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
        Debug.Log($"等待完成，GameManager.Instance = {GameManager.Instance}"); 

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

    // ────────────────── 退出游戏 (Dusk 状态) ──────────────────

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
        // 取消退出 → 轮转回 Night（标题画面）
        int stepsToNight = ((int)MenuState.Night - centerIndex + 3) % 3;
        if (stepsToNight == 0) return;

        // 选择最短路径方向
        if (stepsToNight <= 3 / 2)
            RotateTo((int)MenuState.Night, stepsToNight);
        else
            RotateTo((int)MenuState.Night, -(3 - stepsToNight));
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
    //  公开 API（供外部调用）
    // ═══════════════════════════════════════════════

    /// <summary>直接跳转到指定状态（无动画）</summary>
    public void SetStateImmediate(MenuState state)
    {
        centerIndex = (int)state;
        SyncUIContainers();
        UpdateZoneLabels();
    }

    /// <summary>触发逆时针轮转（等同于点击右区域）</summary>
    public void RotateCounterClockwise()
    {
        if (isTransitioning) return;
        int targetIndex = (centerIndex + 1) % 3;
        RotateTo(targetIndex, +1);
    }

    /// <summary>触发顺时针轮转（等同于点击左区域）</summary>
    public void RotateClockwise()
    {
        if (isTransitioning) return;
        int targetIndex = (centerIndex - 1 + 3) % 3;
        RotateTo(targetIndex, -1);
    }
}
