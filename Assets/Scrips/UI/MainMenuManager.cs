using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using TMPro;

/// <summary>
/// 主菜单管理器：标题画面、开始按钮（景深放大转场）、设置面板。
/// 所有数值全部 [SerializeField] 暴露到面板。
/// </summary>
public class MainMenuManager : MonoBehaviour
{
    // ────────────────── UI 引用 ──────────────────
    [Header("=== UI 引用 ===")]
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private Button startButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button closeSettingsButton;

    // ────────────────── 设置控件 ──────────────────
    [Header("=== 设置面板控件 ===")]
    [SerializeField] private Toggle fullscreenToggle;
    [SerializeField] private Toggle vsyncToggle;
    [SerializeField] private Slider volumeSlider;
    [SerializeField] private TMP_Dropdown resolutionDropdown;

    // ────────────────── 转场效果 ──────────────────
    [Header("=== 开始游戏转场 ===")]
    [SerializeField] private Camera mainCamera;
    [Tooltip("景深放大的目标FOV / Size")]
    [SerializeField] private float zoomTargetSize = 2f;
    [SerializeField] private float zoomSpeed = 3f;
    [Tooltip("放大完成后等待多少秒再切换场景")]
    [SerializeField] private float delayAfterZoom = 0.5f;
    [SerializeField] private string firstLevelSceneName = "Level1_2D";

    // ────────────────── 音频 ──────────────────
    [Header("=== 音频 ===")]
    [SerializeField] private AudioSource bgmSource;
    [SerializeField] private AudioClip buttonClickSFX;
    [SerializeField] private AudioSource sfxSource;

    // ────────────────── 内部状态 ──────────────────
    private Resolution[] availableResolutions;
    private float originalCameraSize;
    private bool isTransitioning;

    // ══════════════════ 初始化 ══════════════════

    void Start()
    {
        isTransitioning = false;

        // 记录相机原始大小
        if (mainCamera != null)
            originalCameraSize = mainCamera.orthographicSize;

        // 面板默认关闭
        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        // 绑定按钮事件
        if (startButton != null) startButton.onClick.AddListener(OnStartGame);
        if (settingsButton != null) settingsButton.onClick.AddListener(OnToggleSettings);
        if (closeSettingsButton != null) closeSettingsButton.onClick.AddListener(OnToggleSettings);

        // 初始化设置
        InitFullscreenToggle();
        InitVsyncToggle();
        InitVolumeSlider();
        InitResolutionDropdown();
    }

    // ══════════════════ 开始游戏 ══════════════════

    void OnStartGame()
    {
        if (isTransitioning) return;
        PlayClickSFX();
        StartCoroutine(StartGameTransition());
    }

    IEnumerator StartGameTransition()
    {
        isTransitioning = true;

        // 景深放大效果
        if (mainCamera != null)
        {
            float current = mainCamera.orthographicSize;
            while (Mathf.Abs(mainCamera.orthographicSize - zoomTargetSize) > 0.05f)
            {
                mainCamera.orthographicSize = Mathf.Lerp(mainCamera.orthographicSize, zoomTargetSize, Time.deltaTime * zoomSpeed);
                yield return null;
            }
            mainCamera.orthographicSize = zoomTargetSize;
        }

        yield return new WaitForSeconds(delayAfterZoom);

        // 加载阶段一场景
        SceneManager.LoadScene(firstLevelSceneName);
    }

    // ══════════════════ 设置面板 ══════════════════

    void OnToggleSettings()
    {
        PlayClickSFX();
        if (settingsPanel != null)
            settingsPanel.SetActive(!settingsPanel.activeSelf);
    }

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

    // ══════════════════ 工具 ══════════════════

    void PlayClickSFX()
    {
        if (sfxSource != null && buttonClickSFX != null)
            sfxSource.PlayOneShot(buttonClickSFX);
    }
}
