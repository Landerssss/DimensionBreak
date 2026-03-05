using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Phase 3 玩家血量系统 + 仪表盘风格巨型血条 UI。
/// 进入场景时血量固定为 99999。
/// </summary>
public class Player3DStats : MonoBehaviour
{
    // ────────────────── 血量 ──────────────────
    [Header("=== 血量 ===")]
    [SerializeField] private float maxHP = 99999f;
    private float currentHP;
    public float CurrentHP => currentHP;
    public float HPRatio => currentHP / maxHP;

    // ────────────────── UI 引用 ──────────────────
    [Header("=== 血条 UI ===")]
    [SerializeField] private Slider hpBar;
    [SerializeField] private Image hpBarFill;
    [SerializeField] private TextMeshProUGUI hpText;

    // ────────────────── 血条颜色渐变 ──────────────────
    [Header("=== 血条视觉 ===")]
    [Tooltip("满血颜色（亮色调高张力）")]
    [SerializeField] private Color fullHPColor = new Color(0f, 1f, 0.85f, 1f); // 青绿霓虹
    [Tooltip("低血颜色")]
    [SerializeField] private Color lowHPColor = new Color(1f, 0.15f, 0.2f, 1f); // 警告红
    [Tooltip("血条低于此比例时开始闪烁")]
    [SerializeField] private float flashThreshold = 0.3f;
    [SerializeField] private float flashSpeed = 6f;

    // ────────────────── 受击屏幕闪红 ──────────────────
    [Header("=== 受击闪红 ===")]
    [SerializeField] private Image damageVignette;
    [SerializeField] private float vignetteFlashDuration = 0.15f;
    [SerializeField] private float vignetteMaxAlpha = 0.4f;

    // ────────────────── 缓动 ──────────────────
    [Header("=== 血条缓动 ===")]
    [SerializeField] private float hpLerpSpeed = 5f;
    private float displayedHP;

    // ══════════════════ 生命周期 ══════════════════

    void Start()
    {
        currentHP = maxHP;
        displayedHP = maxHP;

        if (hpBar != null) hpBar.value = 1f;
        UpdateHPText();

        if (damageVignette != null)
        {
            Color c = damageVignette.color;
            c.a = 0f;
            damageVignette.color = c;
        }
    }

    void Update()
    {
        // 缓动插值
        displayedHP = Mathf.Lerp(displayedHP, currentHP, Time.deltaTime * hpLerpSpeed);

        float ratio = displayedHP / maxHP;

        if (hpBar != null)
            hpBar.value = ratio;

        // 颜色渐变
        if (hpBarFill != null)
        {
            Color baseColor = Color.Lerp(lowHPColor, fullHPColor, ratio);

            // 低血闪烁
            if (ratio < flashThreshold)
            {
                float flash = Mathf.Abs(Mathf.Sin(Time.time * flashSpeed));
                baseColor = Color.Lerp(baseColor, Color.white, flash * 0.5f);
            }

            hpBarFill.color = baseColor;
        }

        UpdateHPText();

        // 受击闪红衰减
        if (damageVignette != null && damageVignette.color.a > 0f)
        {
            Color c = damageVignette.color;
            c.a = Mathf.Max(0f, c.a - Time.deltaTime / vignetteFlashDuration);
            damageVignette.color = c;
        }
    }

    // ══════════════════ 受击 ══════════════════

    public void TakeDamage(float damage)
    {
        currentHP = Mathf.Max(0f, currentHP - damage);
        Debug.Log($"[Player3D] 受击 -{damage:F0}，剩余 HP: {currentHP:F0}");

        // 屏幕闪红
        if (damageVignette != null)
        {
            Color c = damageVignette.color;
            c.a = vignetteMaxAlpha;
            damageVignette.color = c;
        }

        if (currentHP <= 0f)
        {
            OnPlayerDeath();
        }
    }

    void OnPlayerDeath()
    {
        Debug.Log("[Player3D] 玩家阵亡！");
        // TODO: 死亡演出
    }

    // ══════════════════ UI ══════════════════

    void UpdateHPText()
    {
        if (hpText != null)
            hpText.text = $"{Mathf.CeilToInt(displayedHP)}";
    }
}
