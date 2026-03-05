using UnityEngine;

/// <summary>
/// Phase 3 第一人称固定视角玩家控制器。
/// 永远面朝 Z 轴正方向（朝 Boss），禁止鼠标转视角。
/// WASD 在受限矩形范围内平移躲避弹幕。
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class Player3DController : MonoBehaviour
{
    // ────────────────── 移动 ──────────────────
    [Header("=== 移动 ===")]
    [SerializeField] private float moveSpeed = 12f;
    [Tooltip("高速闪避（Shift 加速）")]
    [SerializeField] private float sprintMultiplier = 1.8f;

    // ────────────────── 移动边界 ──────────────────
    [Header("=== 移动范围限制 ===")]
    [SerializeField] private float minX = -6f;
    [SerializeField] private float maxX = 6f;
    [SerializeField] private float minZ = -2f;
    [SerializeField] private float maxZ = 8f;

    // ────────────────── 视觉反馈 ──────────────────
    [Header("=== 移动视觉 ===")]
    [Tooltip("侧移时相机微倾角度")]
    [SerializeField] private float tiltAngle = 3f;
    [SerializeField] private float tiltSpeed = 8f;

    // ────────────────── FOV 呼吸 ──────────────────
    [Header("=== FOV 动态 ===")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private float baseFOV = 70f;
    [Tooltip("移动/冲刺时 FOV 增量")]
    [SerializeField] private float sprintFOVBoost = 8f;
    [SerializeField] private float fovLerpSpeed = 5f;

    // ────────────────── 内部 ──────────────────
    private CharacterController cc;
    private float currentTilt;
    private float targetFOV;

    // ══════════════════ 生命周期 ══════════════════

    void Start()
    {
        cc = GetComponent<CharacterController>();

        // 锁定光标
        Cursor.lockState = CursorLockMode.Confined;

        // 强制面朝 Z 正方向
        transform.rotation = Quaternion.Euler(0f, 0f, 0f);

        if (playerCamera == null)
            playerCamera = Camera.main;

        targetFOV = baseFOV;
    }

    void Update()
    {
        // 受 BossSceneManager 输入锁控制
        if (BossSceneManager.Instance != null && BossSceneManager.Instance.IsPlayerInputLocked)
            return;

        HandleMovement();
        HandleTilt();
        HandleFOV();

        // 强制锁定朝向
        transform.rotation = Quaternion.Euler(0f, 0f, 0f);
    }

    // ══════════════════ 移动 ══════════════════

    void HandleMovement()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        bool sprinting = Input.GetKey(KeyCode.LeftShift);
        float speed = sprinting ? moveSpeed * sprintMultiplier : moveSpeed;

        Vector3 move = new Vector3(h, 0f, v).normalized * speed;
        cc.Move(move * Time.deltaTime);

        // 钳制到边界
        Vector3 pos = transform.position;
        pos.x = Mathf.Clamp(pos.x, minX, maxX);
        pos.z = Mathf.Clamp(pos.z, minZ, maxZ);
        transform.position = pos;

        // FOV
        targetFOV = sprinting && (h != 0 || v != 0)
            ? baseFOV + sprintFOVBoost
            : baseFOV;
    }

    // ══════════════════ 侧倾 ══════════════════

    void HandleTilt()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float targetTilt = -h * tiltAngle; // 左移右倾，右移左倾
        currentTilt = Mathf.Lerp(currentTilt, targetTilt, Time.deltaTime * tiltSpeed);

        if (playerCamera != null)
        {
            Vector3 euler = playerCamera.transform.localEulerAngles;
            euler.z = currentTilt;
            playerCamera.transform.localEulerAngles = euler;
        }
    }

    // ══════════════════ FOV ══════════════════

    void HandleFOV()
    {
        if (playerCamera != null)
        {
            playerCamera.fieldOfView = Mathf.Lerp(
                playerCamera.fieldOfView, targetFOV, Time.deltaTime * fovLerpSpeed);
        }
    }

    // ══════════════════ Gizmos ══════════════════

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Vector3 center = new Vector3((minX + maxX) / 2f, 0.5f, (minZ + maxZ) / 2f);
        Vector3 size = new Vector3(maxX - minX, 0.1f, maxZ - minZ);
        Gizmos.DrawWireCube(center, size);
    }
}
