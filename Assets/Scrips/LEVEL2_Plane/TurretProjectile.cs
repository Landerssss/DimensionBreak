using UnityEngine;

/// <summary>
/// 炮弹：从炮台飞向目标敌人，到达后对敌人造成伤害并自毁。
/// 圆形视觉，使用代码生成的圆形 Sprite（或挂载的 SpriteRenderer）。
/// </summary>
public class TurretProjectile : MonoBehaviour
{
    [Header("=== 飞行参数 ===")]
    [Tooltip("炮弹飞行速度（世界单位/秒）")]
    [SerializeField] private float speed = 5f;

    [Header("=== 视觉 ===")]
    [Tooltip("炮弹颜色")]
    [SerializeField] private Color projectileColor = new Color(1f, 0.6f, 0f, 1f); // 橙色
    [Tooltip("炮弹半径")]
    [SerializeField] private float radius = 0.15f;

    // ────────────────── 内部 ──────────────────
    private PaperEnemy target;
    private Vector3 targetLastPos;
    private bool initialized;
    private SpriteRenderer sr;

    /// <summary>
    /// 初始化炮弹：设定目标
    /// </summary>
    public void Init(PaperEnemy targetEnemy, float flySpeed = -1f)
    {
        target = targetEnemy;
        if (target != null)
            targetLastPos = target.transform.position;

        if (flySpeed > 0f)
            speed = flySpeed;

        initialized = true;
    }

    void Awake()
    {
        // 设置视觉：圆形 Sprite
        sr = GetComponent<SpriteRenderer>();
        if (sr == null)
            sr = gameObject.AddComponent<SpriteRenderer>();

        // 创建圆形纹理
        sr.sprite = CreateCircleSprite();
        sr.color = projectileColor;
        sr.sortingOrder = 100; // 确保炮弹在最上层

        // 设置大小
        transform.localScale = Vector3.one * radius * 2f;
    }

    void Update()
    {
        if (!initialized) return;

        // 更新目标位置（敌人可能在移动动画中）
        if (target != null && !target.IsDead)
            targetLastPos = target.transform.position;

        // 飞向目标
        Vector3 dir = (targetLastPos - transform.position);
        float distThisFrame = speed * Time.deltaTime;

        if (dir.magnitude <= distThisFrame)
        {
            // 到达目标
            OnArrived();
            return;
        }

        transform.position += dir.normalized * distThisFrame;

        // 旋转朝向移动方向（视觉效果）
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, angle);
    }

    void OnArrived()
    {
        // 对目标造成伤害
        if (target != null && !target.IsDead)
        {
            target.TakeHit();
            Debug.Log($"[TurretProjectile] 炮弹命中 {target.gameObject.name}!");
        }

        Destroy(gameObject);
    }

    /// <summary>
    /// 代码生成一个简单的白色圆形 Sprite（运行时使用）
    /// </summary>
    Sprite CreateCircleSprite()
    {
        int texSize = 64;
        Texture2D tex = new Texture2D(texSize, texSize, TextureFormat.RGBA32, false);
        float center = texSize / 2f;
        float radiusPx = center - 1f;

        for (int y = 0; y < texSize; y++)
        {
            for (int x = 0; x < texSize; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                if (dist <= radiusPx)
                    tex.SetPixel(x, y, Color.white);
                else
                    tex.SetPixel(x, y, Color.clear);
            }
        }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, texSize, texSize), new Vector2(0.5f, 0.5f), texSize);
    }
}
