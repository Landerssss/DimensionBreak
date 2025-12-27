using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class GameDirector : MonoBehaviour
{
    [Header("=== 核心对象 ===")]
    public PlayerController player;
    public Camera mainCamera;
    public Transform tdSpawnPoint; // 主角跳下去后的落脚点

    [Header("=== 摄像机机位 (用空物体定位) ===")]
    public Transform camPos_SideView; // P1: 侧视
    public Transform camPos_LedgeLookDown; // P2: 高台俯视 (图2)
    public Transform camPos_TDView; // P3: 塔防视角 (图3)

    [Header("=== UI 引用 ===")]
    public GameObject jumpButtonWorldUI; // "次元跳跃"按钮
    public GameObject jumpHintUI; // "按空格跳跃" 提示
    public GameObject tdCardPanel; // 塔防卡牌栏 (图4)

    // 状态枚举
    public enum GameState { Phase1, LedgeWait, Jumping, Phase2_TD }
    public GameState currentState = GameState.Phase1;

    void Start()
    {
        // 初始化状态
        jumpButtonWorldUI.SetActive(false);
        jumpHintUI.SetActive(false);
        tdCardPanel.SetActive(false);
    }

    void Update()
    {
        // 只有在俯视等待阶段，才允许按空格跳跃
        if (currentState == GameState.LedgeWait)
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                StartCoroutine(PerformTheJump());
            }
        }
    }

    // === 外部调用：当玩家走到第一阶段终点触发 ===
    public void OnReachPhase1End()
    {
        jumpButtonWorldUI.SetActive(true); // 显示"次元跳跃"按钮
    }

    // === 外部调用：点击了"次元跳跃"按钮 ===
    public void OnClickDimensionJump()
    {
        jumpButtonWorldUI.SetActive(false);
        StartCoroutine(SwitchToLedgeView());
    }

    // 协程 1: 切换到俯视视角
    IEnumerator SwitchToLedgeView()
    {
        // 1. 冻结主角
        player.SetVelocity(Vector2.zero);
        player.enabled = false; 

        // 2. 运镜：移到悬崖边往下看
        float t = 0;
        Vector3 startPos = mainCamera.transform.position;
        Quaternion startRot = mainCamera.transform.rotation;

        while (t < 1f)
        {
            t += Time.deltaTime * 2f; // 0.5秒转过去
            mainCamera.transform.position = Vector3.Lerp(startPos, camPos_LedgeLookDown.position, t);
            mainCamera.transform.rotation = Quaternion.Lerp(startRot, camPos_LedgeLookDown.rotation, t);
            yield return null;
        }

        // 3. 显示提示
        jumpHintUI.SetActive(true); // "按空格跳跃"
        currentState = GameState.LedgeWait;
    }

    // 协程 2: 信仰之跃！
    IEnumerator PerformTheJump()
    {
        currentState = GameState.Jumping;
        jumpHintUI.SetActive(false);

        // 1. 让主角做一个跳跃动作 (物理位移或动画)
        // 这里简单处理：直接把主角瞬移到半空中，打开重力让它掉下去
        // 或者做一个优雅的抛物线动画，这里简化为直接落地逻辑
        player.transform.position = tdSpawnPoint.position; // 瞬移到下方
        player.enabled = true; // 恢复控制
        
        // 2. 运镜：跟到塔防视角
        float t = 0;
        Vector3 startPos = mainCamera.transform.position;
        Quaternion startRot = mainCamera.transform.rotation;

        while (t < 1f)
        {
            t += Time.deltaTime * 1.5f;
            mainCamera.transform.position = Vector3.Lerp(startPos, camPos_TDView.position, t);
            mainCamera.transform.rotation = Quaternion.Lerp(startRot, camPos_TDView.rotation, t);
            yield return null;
        }

        // 3. 游戏正式进入塔防阶段
        currentState = GameState.Phase2_TD;
        tdCardPanel.SetActive(true); // 显示卡牌栏
        
        // TODO: 激活刷怪器
    }
}
