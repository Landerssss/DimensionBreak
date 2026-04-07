using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using TMPro;

/// <summary>
/// 主菜单管理器 — "三区域遮罩轮转 (Three-Zone Mask Rotation)" 版本
///
/// 屏幕被视觉上划分为三个区域：
///   Left  (小三角) — 显示上一张图片的局部
///   Center(主体)   — 显示当前状态的主背景
///   Right (大三角) — 显示下一张图片的局部
///
/// 三张完整大图 [Day, Dusk, Night] 存储在 allBackgrounds 数组中，
/// 通过索引轮转将不同 Sprite 赋值给三个 Image Slot。
///
/// 状态对应功能：
///   Night (index=2) — 标题画面（游戏启动默认状态）
///   Day   (index=0) — 设置 + 开始游戏
///   Dusk  (index=1) — 退出游戏 + 确认弹窗
///
/// 点击右区域 → 逆时针轮转（索引 +1），图片引用序列整体左移一位。
/// 点击左区域 → 顺时针轮转（索引 -1），图片引用序列整体右移一位。
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
        Day   = 0,  // 设置 + 开始游戏
        Dusk  = 1,  // 退出确认
        Night = 2   // 标题画面（初始）
    }

    // ═══════════════════════════════════════════════
    //  序列化字段 — 全部暴露到 Inspector
    // ═══════════════════════════════════════════════

    // ────────────────── 三区域 Image Slot ──────────────────
    [Header("=== 三区域 Image Slot ===")]
    [Tooltip("左侧小三角区域的 Image 组件")]
    [SerializeField] private Image leftSlot;
    [Tooltip("中央主体区域的 Image 组件")]
    [SerializeField] private Image centerSlot;
    [Tooltip("右侧大三角区域的 Image 组件")]
    [SerializeField] private Image rightSlot;

    // ────────────────── 背景图集 ──────────────────
    [Header("=== 背景图集 ===")]
    [Tooltip("按 [Day, Dusk, Night] 顺序存放三张背景大图")]
    [SerializeField] private Sprite[] allBackgrounds = new Sprite[3];

    // ────────────────── 初始状态 ──────────────────
    [Header("=== 初始状态 ===")]
    [Tooltip("游戏启动时的默认状态")]
    [SerializeField] private MenuState initialState = MenuState.Night;

    // ────────────────── 过渡效果 ──────────────────
    [Header("=== 轮转过渡 ===")]
    [Tooltip("轮转 alpha 渐变的总时长（秒）")]
    [SerializeField] private float fadeDuration = 0.5f;
    [Tooltip("渐变缓动曲线")]
    [SerializeField] private AnimationCurve fadeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

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
    [SerializeField] private GameObject quitConfirmPanel;
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

        // 立即更新三个 Slot 的 Sprite（无动画）
        ApplySpritesToSlots();
        UpdateUIForCurrentState();

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

        // 退出确认弹窗默认隐藏
        if (quitConfirmPanel != null)
            quitConfirmPanel.SetActive(false);
    }

    // ═══════════════════════════════════════════════
    //  区域按钮回调
    // ═══════════════════════════════════════════════

    /// <summary>
    /// 右区域大三角点击 → 逆时针轮转（索引 +1）
    /// 图片引用序列整体向左位移一位
    /// </summary>
    void OnRightZoneClicked()
    {
        if (isTransitioning) return;
        PlayClickSFX();
        RotateWithFade(+1);
    }

    /// <summary>
    /// 左区域小三角点击 → 顺时针轮转（索引 -1）
    /// 图片引用序列整体向右位移一位
    /// </summary>
    void OnLeftZoneClicked()
    {
        if (isTransitioning) return;
        PlayClickSFX();
        RotateWithFade(-1);
    }

    // ═══════════════════════════════════════════════
    //  轮转核心
    // ═══════════════════════════════════════════════

    /// <summary>
    /// 带 alpha 渐变的轮转动画。
    /// direction: +1 = 逆时针（右侧触发），-1 = 顺时针（左侧触发）
    /// </summary>
    private void RotateWithFade(int direction)
    {
        if (activeTransition != null)
            StopCoroutine(activeTransition);

        activeTransition = StartCoroutine(RotateFadeCoroutine(direction));
    }

    /// <summary>
    /// 轮转渐变协程：
    /// 1) 三个 Slot 的 alpha 从 1 渐变到 0（淡出）
    /// 2) 更新 centerIndex 和 Sprite 赋值
    /// 3) 三个 Slot 的 alpha 从 0 渐变到 1（淡入）
    /// </summary>
    private IEnumerator RotateFadeCoroutine(int direction)
    {
        isTransitioning = true;
        PlayTransitionSFX();

        float halfDuration = fadeDuration * 0.5f;

        // ── Phase 1: 淡出 ──
        float elapsed = 0f;
        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / halfDuration);
            float curveT = fadeCurve.Evaluate(t);
            float alpha = Mathf.Lerp(1f, 0f, curveT);

            SetSlotAlpha(leftSlot, alpha);
            SetSlotAlpha(centerSlot, alpha);
            SetSlotAlpha(rightSlot, alpha);

            yield return null;
        }

        // 确保完全透明
        SetSlotAlpha(leftSlot, 0f);
        SetSlotAlpha(centerSlot, 0f);
        SetSlotAlpha(rightSlot, 0f);

        // ── 更新索引和 Sprite ──
        int count = allBackgrounds.Length;
        centerIndex = (centerIndex + direction + count) % count;
        ApplySpritesToSlots();
        UpdateUIForCurrentState();

        // ── Phase 2: 淡入 ──
        elapsed = 0f;
        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / halfDuration);
            float curveT = fadeCurve.Evaluate(t);
            float alpha = Mathf.Lerp(0f, 1f, curveT);

            SetSlotAlpha(leftSlot, alpha);
            SetSlotAlpha(centerSlot, alpha);
            SetSlotAlpha(rightSlot, alpha);

            yield return null;
        }

        // 确保完全不透明
        SetSlotAlpha(leftSlot, 1f);
        SetSlotAlpha(centerSlot, 1f);
        SetSlotAlpha(rightSlot, 1f);

        isTransitioning = false;
        activeTransition = null;
    }

    // ═══════════════════════════════════════════════
    //  Sprite 赋值
    // ═══════════════════════════════════════════════

    /// <summary>
    /// 根据当前 centerIndex，将 Sprite 分配给三个 Slot：
    ///   Left  = allBackgrounds[(centerIndex - 1 + N) % N]  (上一张)
    ///   Center = allBackgrounds[centerIndex]                (当前)
    ///   Right  = allBackgrounds[(centerIndex + 1) % N]      (下一张)
    /// </summary>
    private void ApplySpritesToSlots()
    {
        if (allBackgrounds == null || allBackgrounds.Length < 3)
        {
            Debug.LogWarning("[MainMenuManager] allBackgrounds 数组需要至少 3 张 Sprite！");
            return;
        }

        int count = allBackgrounds.Length;
        int leftIndex  = (centerIndex - 1 + count) % count;
        int rightIndex = (centerIndex + 1) % count;

        if (leftSlot != null)
            leftSlot.sprite = allBackgrounds[leftIndex];
        if (centerSlot != null)
            centerSlot.sprite = allBackgrounds[centerIndex];
        if (rightSlot != null)
            rightSlot.sprite = allBackgrounds[rightIndex];
    }

    // ═══════════════════════════════════════════════
    //  UI 状态联动
    // ═══════════════════════════════════════════════

    /// <summary>根据当前 centerIndex 切换各功能 UI 的显示/隐藏并更新标签</summary>
    private void UpdateUIForCurrentState()
    {
        MenuState state = (MenuState)centerIndex;

        // ── 功能容器显隐 ──
        if (titleContainer != null)
            titleContainer.SetActive(state == MenuState.Night);

        if (settingsContainer != null)
            settingsContainer.SetActive(state == MenuState.Day);

        if (startGameButton != null)
            startGameButton.gameObject.SetActive(state == MenuState.Night);

        // 退出弹窗仅在 Dusk 状态时自动显示
        if (quitConfirmPanel != null)
            quitConfirmPanel.SetActive(state == MenuState.Dusk);

        // ── 更新区域标签 ──
        UpdateZoneLabels();
    }

    /// <summary>
    /// 根据三个 Slot 对应的背景，更新标签文字。
    /// 标签内容 = 该 Slot 对应状态的名称。
    /// </summary>
    private void UpdateZoneLabels()
    {
        int count = allBackgrounds.Length;
        int leftIndex  = (centerIndex - 1 + count) % count;
        int rightIndex = (centerIndex + 1) % count;

        if (leftZoneLabel != null)
            leftZoneLabel.text = GetLabelForIndex(leftIndex);
        if (rightZoneLabel != null)
            rightZoneLabel.text = GetLabelForIndex(rightIndex);
        if (centerLabel != null)
            centerLabel.text = GetLabelForIndex(centerIndex);
    }

    /// <summary>根据背景索引返回对应的标签文字</summary>
    private string GetLabelForIndex(int index)
    {
        MenuState state = (MenuState)index;
        return state switch
        {
            MenuState.Day   => labelDay,    // "Setting"
            MenuState.Dusk  => labelDusk,   // "Exit"
            MenuState.Night => labelNight,  // "Title"
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
        // 计算需要旋转的方向：从 Dusk(1) 到 Night(2) 需要 +1
        int stepsToNight = ((int)MenuState.Night - centerIndex + allBackgrounds.Length) % allBackgrounds.Length;
        if (stepsToNight == 0) return;

        // 选择最短路径方向
        if (stepsToNight <= allBackgrounds.Length / 2)
            RotateWithFade(+stepsToNight);
        else
            RotateWithFade(-(allBackgrounds.Length - stepsToNight));
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

    /// <summary>设置 Image 组件的 alpha 值</summary>
    private void SetSlotAlpha(Image slot, float alpha)
    {
        if (slot == null) return;
        Color c = slot.color;
        c.a = alpha;
        slot.color = c;
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
    //  公开 API（供外部调用）
    // ═══════════════════════════════════════════════

    /// <summary>直接跳转到指定状态（无动画）</summary>
    public void SetStateImmediate(MenuState state)
    {
        centerIndex = (int)state;
        ApplySpritesToSlots();
        SetSlotAlpha(leftSlot, 1f);
        SetSlotAlpha(centerSlot, 1f);
        SetSlotAlpha(rightSlot, 1f);
        UpdateUIForCurrentState();
    }

    /// <summary>触发逆时针轮转（等同于点击右区域）</summary>
    public void RotateCounterClockwise()
    {
        if (isTransitioning) return;
        RotateWithFade(+1);
    }

    /// <summary>触发顺时针轮转（等同于点击左区域）</summary>
    public void RotateClockwise()
    {
        if (isTransitioning) return;
        RotateWithFade(-1);
    }
}
