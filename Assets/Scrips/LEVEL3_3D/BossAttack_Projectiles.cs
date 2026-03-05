using UnityEngine;
using System.Collections;

/// <summary>
/// 攻击模式 1：沙石飞弹 —— 连续生成极速飞弹朝玩家位置射去。
/// 带长拖尾，像迎面撞来的赛道碎片。Z 坐标小于玩家时自动销毁。
/// 所有数值 [SerializeField] 暴露。
/// </summary>
public class BossAttack_Projectiles : MonoBehaviour
{
    // ────────────────── 飞弹参数 ──────────────────
    [Header("=== 飞弹参数 ===")]
    [SerializeField] private int projectileCount = 8;
    [Tooltip("每颗飞弹之间的生成间隔")]
    [SerializeField] private float spawnInterval = 0.12f;
    [SerializeField] private float projectileSpeed = 60f;
    [SerializeField] private float projectileDamage = 500f;
    [SerializeField] private float projectileScale = 0.5f;

    // ────────────────── 散布 ──────────────────
    [Header("=== 散布 ===")]
    [Tooltip("X 轴随机偏移范围")]
    [SerializeField] private float spreadX = 3f;
    [Tooltip("Y 轴随机偏移范围")]
    [SerializeField] private float spreadY = 1.5f;

    // ────────────────── 拖尾视觉 ──────────────────
    [Header("=== 拖尾特效 ===")]
    [SerializeField] private Color trailColor = new Color(1f, 0.6f, 0.2f, 1f); // 沙石橙
    [SerializeField] private Color trailEndColor = new Color(0.8f, 0.3f, 0.05f, 0f);
    [SerializeField] private float trailWidth = 0.25f;
    [SerializeField] private float trailTime = 0.4f;
    [SerializeField] private Color bodyColor = new Color(0.7f, 0.5f, 0.2f, 1f); // 岩石棕

    // ══════════════════ 执行 ══════════════════

    public IEnumerator Execute(Boss3DAI boss)
    {
        Vector3 bossPos = boss.GetBossPosition();

        for (int i = 0; i < projectileCount; i++)
        {
            Vector3 playerPos = boss.GetPlayerPosition();

            // 生成位置：Boss 附近 + 随机偏移
            Vector3 spawnPos = bossPos + new Vector3(
                Random.Range(-spreadX, spreadX),
                Random.Range(0.5f, spreadY),
                0f
            );

            // 飞行方向：朝玩家当前位置（加微量散布）
            Vector3 target = playerPos + new Vector3(
                Random.Range(-0.8f, 0.8f),
                Random.Range(-0.3f, 0.3f),
                0f
            );
            Vector3 direction = (target - spawnPos).normalized;

            SpawnProjectile(spawnPos, direction, playerPos.z);

            yield return new WaitForSeconds(spawnInterval);
        }
    }

    // ══════════════════ 生成单颗飞弹 ══════════════════

    void SpawnProjectile(Vector3 position, Vector3 direction, float playerZ)
    {
        // 基础形体 — 不规则感用拉伸的 Cube
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "SandProjectile";
        go.transform.position = position;
        go.transform.localScale = new Vector3(
            projectileScale * Random.Range(0.6f, 1.4f),
            projectileScale * Random.Range(0.6f, 1f),
            projectileScale * Random.Range(1.5f, 2.5f) // Z 拉长 = 碎片感
        );
        go.transform.rotation = Quaternion.LookRotation(direction) *
            Quaternion.Euler(Random.Range(-20f, 20f), Random.Range(-20f, 20f), Random.Range(-45f, 45f));

        // 材质
        Renderer rend = go.GetComponent<Renderer>();
        if (rend != null)
        {
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", bodyColor);
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", trailColor * 1.5f);
            }
            rend.material = mat;
        }

        // Trigger
        Collider col = go.GetComponent<Collider>();
        if (col != null) col.isTrigger = true;

        // Rigidbody
        Rigidbody rb = go.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.linearVelocity = direction * projectileSpeed;

        // 随机旋转（视觉：碎片翻滚）
        rb.angularVelocity = new Vector3(
            Random.Range(-10f, 10f),
            Random.Range(-10f, 10f),
            Random.Range(-10f, 10f)
        );

        // 长拖尾
        TrailRenderer trail = go.AddComponent<TrailRenderer>();
        trail.time = trailTime;
        trail.startWidth = trailWidth;
        trail.endWidth = 0f;
        trail.material = new Material(Shader.Find("Sprites/Default"));
        trail.startColor = trailColor;
        trail.endColor = trailEndColor;
        trail.minVertexDistance = 0.1f;

        // 挂载行为脚本
        SandProjectileBehaviour behaviour = go.AddComponent<SandProjectileBehaviour>();
        behaviour.Init(projectileDamage, playerZ);
    }
}

/// <summary>
/// 沙石飞弹行为：碰到玩家造成伤害，Z 坐标越过玩家后自动销毁。
/// </summary>
public class SandProjectileBehaviour : MonoBehaviour
{
    private float damage;
    private float destroyBelowZ;
    private float spawnTime;

    public void Init(float dmg, float playerZ)
    {
        damage = dmg;
        destroyBelowZ = playerZ;
        spawnTime = Time.time;
    }

    void Update()
    {
        // Z 越过玩家 → 销毁
        if (transform.position.z < destroyBelowZ)
        {
            Destroy(gameObject);
            return;
        }

        // 超时保险
        if (Time.time - spawnTime > 8f)
            Destroy(gameObject);
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Player3DStats stats = other.GetComponent<Player3DStats>();
            if (stats != null) stats.TakeDamage(damage);

            Destroy(gameObject);
        }
    }
}
