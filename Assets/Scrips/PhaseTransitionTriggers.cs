using UnityEngine;
using System.Collections;

public class PhaseTransitionTriggers : MonoBehaviour
{
    public enum TriggerType { CameraZoomIn, JumpToPhase2 }
    public TriggerType type;

    [Header("=== 引用 ===")]
    public Transform cameraTarget; // 摄像机要移动到的目标位置
    public Transform phase2SpawnPoint; // 二阶段落点

    private bool hasTriggered = false;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (hasTriggered) return;
        if (other.CompareTag("Player"))
        {
            hasTriggered = true;

            if (type == TriggerType.CameraZoomIn)
            {
                Debug.Log("触发节点1：视角拉近，模拟第一人称...");
                StartCoroutine(ZoomCameraSequence());
            }
            else if (type == TriggerType.JumpToPhase2)
            {
                Debug.Log("触发节点2：信仰之跃！");
                StartCoroutine(JumpSequence(other.gameObject));
            }
        }
    }

    // 效果：摄像机快速平滑地移动到主角“眼睛”的位置，模拟FPS感
    IEnumerator ZoomCameraSequence()
    {
        Camera cam = Camera.main;
        float t = 0;
        float duration = 1.0f;
        Vector3 startPos = cam.transform.position;
        float startSize = cam.orthographicSize;

        // 目标：相机变小（聚焦），位置贴近主角
        float targetSize = 2.0f; // 视野变窄
        // 这里只是简单演示，实际可以通过修改CameraFollow脚本的Offset来实现
        
        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            cam.orthographicSize = Mathf.Lerp(startSize, targetSize, t);
            yield return null;
        }
    }

    // 效果：主角跳出画面，转场到二阶段
    IEnumerator JumpSequence(GameObject player)
    {
        GameManager.Instance.isTransitioning = true; // 锁定操作
        Rigidbody2D rb = player.GetComponent<Rigidbody2D>();

        // 1. 给一个向前的力 + 各种旋转特效（可选）
        rb.SetVelocity(new Vector2(10f, 5f)); 
        
        yield return new WaitForSeconds(1.0f); // 等待跳出屏幕

        // 2. 瞬移到二阶段平台
        player.transform.position = phase2SpawnPoint.position;
        player.transform.rotation = Quaternion.identity;
        rb.SetVelocity(Vector2.zero);

        // 3. 切换摄像机模式为“俯视塔防” (需要调用之前的CameraDimensionController)
        Camera.main.GetComponent<CameraDimensionController>().SwitchToTDMode();

        GameManager.Instance.isTransitioning = false; // 恢复（或进入塔防模式的操作逻辑）
        Debug.Log("=== 进入第二阶段：植物融合塔防 ===");
    }
}