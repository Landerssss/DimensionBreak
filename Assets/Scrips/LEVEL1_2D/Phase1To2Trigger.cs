using UnityEngine;
using TMPro;

/// <summary>
/// Phase 1 → Phase 2 转场触发器：玩家碰到后保存坐标并加载 Phase 2 方格场景。
/// 使用 2D 物理检测（OnTriggerEnter2D），挂载的 Collider2D 必须勾选 Is Trigger。
/// </summary>
public class Phase1To2Trigger : MonoBehaviour
{
    [Header("=== 场景配置 ===")]
    [Tooltip("Phase 2 场景名称")]
    [SerializeField] private string phase2SceneName = "Level2_GridPuzzle";

    [Header("=== UI 提示 ===")]
    [SerializeField] private TextMeshProUGUI hintText;
    [Tooltip("提示文字显示时长")]
    [SerializeField] private float hintDuration = 2.5f;
    [SerializeField] private string enterMessage = "正在进入纸片空间...";

    [Header("=== 视觉反馈 ===")]
    [Tooltip("当前机关的 SpriteRenderer，触发后将替换为暗色贴图")]
    [SerializeField] private SpriteRenderer targetSpriteRenderer;
    [Tooltip("触发后显示的暗色贴图")]
    [SerializeField] private Sprite darkenedSprite;

    // 防止重复触发
    private bool triggered;

    public string GetTriggerID()
    {
        // 简易唯一标识符
        return gameObject.name + "_" + transform.position.ToString();
    }

    void Start()
    {
        if (hintText != null)
            hintText.gameObject.SetActive(false);

        // 持久化状态检查：若玩家已进入过 Phase 2，恢复已触发状态
        if (GameManager.Instance != null && GameManager.Instance.consumedTriggers.Contains(GetTriggerID()))
        {
            triggered = true;
            ApplyDarkenedSprite();
            var col = GetComponent<Collider2D>();
            if (col != null) col.enabled = false;
        }
    }

    /// <summary>
    /// OnEnable 时输出调试信息，便于排查环境配置问题
    /// </summary>
    void OnEnable()
    {
        var col = GetComponent<Collider2D>();
        Debug.Log($"[Phase1To2Trigger] OnEnable — Collider2D: {(col != null ? "存在" : "缺失")}, " +
                  $"IsTrigger: {(col != null && col.isTrigger)}");
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (triggered) return;
        if (!other.CompareTag("Player")) return;

        triggered = true;
        
        if (GameManager.Instance != null)
        {
            GameManager.Instance.consumedTriggers.Add(GetTriggerID());
        }

        ApplyDarkenedSprite();
        Debug.Log("[Phase1To2Trigger] 玩家进入触发区，准备转场至 Phase 2！");

        // 保存玩家在 Phase 1 的坐标，以便 Phase 2 失败后返回原地
        if (GameManager.Instance != null)
        {
            GameManager.Instance.SavePhase1Position(other.transform.position);
        }

        // 显示提示
        ShowHint(enterMessage);

        // 转场至 Phase 2
        if (GameManager.Instance != null)
        {
            GameManager.Instance.GoToPhase(GameManager.GamePhase.Phase2_GridPuzzle);
        }
        else
        {
            // Fallback：直接加载场景
            UnityEngine.SceneManagement.SceneManager.LoadScene(phase2SceneName);
        }
    }

    void ShowHint(string message)
    {
        if (hintText == null) return;
        hintText.text = message;
        hintText.gameObject.SetActive(true);
        CancelInvoke(nameof(HideHint));
        Invoke(nameof(HideHint), hintDuration);
    }

    void HideHint()
    {
        if (hintText != null)
            hintText.gameObject.SetActive(false);
    }

    /// <summary>
    /// 将 targetSpriteRenderer 的贴图替换为暗色版本（仅在两者均不为空时执行）。
    /// </summary>
    void ApplyDarkenedSprite()
    {
        if (targetSpriteRenderer != null && darkenedSprite != null)
            targetSpriteRenderer.sprite = darkenedSprite;
    }
}
