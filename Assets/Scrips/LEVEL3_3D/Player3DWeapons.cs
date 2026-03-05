using UnityEngine;

/// <summary>
/// Phase 3 武器系统：纯代码生成发射物（无手模式）。
/// 左键 = 弓箭（高频直线，中央准星发射，快速曳光弹拖尾）。
/// 右键/E = 水魔爆（两侧大型水弹，弧线飞行，粗壮拖尾，长CD）。
/// </summary>
public class Player3DWeapons : MonoBehaviour
{
    // ────────────────── 弓箭 (左键) ──────────────────
    [Header("=== 弓箭（左键 · 高频直线） ===")]
    [SerializeField] private float arrowFireRate = 0.08f;
    [SerializeField] private float arrowSpeed = 120f;
    [SerializeField] private float arrowDamage = 50f;
    [SerializeField] private float arrowLifetime = 3f;

    [Header("=== 弓箭视觉 ===")]
    [SerializeField] private Color arrowColor = new Color(0.3f, 1f, 0.9f, 1f); // 青色激光
    [SerializeField] private Color arrowTrailEndColor = new Color(0.1f, 0.4f, 1f, 0f);
    [SerializeField] private float arrowScale = 0.15f;
    [SerializeField] private float arrowTrailWidth = 0.08f;
    [SerializeField] private float arrowTrailTime = 0.12f;

    // ────────────────── 水魔爆 (右键/E · 两侧弧线) ──────────────────
    [Header("=== 水魔爆（右键/E · 两侧弧线） ===")]
    [SerializeField] private float waterBombCooldown = 3f;
    [SerializeField] private float waterBombSpeed = 40f;
    [SerializeField] private float waterBombDamage = 800f;
    [SerializeField] private float waterBombLifetime = 4f;
    [Tooltip("水弹从摄像机两侧偏移的 X 距离")]
    [SerializeField] private float waterBombSideOffset = 2.5f;
    [Tooltip("水弹初始向外弧度的力度")]
    [SerializeField] private float waterBombArcForce = 8f;

    [Header("=== 水魔爆视觉 ===")]
    [SerializeField] private Color waterBombColor = new Color(0.2f, 0.5f, 1f, 1f); // 深蓝水
    [SerializeField] private Color waterBombTrailEndColor = new Color(0f, 0.2f, 0.8f, 0f);
    [SerializeField] private float waterBombScale = 0.6f;
    [SerializeField] private float waterBombTrailWidth = 0.4f;
    [SerializeField] private float waterBombTrailTime = 0.5f;

    // ────────────────── 引用 ──────────────────
    [Header("=== 引用 ===")]
    [SerializeField] private Camera playerCamera;

    // ────────────────── 内部 ──────────────────
    private float lastArrowTime;
    private float lastWaterBombTime = -999f;

    // ══════════════════ 生命周期 ══════════════════

    void Start()
    {
        if (playerCamera == null)
            playerCamera = Camera.main;
    }

    void Update()
    {
        // 受 BossSceneManager 输入锁控制
        if (BossSceneManager.Instance != null && BossSceneManager.Instance.IsPlayerInputLocked)
            return;

        // 战斗阶段才允许射击
        if (BossSceneManager.Instance != null &&
            BossSceneManager.Instance.CurrentPhase != BossSceneManager.BossPhase.Fighting)
            return;

        // 左键 → 弓箭（高频连射）
        if (Input.GetMouseButton(0) && Time.time >= lastArrowTime + arrowFireRate)
        {
            lastArrowTime = Time.time;
            FireArrow();
        }

        // 右键/E → 水魔爆
        if ((Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.E)) &&
            Time.time >= lastWaterBombTime + waterBombCooldown)
        {
            lastWaterBombTime = Time.time;
            FireWaterBomb();
        }
    }

    // ══════════════════ 弓箭 ══════════════════

    void FireArrow()
    {
        // 从摄像机正中央发射
        Vector3 spawnPos = playerCamera.transform.position + playerCamera.transform.forward * 1.5f;

        // 微小随机散布增加射击质感
        spawnPos += playerCamera.transform.right * Random.Range(-0.05f, 0.05f);
        spawnPos += playerCamera.transform.up * Random.Range(-0.03f, 0.03f);

        Vector3 direction = playerCamera.transform.forward;

        CreateProjectile(
            spawnPos, direction,
            arrowSpeed, arrowDamage, arrowLifetime,
            arrowColor, arrowTrailEndColor,
            arrowScale, arrowTrailWidth, arrowTrailTime,
            isHeavy: false
        );
    }

    // ══════════════════ 水魔爆 ══════════════════

    void FireWaterBomb()
    {
        Vector3 camPos = playerCamera.transform.position;
        Vector3 camRight = playerCamera.transform.right;
        Vector3 camForward = playerCamera.transform.forward;
        Vector3 camDown = -playerCamera.transform.up;

        // 左侧水弹
        Vector3 leftPos = camPos - camRight * waterBombSideOffset + camDown * 0.5f;
        Vector3 leftDir = (camForward + camRight * 0.3f).normalized; // 略微内收汇聚
        CreateProjectile(
            leftPos, leftDir,
            waterBombSpeed, waterBombDamage, waterBombLifetime,
            waterBombColor, waterBombTrailEndColor,
            waterBombScale, waterBombTrailWidth, waterBombTrailTime,
            isHeavy: true,
            arcForce: waterBombArcForce, arcDir: camRight // 先外弧再内收
        );

        // 右侧水弹
        Vector3 rightPos = camPos + camRight * waterBombSideOffset + camDown * 0.5f;
        Vector3 rightDir = (camForward - camRight * 0.3f).normalized;
        CreateProjectile(
            rightPos, rightDir,
            waterBombSpeed, waterBombDamage, waterBombLifetime,
            waterBombColor, waterBombTrailEndColor,
            waterBombScale, waterBombTrailWidth, waterBombTrailTime,
            isHeavy: true,
            arcForce: waterBombArcForce, arcDir: -camRight
        );
    }

    // ══════════════════ 子弹生成 ══════════════════

    void CreateProjectile(
        Vector3 position, Vector3 direction,
        float speed, float damage, float lifetime,
        Color color, Color trailEndColor,
        float scale, float trailWidth, float trailTime,
        bool isHeavy,
        float arcForce = 0f, Vector3 arcDir = default)
    {
        // 创建基础 GameObject
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = isHeavy ? "WaterBomb" : "Arrow";
        go.transform.position = position;
        go.transform.localScale = Vector3.one * scale;
        go.layer = gameObject.layer;

        // 材质 — 自发光
        Renderer rend = go.GetComponent<Renderer>();
        if (rend != null)
        {
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", color * 3f);
            }
            rend.material = mat;
        }

        // Collider 设为 Trigger
        Collider col = go.GetComponent<Collider>();
        if (col != null) col.isTrigger = true;

        // Rigidbody（无重力）
        Rigidbody rb = go.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.linearVelocity = direction * speed;

        // TrailRenderer 拖尾
        TrailRenderer trail = go.AddComponent<TrailRenderer>();
        trail.time = trailTime;
        trail.startWidth = trailWidth;
        trail.endWidth = isHeavy ? trailWidth * 0.4f : 0f;
        trail.material = new Material(Shader.Find("Sprites/Default"));
        trail.startColor = color;
        trail.endColor = trailEndColor;
        trail.minVertexDistance = 0.05f;

        // 第二层粗拖尾（水魔爆专属）
        if (isHeavy)
        {
            TrailRenderer trail2 = go.AddComponent<TrailRenderer>();
            trail2.time = trailTime * 1.5f;
            trail2.startWidth = trailWidth * 2f;
            trail2.endWidth = 0f;
            Color glowColor = color;
            glowColor.a = 0.3f;
            trail2.startColor = glowColor;
            trail2.endColor = new Color(glowColor.r, glowColor.g, glowColor.b, 0f);
            trail2.material = new Material(Shader.Find("Sprites/Default"));
            trail2.minVertexDistance = 0.1f;
        }

        // 发射弧线力（水魔爆初始外弧）
        if (arcForce > 0f && arcDir != default)
        {
            rb.AddForce(arcDir * arcForce, ForceMode.Impulse);
        }

        // 挂载 Projectile3D 脚本
        Projectile3D proj = go.AddComponent<Projectile3D>();
        proj.Init(damage, lifetime, speed, direction, isHeavy);
    }
}
