using UnityEngine;

public class CameraDimensionController : MonoBehaviour
{
    [Header("=== 目标对象 ===")]
    public Transform player;       // 主角
    public Transform tdFocusPoint; // 塔防模式的地图中心点（在场景里放个空物体作为靶心）

    [Header("=== 视角设置 ===")]
    public Vector3 sideViewOffset = new Vector3(0, 2, -10); // 2D模式相机的偏移
    public Vector3 topViewOffset = new Vector3(0, 15, 0);   // 塔防模式相机的偏移（高空俯视）
    public float switchSpeed = 2.0f; // 切换镜头的速度

    // 状态标记
    public bool isTowerDefenseMode = false;

    void LateUpdate()
    {
        if (player == null) return;

        Vector3 targetPosition;
        Quaternion targetRotation;

        if (!isTowerDefenseMode)
        {
            // === 模式 1: 2D 动作跟拍 ===
            // 只跟随X轴和Y轴，Z轴固定
            targetPosition = new Vector3(player.position.x, player.position.y, 0) + sideViewOffset;
            targetRotation = Quaternion.Euler(0, 0, 0); // 正视前方
        }
        else
        {
            // === 模式 2: 3D 俯视塔防 ===
            // 移动到塔防地图中心的正上方
            targetPosition = tdFocusPoint.position + topViewOffset;
            // 旋转向下看 (X轴旋转90度)
            targetRotation = Quaternion.Euler(90, 0, 0);
        }

        // 平滑插值 (Lerp) 实现运镜效果
        transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * switchSpeed);
        transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, Time.deltaTime * switchSpeed);
    }

    // 外部调用这个方法来触发切换
    public void SwitchToTDMode()
    {
        isTowerDefenseMode = true;
    }
}
