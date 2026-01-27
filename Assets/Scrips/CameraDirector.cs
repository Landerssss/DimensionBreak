using UnityEngine;

public class CameraDirector : MonoBehaviour
{
    public static CameraDirector Instance;

    [Header("=== 目标引用 ===")]
    public Transform player;
    
    // 摄像机状态枚举
    public enum CamMode { SideView, FPS_Zoom, TopDown_TD }
    public CamMode currentMode = CamMode.SideView;

    [Header("=== 视角参数 ===")]
    public Vector3 sideOffset = new Vector3(0, 2, -10); // 正常横版偏移
    public float fpsZoomSize = 2.5f; // 第一人称拉近时的镜头大小
    public Vector3 fpsOffset = new Vector3(2, 0, -5); // 第一人称偏移（偏右一点）
    
    public Vector3 tdPosition; // 塔防模式摄像机固定的世界坐标（需在Inspector里填）
    public float tdSize = 8f;  // 塔防模式的视野大小

    void Awake()
    {
        Instance = this;
    }

    void LateUpdate()
    {
        if (player == null) return;

        switch (currentMode)
        {
            case CamMode.SideView:
                // 平滑跟随
                Vector3 targetPos = player.position + sideOffset;
                // 锁定Z轴，只跟XY
                transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * 5f);
                break;

            case CamMode.FPS_Zoom:
                // 模拟第一人称推进：镜头拉近主角，位置贴近
                Vector3 zoomPos = player.position + fpsOffset;
                transform.position = Vector3.Lerp(transform.position, zoomPos, Time.deltaTime * 3f);
                
                // 修改 Camera Size 来模拟变焦
                Camera.main.orthographicSize = Mathf.Lerp(Camera.main.orthographicSize, fpsZoomSize, Time.deltaTime * 2f);
                break;

            case CamMode.TopDown_TD:
                // 锁定在塔防地图上方，不动
                transform.position = Vector3.Lerp(transform.position, tdPosition, Time.deltaTime * 2f);
                transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.Euler(90, 0, 0), Time.deltaTime * 2f);
                Camera.main.orthographicSize = Mathf.Lerp(Camera.main.orthographicSize, tdSize, Time.deltaTime * 2f);
                break;
        }
    }

    // 外部调用：切换到FPS推进模式
    public void TriggerFPSZoom()
    {
        currentMode = CamMode.FPS_Zoom;
        Debug.Log("进入 FPS 推进视角");
    }

    // 外部调用：切换到塔防模式
    public void TriggerTopDown()
    {
        currentMode = CamMode.TopDown_TD;
        GameManager.Instance.isPhase1 = false; // 标记进入二阶段
        Debug.Log("进入 塔防俯视视角");
    }
}