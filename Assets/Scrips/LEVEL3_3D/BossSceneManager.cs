using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// Phase 3 Boss 战场景管理器。
/// 状态机：Falling → Walking → BossRising → Fighting → End。
/// 通过 Coroutine 驱动整个开场演出序列。
/// 所有参数全部 [SerializeField] 暴露到面板。
/// 
/// 坐标系约定（纯 3D）：X = 左右，Z = 前后，Y = 上下。
/// </summary>
public class BossSceneManager : MonoBehaviour
{
    public static BossSceneManager Instance { get; private set; }

    // ────────────────── 状态 ──────────────────
    public enum BossPhase
    {
        Falling,     // 相机从高空下落
        Walking,     // 玩家向前走
        BossRising,  // Boss 从地底升起
        Fighting,    // 战斗中
        End          // 结束
    }

    [Header("=== 当前状态 ===")]
    [SerializeField] private BossPhase currentPhase = BossPhase.Falling;
    public BossPhase CurrentPhase => currentPhase;

    // ────────────────── 引用 ──────────────────
    [Header("=== 场景引用 ===")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private Transform playerTransform;
    [SerializeField] private Transform bossTransform;
    [SerializeField] private Transform walkTrigger;
    [Tooltip("Boss 最终停留的 Y 坐标（露出上半身）")]
    [SerializeField] private float bossTargetY = 0f;

    // ────────────────── UI ──────────────────
    [Header("=== UI 引用 ===")]
    [SerializeField] private TextMeshProUGUI centerText;
    [SerializeField] private Slider bossHPBar;
    [SerializeField] private CanvasGroup bossHPBarCanvasGroup;

    // ────────────────── 1. 下落参数 ──────────────────
    [Header("=== 1. 相机下落 ===")]
    [Tooltip("相机初始高度（Y）")]
    [SerializeField] private float fallStartHeight = 80f;
    [Tooltip("下落最终高度（Y）")]
    [SerializeField] private float fallEndHeight = 2f;
    [Tooltip("下落初始速度")]
    [SerializeField] private float fallStartSpeed = 5f;
    [Tooltip("下落加速度")]
    [SerializeField] private float fallAcceleration = 30f;
    [Tooltip("下落时相机朝向（默认看正下方）")]
    [SerializeField] private Vector3 fallCameraRotation = new Vector3(90f, 0f, 0f);

    // ────────────────── 2. 落地震动 ──────────────────
    [Header("=== 2. 落地震动 ===")]
    [SerializeField] private float shakeIntensity = 0.3f;
    [SerializeField] private float shakeDuration = 0.4f;
    [Tooltip("落地后相机平视朝前的旋转")]
    [SerializeField] private Vector3 groundCameraRotation = new Vector3(0f, 0f, 0f);

    // ────────────────── 3. 行走阶段 ──────────────────
    [Header("=== 3. 行走阶段 ===")]
    [SerializeField] private string walkPromptText = "往前走";
    [SerializeField] private float playerWalkSpeed = 4f;

    // ────────────────── 4. Boss 出场 ──────────────────
    [Header("=== 4. Boss 出场 ===")]
    [Tooltip("Boss 起始位置（远处高空）")]
    [SerializeField] private Vector3 bossStartOffset = new Vector3(0f, 30f, 80f); // 正前方远处高空
    [SerializeField] private float bossFlyDuration = 3f;       // 飞行时长
    [SerializeField] private float bossFlyRotateSpeed = 180f;  // 飞行时旋转速度
    [SerializeField] private string bossRisingText = "击败他！";
    [SerializeField] private float textToHPBarDelay = 3f;

    // ────────────────── 5. 战斗阶段 ──────────────────
    [Header("=== 5. 战斗 ===")]
    [SerializeField] private float bossMaxHP = 1000f; // 每段血条的血量
    private float bossCurrentHP;

    [Header("=== Boss 多段血条 ===")]
    [SerializeField] private int totalSegments = 10;
    [SerializeField] private Color[] customSegmentColors; // 可选：手动指定颜色
    [SerializeField] private GameObject segmentClearedUI;   // 清空一段时的提示 UI
    [SerializeField] private float segmentClearedDuration = 0.5f;
    [SerializeField] private float segmentClearedShake = 0.5f;

    private int currentSegmentIndex = 0; // 0 是第一段，逐渐增加到 totalSegments-1

    /// <summary>
    /// 获取 Boss 的总体血量比例（考虑所有段位）
    /// </summary>
    public float BossHPRatio
    {
        get
        {
            if (totalSegments <= 0 || bossMaxHP <= 0) return 0f;
            // 剩余段数所占比例 + 当前段位剩余比例
            float totalRemaining = (totalSegments - 1 - currentSegmentIndex) * bossMaxHP + bossCurrentHP;
            return totalRemaining / (totalSegments * bossMaxHP);
        }
    }

    // ────────────────── 内部 ──────────────────
    private bool playerInputLocked = true;
    public bool IsPlayerInputLocked => playerInputLocked;

    private bool walkTriggerReached;
    private Vector3 bossStartPosition; // Boss 最终落点

    // ══════════════════ 生命周期 ══════════════════

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        bossCurrentHP = bossMaxHP;
        currentSegmentIndex = 0;
        playerInputLocked = true;

        // 隐藏 UI
        if (centerText != null)
            centerText.gameObject.SetActive(false);
        if (segmentClearedUI != null)
            segmentClearedUI.SetActive(false);
        if (bossHPBar != null && bossHPBarCanvasGroup != null)
            bossHPBarCanvasGroup.alpha = 0f;

        // 初始化血条颜色
        UpdateHPBarVisual();

        // Boss 放到远处高空
        if (bossTransform != null)
        {
            bossStartPosition = bossTransform.position; // 记录最终落点
            bossTransform.position = bossStartPosition + bossStartOffset;
        }

        // 相机放到高空
        if (mainCamera != null)
        {
            Vector3 camPos = mainCamera.transform.position;
            camPos.y = fallStartHeight;
            mainCamera.transform.position = camPos;
            mainCamera.transform.rotation = Quaternion.Euler(fallCameraRotation);
        }

        StartCoroutine(CinematicSequence());
    }

    void Update()
    {
        // 行走阶段：检测玩家手动前进
        if (currentPhase == BossPhase.Walking && !playerInputLocked)
        {
            HandleWalkInput();
        }
    }

    // ══════════════════ 演出序列主协程 ══════════════════

    IEnumerator CinematicSequence()
    {
        // ─── 阶段 1：相机下落 ───
        currentPhase = BossPhase.Falling;
        yield return StartCoroutine(CameraFallSequence());

        // ─── 落地震动 ───
        yield return StartCoroutine(ScreenShake());

        // ─── 相机转为平视 ───
        yield return StartCoroutine(RotateCameraSmooth(groundCameraRotation, 0.6f));

        // ─── 阶段 2：提示玩家往前走 ───
        currentPhase = BossPhase.Walking;
        ShowCenterText(walkPromptText);
        playerInputLocked = false;

        // 等玩家走到 Trigger
        yield return new WaitUntil(() => walkTriggerReached);

        playerInputLocked = true;
        HideCenterText();

        // ─── 阶段 3：Boss 从地底升起 ───
        currentPhase = BossPhase.BossRising;
        ShowCenterText(bossRisingText);
        yield return StartCoroutine(BossRiseSequence());

        // ─── 文字过渡到血条 ───
        yield return new WaitForSeconds(textToHPBarDelay);
        HideCenterText();
        yield return StartCoroutine(FadeInBossHPBar(0.8f));

        // ─── 阶段 4：战斗开始 ───
        currentPhase = BossPhase.Fighting;
        playerInputLocked = false;
        Debug.Log("[BossSceneManager] 战斗开始！");
    }

    // ══════════════════ 1. 相机下落 ══════════════════

    IEnumerator CameraFallSequence()
    {
        Transform camT = mainCamera.transform;
        float speed = fallStartSpeed;

        while (camT.position.y > fallEndHeight)
        {
            speed += fallAcceleration * Time.deltaTime;
            Vector3 pos = camT.position;
            pos.y -= speed * Time.deltaTime;
            if (pos.y < fallEndHeight) pos.y = fallEndHeight;
            camT.position = pos;
            yield return null;
        }

        Debug.Log("[BossSceneManager] 落地！");
    }

    // ══════════════════ 2. 屏幕震动 ══════════════════

    IEnumerator ScreenShake(float intensity = -1f, float duration = -1f)
    {
        float useIntensity = intensity < 0 ? shakeIntensity : intensity;
        float useDuration = duration < 0 ? shakeDuration : duration;

        Transform camT = mainCamera.transform;
        Vector3 originalPos = camT.position;
        float elapsed = 0f;

        while (elapsed < useDuration)
        {
            elapsed += Time.deltaTime;
            float decay = 1f - (elapsed / useDuration);
            Vector3 offset = new Vector3(
                Random.Range(-1f, 1f) * useIntensity * decay,
                Random.Range(-1f, 1f) * useIntensity * decay,
                0f
            );
            camT.position = originalPos + offset;
            yield return null;
        }

        camT.position = originalPos;
    }

    // ══════════════════ 相机平滑旋转 ══════════════════

    IEnumerator RotateCameraSmooth(Vector3 targetEuler, float duration)
    {
        Transform camT = mainCamera.transform;
        Quaternion startRot = camT.rotation;
        Quaternion endRot = Quaternion.Euler(targetEuler);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            camT.rotation = Quaternion.Slerp(startRot, endRot, elapsed / duration);
            yield return null;
        }
        camT.rotation = endRot;
    }

    // ══════════════════ 3. 玩家行走 ══════════════════

    void HandleWalkInput()
    {
        if (playerTransform == null) return;

        float z = Input.GetAxisRaw("Vertical");
        float x = Input.GetAxisRaw("Horizontal");

        Vector3 move = new Vector3(x, 0f, z).normalized * playerWalkSpeed * Time.deltaTime;
        playerTransform.position += move;

        // 相机跟随（简单偏移）
        if (mainCamera != null)
        {
            Vector3 camPos = mainCamera.transform.position;
            camPos.x = playerTransform.position.x;
            camPos.z = playerTransform.position.z - 5f; // 跟在玩家身后
            mainCamera.transform.position = camPos;
        }
    }

    /// <summary>
    /// 由行走阶段的隐形 Trigger 调用
    /// </summary>
    public void OnWalkTriggerReached()
    {
        walkTriggerReached = true;
        Debug.Log("[BossSceneManager] 玩家到达行走触发点");
    }

    // ══════════════════ 4. Boss 升起 ══════════════════

    IEnumerator BossRiseSequence()
    {
        if (bossTransform == null) yield break;

        Vector3 startPos = bossTransform.position;
        Vector3 endPos = bossStartPosition;
        float elapsed = 0f;

        while (elapsed < bossFlyDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / bossFlyDuration); // 先快后慢

            // 位置插值
            bossTransform.position = Vector3.Lerp(startPos, endPos, t);

            // 飞行旋转（翻滚感）
            bossTransform.Rotate(0f, bossFlyRotateSpeed * Time.deltaTime, 0f, Space.World);

            yield return null;
        }

        // 落地：强制到终点，重置旋转
        bossTransform.position = endPos;
        bossTransform.rotation = Quaternion.Euler(0f, 180f, 0f);

        // 落地震动
        yield return StartCoroutine(ScreenShake());

        Debug.Log("[BossSceneManager] Boss 出场完毕！");
    }

    // ══════════════════ Boss 血条 ══════════════════

    IEnumerator FadeInBossHPBar(float duration)
    {
        if (bossHPBarCanvasGroup == null) yield break;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            bossHPBarCanvasGroup.alpha = Mathf.Lerp(0f, 1f, elapsed / duration);
            yield return null;
        }
        bossHPBarCanvasGroup.alpha = 1f;

        if (bossHPBar != null)
            bossHPBar.value = 1f;
    }

    /// <summary>
    /// Boss 受击时由外部调用来扣血并更新血条。
    /// </summary>
    public void DamageBoss(float damage)
    {
        if (currentPhase != BossPhase.Fighting) return;

        bossCurrentHP -= damage;

        if (bossCurrentHP <= 0f)
        {
            // 如果还有下一段血条
            if (currentSegmentIndex < totalSegments - 1)
            {
                currentSegmentIndex++;
                bossCurrentHP = bossMaxHP; // 重新满格
                StartCoroutine(OnSegmentClearedEffect());
            }
            else
            {
                bossCurrentHP = 0f;
                OnBossDefeated();
            }
        }

        UpdateHPBarVisual();
    }

    void UpdateHPBarVisual()
    {
        if (bossHPBar != null)
        {
            bossHPBar.value = bossCurrentHP / bossMaxHP;

            // 更新颜色
            if (bossHPBar.fillRect != null)
            {
                Image fillImage = bossHPBar.fillRect.GetComponent<Image>();
                if (fillImage != null)
                {
                    fillImage.color = GetSegmentColor(currentSegmentIndex);
                }
            }
        }
    }

    /// <summary>
    /// 获取当前段位的颜色（从青到红）
    /// </summary>
    Color GetSegmentColor(int index)
    {
        if (customSegmentColors != null && customSegmentColors.Length > 0)
        {
            return customSegmentColors[index % customSegmentColors.Length];
        }

        // 自动计算：从青色 (Hue 180) 到红色 (Hue 0)
        // 注意：Hue 从 0.5 (180/360) 递减到 0
        float t = (float)index / (totalSegments - 1);
        float hue = Mathf.Lerp(0.5f, 0f, t);
        return Color.HSVToRGB(hue, 0.8f, 1f);
    }

    IEnumerator OnSegmentClearedEffect()
    {
        // 提示 UI
        if (segmentClearedUI != null)
        {
            segmentClearedUI.SetActive(true);
            yield return new WaitForSeconds(segmentClearedDuration);
            segmentClearedUI.SetActive(false);
        }

        // 屏幕微震
        yield return StartCoroutine(ScreenShake(segmentClearedShake, 0.2f));
    }

    void OnBossDefeated()
    {
        currentPhase = BossPhase.End;
        playerInputLocked = true;

        Debug.Log("[BossSceneManager] Boss 已被击败！次元突破通关！");

        ShowCenterText("次元突破！");

        // TODO: 播放最终演出动画，然后回到主菜单或结算画面
        StartCoroutine(EndSequence());
    }

    IEnumerator EndSequence()
    {
        yield return new WaitForSeconds(4f);

        if (GameManager.Instance != null)
            GameManager.Instance.ReturnToMenu();
    }

    // ══════════════════ UI 工具 ══════════════════

    void ShowCenterText(string text)
    {
        if (centerText == null) return;
        centerText.text = text;
        centerText.gameObject.SetActive(true);
    }

    void HideCenterText()
    {
        if (centerText != null)
            centerText.gameObject.SetActive(false);
    }
}
