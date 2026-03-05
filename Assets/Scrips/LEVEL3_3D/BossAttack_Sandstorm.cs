using UnityEngine;
using System.Collections;

/// <summary>
/// 攻击模式 3：沙尘暴死光 —— 预警线闪烁后，宽体沙尘暴碰撞体沿线横扫。
/// 预警线 = 赛车道方向指示灯风格的高亮闪烁线。
/// 所有数值 [SerializeField] 暴露。
/// </summary>
public class BossAttack_Sandstorm : MonoBehaviour
{
    // ────────────────── 预警线 ──────────────────
    [Header("=== 预警线 ===")]
    [Tooltip("预警持续时间")]
    [SerializeField] private float warningDuration = 1.5f;
    [SerializeField] private float lineWidth = 0.5f;
    [SerializeField] private Color lineColor = new Color(1f, 0.2f, 0.1f, 1f);    // 赛道红
    [SerializeField] private Color lineFlashColor = new Color(1f, 1f, 0.3f, 1f); // 闪烁黄
    [SerializeField] private float flashSpeed = 10f;
    [Tooltip("预警线离地高度")]
    [SerializeField] private float lineHeight = 0.2f;

    // ────────────────── 沙尘暴本体 ──────────────────
    [Header("=== 沙尘暴本体 ===")]
    [SerializeField] private float stormSpeed = 80f;
    [SerializeField] private float stormDamage = 3000f;
    [Tooltip("沙尘暴碰撞体宽度 (X)")]
    [SerializeField] private float stormWidth = 4f;
    [Tooltip("沙尘暴碰撞体高度 (Y)")]
    [SerializeField] private float stormHeight = 5f;
    [Tooltip("沙尘暴碰撞体厚度 (Z)")]
    [SerializeField] private float stormDepth = 2f;
    [SerializeField] private Color stormColor = new Color(0.9f, 0.7f, 0.3f, 0.85f); // 沙尘黄
    [SerializeField] private float stormTrailTime = 0.6f;
    [SerializeField] private float stormTrailWidth = 3f;

    // ══════════════════ 执行 ══════════════════

    public IEnumerator Execute(Boss3DAI boss)
    {
        Vector3 bossPos = boss.GetBossPosition();
        Vector3 playerPos = boss.GetPlayerPosition();

        // 预警线起止点
        Vector3 lineStart = new Vector3(playerPos.x, lineHeight, bossPos.z);
        Vector3 lineEnd = new Vector3(playerPos.x, lineHeight, playerPos.z - 3f);

        // ─── 创建预警线 ───
        GameObject lineObj = new GameObject("StormWarningLine");
        LineRenderer lr = lineObj.AddComponent<LineRenderer>();
        lr.positionCount = 2;
        lr.SetPosition(0, lineStart);
        lr.SetPosition(1, lineEnd);
        lr.startWidth = lineWidth;
        lr.endWidth = lineWidth;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = lineColor;
        lr.endColor = lineColor;

        // 预警线闪烁
        float elapsed = 0f;
        while (elapsed < warningDuration)
        {
            elapsed += Time.deltaTime;

            // 赛道指示灯风格：分段闪烁
            float t = Mathf.PingPong(elapsed * flashSpeed, 1f);
            Color c = Color.Lerp(lineColor, lineFlashColor, t);
            lr.startColor = c;
            lr.endColor = c;

            // 宽度脉冲
            float widthPulse = lineWidth * (1f + Mathf.Sin(elapsed * flashSpeed * 2f) * 0.3f);
            lr.startWidth = widthPulse;
            lr.endWidth = widthPulse;

            yield return null;
        }

        // 最后一刻全白闪烁
        lr.startColor = Color.white;
        lr.endColor = Color.white;
        lr.startWidth = lineWidth * 2f;
        lr.endWidth = lineWidth * 2f;
        yield return new WaitForSeconds(0.1f);

        // 销毁预警线
        Destroy(lineObj);

        // ─── 生成沙尘暴碰撞体 ───
        SpawnStormWall(lineStart, lineEnd, playerPos.z);

        yield return new WaitForSeconds(0.5f); // 留时间让沙尘暴飞过
    }

    // ══════════════════ 沙尘暴本体 ══════════════════

    void SpawnStormWall(Vector3 start, Vector3 end, float playerZ)
    {
        GameObject storm = GameObject.CreatePrimitive(PrimitiveType.Cube);
        storm.name = "SandstormWall";
        storm.transform.position = start + Vector3.up * (stormHeight * 0.5f);
        storm.transform.localScale = new Vector3(stormWidth, stormHeight, stormDepth);

        // 半透明沙尘材质
        Renderer rend = storm.GetComponent<Renderer>();
        if (rend != null)
        {
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetFloat("_Surface", 1f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.renderQueue = 3000;
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", stormColor);
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", stormColor * 2f);
            }
            rend.material = mat;
        }

        // Trigger
        Collider col = storm.GetComponent<Collider>();
        if (col != null) col.isTrigger = true;

        // Rigidbody
        Rigidbody rb = storm.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        // 飞行方向：从 Boss → 玩家（沿 Z 负方向）
        Vector3 dir = (end - start).normalized;
        rb.linearVelocity = dir * stormSpeed;

        // 粗壮拖尾
        TrailRenderer trail = storm.AddComponent<TrailRenderer>();
        trail.time = stormTrailTime;
        trail.startWidth = stormTrailWidth;
        trail.endWidth = stormTrailWidth * 0.5f;
        trail.material = new Material(Shader.Find("Sprites/Default"));
        Color trailC = stormColor;
        trailC.a = 0.5f;
        trail.startColor = trailC;
        trail.endColor = new Color(trailC.r, trailC.g, trailC.b, 0f);
        trail.minVertexDistance = 0.2f;

        // 行为
        StormWallBehaviour behaviour = storm.AddComponent<StormWallBehaviour>();
        behaviour.Init(stormDamage, playerZ);
    }
}

/// <summary>
/// 沙尘暴墙行为：碰到玩家造成伤害，越过玩家 Z 坐标后销毁。
/// </summary>
public class StormWallBehaviour : MonoBehaviour
{
    private float damage;
    private float destroyBelowZ;
    private float spawnTime;
    private bool hasHitPlayer;

    public void Init(float dmg, float playerZ)
    {
        damage = dmg;
        destroyBelowZ = playerZ - 5f; // 多飞一段距离再销毁
        spawnTime = Time.time;
    }

    void Update()
    {
        if (transform.position.z < destroyBelowZ)
        {
            Destroy(gameObject);
            return;
        }
        if (Time.time - spawnTime > 6f)
            Destroy(gameObject);
    }

    void OnTriggerEnter(Collider other)
    {
        if (hasHitPlayer) return;
        if (!other.CompareTag("Player")) return;

        hasHitPlayer = true;
        Player3DStats stats = other.GetComponent<Player3DStats>();
        if (stats != null) stats.TakeDamage(damage);
    }
}
