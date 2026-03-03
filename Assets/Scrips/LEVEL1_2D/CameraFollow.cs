using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Tooltip("要跟随的主角")]
    public Transform target;
    
    [Tooltip("摄像机的偏移量。X为正，代表摄像机在右，主角在左")]
    public Vector2 offset = new Vector2(3f, 2f); 
    
    [Tooltip("镜头平滑移动的时间")]
    public float smoothTime = 0.2f;

    private Vector3 velocity = Vector3.zero;

    void LateUpdate()
    {
        if (target == null) return;

        // 目标位置 = 主角位置 + 偏移量。Z轴保持 -10f 防止看不见场景
        Vector3 targetPosition = new Vector3(target.position.x + offset.x, target.position.y + offset.y, -10f);
        
        // 平滑移动
        transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref velocity, smoothTime);
    }
}