using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 攻击模式 2：流沙深坑 AOE —— 预警圈蔓延式生成。
/// Boss附近 → 地图中间 → 玩家位置。高亮鲜红预警圈 + 伤害判定。
/// 所有数值 [SerializeField] 暴露。
/// </summary>
public class BossAttack_SandPits : MonoBehaviour
{
    // ────────────────── 深坑参数 ──────────────────
    [Header("=== 深坑参数 ===")]
    [SerializeField] private float pitRadius = 1.8f;
    [SerializeField] private float pitDamage = 1200f;
    [Tooltip("坑的持续时间")]
    [SerializeField] private float pitDuration = 3f;
    [Tooltip("预警圈显示时间（伤害延迟）")]
    [SerializeField] private float warningDuration = 0.8f;

    // ────────────────── 蔓延节奏 ──────────────────
    [Header("=== 蔓延节奏 ===")]
    [Tooltip("第一波（Boss附近）坑数量")]
    [SerializeField] private int wave1Count = 2;
    [Tooltip("第二波（中间区域）坑数量")]
    [SerializeField] private int wave2Count = 3;
    [Tooltip("第三波（玩家附近）坑数量")]
    [SerializeField] private int wave3Count = 2;
    [Tooltip("波次之间的延迟")]
    [SerializeField] private float wavePause = 1f;

    // ────────────────── 区域定义 ──────────────────
    [Header("=== 区域 ===")]
    [SerializeField] private float bossZoneZMin = 15f;
    [SerializeField] private float bossZoneZMax = 22f;
    [SerializeField] private float midZoneZMin = 6f;
    [SerializeField] private float midZoneZMax = 14f;
    [SerializeField] private float xRange = 5f;
    [Tooltip("玩家附近的散布半径")]
    [SerializeField] private float playerZoneSpread = 3f;

    // ────────────────── 视觉 ──────────────────
    [Header("=== 视觉 ===")]
    [SerializeField] private Color warningColor = new Color(1f, 0.1f, 0.1f, 0.7f); // 鲜红高亮
    [SerializeField] private Color activeColor = new Color(0.8f, 0.2f, 0f, 0.9f);  // 深红橙
    [SerializeField] private float warningFlashSpeed = 12f;
    [SerializeField] private float pitHeight = 0.05f; // 贴地薄片

    // ══════════════════ 执行 ══════════════════

    public IEnumerator Execute(Boss3DAI boss)
    {
        // 第一波：Boss 附近
        SpawnWave(wave1Count, bossZoneZMin, bossZoneZMax, Vector3.zero);
        yield return new WaitForSeconds(wavePause);

        // 第二波：地图中间
        SpawnWave(wave2Count, midZoneZMin, midZoneZMax, Vector3.zero);
        yield return new WaitForSeconds(wavePause);

        // 第三波：玩家当前位置附近
        Vector3 playerPos = boss.GetPlayerPosition();
        SpawnWave(wave3Count, playerPos.z - playerZoneSpread, playerPos.z + playerZoneSpread, playerPos);
        yield return new WaitForSeconds(pitDuration);
    }

    // ══════════════════ 波次生成 ══════════════════

    void SpawnWave(int count, float zMin, float zMax, Vector3 center)
    {
        for (int i = 0; i < count; i++)
        {
            float x = center != Vector3.zero
                ? center.x + Random.Range(-playerZoneSpread, playerZoneSpread)
                : Random.Range(-xRange, xRange);
            float z = Random.Range(zMin, zMax);

            Vector3 pos = new Vector3(x, pitHeight, z);
            StartCoroutine(SpawnPit(pos));
        }
    }

    // ══════════════════ 单个深坑 ══════════════════

    IEnumerator SpawnPit(Vector3 position)
    {
        // 创建预警圈（扁平圆柱）
        GameObject pit = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pit.name = "SandPit_Warning";
        pit.transform.position = position;
        pit.transform.localScale = new Vector3(pitRadius * 2f, pitHeight, pitRadius * 2f);

        // 移除默认碰撞器
        Collider defaultCol = pit.GetComponent<Collider>();
        if (defaultCol != null) Destroy(defaultCol);

        // 预警材质
        Renderer rend = pit.GetComponent<Renderer>();
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.SetFloat("_Surface", 1f); // Transparent
        mat.SetFloat("_Blend", 0f);
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.renderQueue = 3000;
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", warningColor);
        if (mat.HasProperty("_EmissionColor"))
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", warningColor * 4f);
        }
        rend.material = mat;

        // 预警闪烁阶段
        float elapsed = 0f;
        while (elapsed < warningDuration)
        {
            elapsed += Time.deltaTime;
            float flash = Mathf.Abs(Mathf.Sin(elapsed * warningFlashSpeed));
            Color c = Color.Lerp(warningColor, Color.white, flash * 0.6f);
            c.a = warningColor.a;
            mat.SetColor("_BaseColor", c);
            if (mat.HasProperty("_EmissionColor"))
                mat.SetColor("_EmissionColor", c * 4f);
            yield return null;
        }

        // 激活伤害 —— 变为深色 + 添加碰撞器
        mat.SetColor("_BaseColor", activeColor);
        if (mat.HasProperty("_EmissionColor"))
            mat.SetColor("_EmissionColor", activeColor * 2f);

        // 球形触发器做伤害判定
        SphereCollider trigger = pit.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        // Cylinder 默认高度2 半径0.5，scale 后换算
        trigger.radius = 0.5f;
        trigger.center = Vector3.zero;

        SandPitDamageZone zone = pit.AddComponent<SandPitDamageZone>();
        zone.Init(pitDamage);

        pit.name = "SandPit_Active";

        // 持续一段时间后销毁
        yield return new WaitForSeconds(pitDuration);
        Destroy(pit);
    }
}

/// <summary>
/// 流沙深坑伤害区域：玩家踩入时持续造成伤害。
/// </summary>
public class SandPitDamageZone : MonoBehaviour
{
    private float damage;
    private float damageInterval = 0.5f;
    private float lastDamageTime;

    public void Init(float dmg)
    {
        damage = dmg;
    }

    void OnTriggerStay(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (Time.time - lastDamageTime < damageInterval) return;

        lastDamageTime = Time.time;
        Player3DStats stats = other.GetComponent<Player3DStats>();
        if (stats != null) stats.TakeDamage(damage);
    }
}
