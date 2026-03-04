using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 右上角武器切换 UI：显示已解锁的武器图标，点击切换当前攻击模式。
/// 未解锁的武器显示锁图标。
/// </summary>
public class WeaponUIController : MonoBehaviour
{
    // ────────────────── 武器槽位 ──────────────────
    [Header("=== 武器按钮 ===")]
    [SerializeField] private Button meleeButton;
    [SerializeField] private Button bowButton;
    [SerializeField] private Button waterBombButton;

    // ────────────────── 锁图标覆盖 ──────────────────
    [Header("=== 锁图标 ===")]
    [Tooltip("覆盖在弓箭按钮上的锁 Image")]
    [SerializeField] private GameObject bowLockIcon;
    [Tooltip("覆盖在水魔爆按钮上的锁 Image")]
    [SerializeField] private GameObject waterBombLockIcon;

    // ────────────────── 选中高亮 ──────────────────
    [Header("=== 选中高亮 ===")]
    [SerializeField] private Color selectedColor = new Color(1f, 0.85f, 0.2f, 1f);
    [SerializeField] private Color normalColor = Color.white;

    // ────────────────── 提示文字 ──────────────────
    [Header("=== 提示 ===")]
    [SerializeField] private TextMeshProUGUI weaponNameText;
    [SerializeField] private float nameDisplayDuration = 2f;

    // ────────────────── 按钮 Image 引用 ──────────────────
    private Image meleeImage;
    private Image bowImage;
    private Image waterBombImage;

    // ══════════════════ 生命周期 ══════════════════

    void Start()
    {
        // 缓存 Image
        if (meleeButton != null)      meleeImage      = meleeButton.GetComponent<Image>();
        if (bowButton != null)        bowImage        = bowButton.GetComponent<Image>();
        if (waterBombButton != null)  waterBombImage  = waterBombButton.GetComponent<Image>();

        // 绑定点击
        if (meleeButton != null)      meleeButton.onClick.AddListener(OnClickMelee);
        if (bowButton != null)        bowButton.onClick.AddListener(OnClickBow);
        if (waterBombButton != null)  waterBombButton.onClick.AddListener(OnClickWaterBomb);

        // 订阅 GameManager 事件
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnWeaponChanged  += HandleWeaponChanged;
            GameManager.Instance.OnWeaponUnlocked += HandleWeaponUnlocked;
        }

        RefreshUI();
    }

    void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnWeaponChanged  -= HandleWeaponChanged;
            GameManager.Instance.OnWeaponUnlocked -= HandleWeaponUnlocked;
        }
    }

    // ══════════════════ 按钮回调 ══════════════════

    void OnClickMelee()
    {
        GameManager.Instance?.SwitchWeapon(GameManager.WeaponType.Melee);
    }

    void OnClickBow()
    {
        if (GameManager.Instance == null) return;
        if (!GameManager.Instance.BowUnlocked)
        {
            ShowWeaponName("🔒 未解锁");
            return;
        }
        GameManager.Instance.SwitchWeapon(GameManager.WeaponType.Bow);
    }

    void OnClickWaterBomb()
    {
        if (GameManager.Instance == null) return;
        if (!GameManager.Instance.WaterBombUnlocked)
        {
            ShowWeaponName("🔒 未解锁");
            return;
        }
        GameManager.Instance.SwitchWeapon(GameManager.WeaponType.WaterBomb);
    }

    // ══════════════════ 事件处理 ══════════════════

    void HandleWeaponChanged(GameManager.WeaponType weapon)
    {
        RefreshHighlight(weapon);

        string name = weapon switch
        {
            GameManager.WeaponType.Melee     => "基础攻击",
            GameManager.WeaponType.Bow       => "弓箭",
            GameManager.WeaponType.WaterBomb => "水魔爆",
            _ => ""
        };
        ShowWeaponName(name);
    }

    void HandleWeaponUnlocked(string weaponName)
    {
        RefreshLockIcons();
        ShowWeaponName($"解锁：{weaponName}！");
    }

    // ══════════════════ UI 刷新 ══════════════════

    void RefreshUI()
    {
        RefreshLockIcons();

        var current = GameManager.Instance != null
            ? GameManager.Instance.CurrentWeapon
            : GameManager.WeaponType.Melee;
        RefreshHighlight(current);
    }

    void RefreshLockIcons()
    {
        bool bow   = GameManager.Instance != null && GameManager.Instance.BowUnlocked;
        bool water = GameManager.Instance != null && GameManager.Instance.WaterBombUnlocked;

        if (bowLockIcon != null)        bowLockIcon.SetActive(!bow);
        if (waterBombLockIcon != null)  waterBombLockIcon.SetActive(!water);
    }

    void RefreshHighlight(GameManager.WeaponType active)
    {
        SetButtonColor(meleeImage,      active == GameManager.WeaponType.Melee);
        SetButtonColor(bowImage,        active == GameManager.WeaponType.Bow);
        SetButtonColor(waterBombImage,  active == GameManager.WeaponType.WaterBomb);
    }

    void SetButtonColor(Image img, bool selected)
    {
        if (img != null)
            img.color = selected ? selectedColor : normalColor;
    }

    void ShowWeaponName(string text)
    {
        if (weaponNameText == null) return;
        weaponNameText.text = text;
        weaponNameText.gameObject.SetActive(true);
        CancelInvoke(nameof(HideWeaponName));
        Invoke(nameof(HideWeaponName), nameDisplayDuration);
    }

    void HideWeaponName()
    {
        if (weaponNameText != null)
            weaponNameText.gameObject.SetActive(false);
    }
}
