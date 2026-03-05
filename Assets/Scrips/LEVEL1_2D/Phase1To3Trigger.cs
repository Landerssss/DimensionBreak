using UnityEngine;
using TMPro;

/// <summary>
/// Phase 1 黑雾触发器：玩家碰到后检测是否已解锁弓箭。
/// 已解锁 → 加载 Phase 3 Boss 战场景。
/// 未解锁 → 显示 UI 提示"前方的区域无法探索"。
/// </summary>
public class Phase1To3Trigger : MonoBehaviour
{
    [Header("=== UI 提示 ===")]
    [SerializeField] private TextMeshProUGUI hintText;
    [Tooltip("提示文字显示时长")]
    [SerializeField] private float hintDuration = 2.5f;
    [SerializeField] private string lockedMessage = "前方的区域无法探索";

    [Header("=== 视觉 ===")]
    [Tooltip("黑雾粒子（可选），解锁后可以播放消散特效")]
    [SerializeField] private ParticleSystem fogParticle;

    // 防止重复触发
    private bool triggered;

    void Start()
    {
        if (hintText != null)
            hintText.gameObject.SetActive(false);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (triggered) return;
        if (!other.CompareTag("Player")) return;

        if (GameManager.Instance == null) return;

        if (GameManager.Instance.BowUnlocked)
        {
            // 已解锁弓箭 → 进入 Phase 3
            triggered = true;
            Debug.Log("[Phase1To3Trigger] 弓箭已解锁，进入 Boss 战！");

            if (fogParticle != null)
                fogParticle.Stop();

            GameManager.Instance.GoToPhase(GameManager.GamePhase.Phase3_BossFight);
        }
        else
        {
            // 未解锁 → 弹提示
            ShowHint(lockedMessage);
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
}
