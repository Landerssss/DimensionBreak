using UnityEngine;

/// <summary>
/// 通用 3D 发射物：直线/弧线飞行，碰到 "Boss" 标签物体造成伤害并销毁。
/// 由 Player3DWeapons 在运行时 AddComponent 并调用 Init。
/// </summary>
public class Projectile3D : MonoBehaviour
{
    // 由 Init 设置
    private float damage;
    private float lifetime;
    private float speed;
    private Vector3 direction;
    private bool isHeavy;

    private float spawnTime;
    private Rigidbody rb;

    // ────────────────── 弧线矫正 ──────────────────
    // 水魔爆有初始侧向力，飞行一段后需要矫正回正前方
    private bool needsCorrection;
    private float correctionStartTime;
    [Tooltip("弧线矫正开始时间（秒后）")]
    private float correctionDelay = 0.3f;
    private float correctionStrength = 15f;

    // ══════════════════ 初始化 ══════════════════

    public void Init(float dmg, float life, float spd, Vector3 dir, bool heavy)
    {
        damage = dmg;
        lifetime = life;
        speed = spd;
        direction = dir;
        isHeavy = heavy;
        needsCorrection = heavy; // 水魔爆需要弧线矫正

        spawnTime = Time.time;
        rb = GetComponent<Rigidbody>();
    }

    // ══════════════════ 生命周期 ══════════════════

    void Update()
    {
        // 超时销毁
        if (Time.time - spawnTime > lifetime)
        {
            Destroy(gameObject);
            return;
        }

        // 水魔爆弧线矫正：飞行一段后逐渐转向正前方
        if (needsCorrection && rb != null && Time.time - spawnTime > correctionDelay)
        {
            Vector3 targetVel = direction * speed;
            rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, targetVel, Time.deltaTime * correctionStrength);
        }
    }

    // ══════════════════ 碰撞 ══════════════════

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Boss"))
        {
            // 对 BossSceneManager 造成伤害
            if (BossSceneManager.Instance != null)
            {
                BossSceneManager.Instance.DamageBoss(damage);
            }

            // 击中特效 — 缩放闪烁后销毁
            SpawnHitFlash(other.ClosestPoint(transform.position));
            Destroy(gameObject);
        }
    }

    // ══════════════════ 击中闪光 ══════════════════

    void SpawnHitFlash(Vector3 hitPoint)
    {
        // 创建一个短暂的发光球体
        GameObject flash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        flash.name = "HitFlash";
        flash.transform.position = hitPoint;

        float flashScale = isHeavy ? 1.5f : 0.4f;
        flash.transform.localScale = Vector3.one * flashScale;

        // 移除碰撞器
        Collider col = flash.GetComponent<Collider>();
        if (col != null) Destroy(col);

        // 自发光材质
        Renderer rend = flash.GetComponent<Renderer>();
        if (rend != null)
        {
            Color flashColor = isHeavy
                ? new Color(0.3f, 0.6f, 1f, 1f)
                : new Color(0.5f, 1f, 0.9f, 1f);

            Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", flashColor);
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", flashColor * 5f);
            }
            rend.material = mat;
        }

        // 缩放动画后销毁
        Destroy(flash, 0.15f);
    }
}
