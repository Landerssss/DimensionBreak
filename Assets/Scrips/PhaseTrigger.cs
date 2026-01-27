using UnityEngine;
using System.Collections;

public class PhaseTrigger : MonoBehaviour
{
    // 这里定义了两个节点的类型
    public enum TriggerType { Node1_Zoom, Node2_Jump }
    public TriggerType type;

    [Header("=== 跳转设置 (仅Node2需要) ===")]
    public Transform tdSpawnPoint; // 塔防地图的落脚点

    private bool triggered = false;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (triggered) return;
        if (other.CompareTag("Player"))
        {
            triggered = true;

            if (type == TriggerType.Node1_Zoom)
            {
                // 【关键修复】这里调用的是 CameraDirector，不是旧的 DimensionController
                CameraDirector.Instance.TriggerFPSZoom();
            }
            else if (type == TriggerType.Node2_Jump)
            {
                StartCoroutine(TransitionSequence(other.gameObject));
            }
        }
    }

    IEnumerator TransitionSequence(GameObject player)
    {
        GameManager.Instance.isTransitioning = true; 
        Rigidbody2D rb = player.GetComponent<Rigidbody2D>();

        // 模拟跳跃力
        rb.SetVelocity(new Vector2(10f, 8f));

        yield return new WaitForSeconds(1.0f); 

        // 瞬移
        if (tdSpawnPoint != null)
            player.transform.position = tdSpawnPoint.position;
        
        rb.SetVelocity(Vector2.zero);

        // 【关键修复】调用新的摄像机导演
        CameraDirector.Instance.TriggerTopDown();

        yield return new WaitForSeconds(1.0f); 
        GameManager.Instance.isTransitioning = false;
        
        Debug.Log("第二阶段开始！");
    }
}