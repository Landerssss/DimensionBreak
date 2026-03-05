using UnityEngine;

/// <summary>
/// 行走阶段的隐形 Trigger：玩家碰到后通知 BossSceneManager。
/// 挂在 Phase 3 场景中，玩家前方几米处的空 GameObject (带 Collider, isTrigger=true)。
/// </summary>
public class BossWalkTrigger : MonoBehaviour
{
    private bool triggered;

    void OnTriggerEnter(Collider other)
    {
        if (triggered) return;
        if (!other.CompareTag("Player")) return;

        triggered = true;

        if (BossSceneManager.Instance != null)
        {
            BossSceneManager.Instance.OnWalkTriggerReached();
        }
    }
}
