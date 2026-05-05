using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// 优化版玩家控制器：包含基础移动 / 二段跳 / 冲刺 / 攻击 / 虚空钩锁 / 下坠攻击
/// 所有数值均通过 [SerializeField] 暴露到 Inspector。
/// </summary>
public class PlayerController : MonoBehaviour
{
    // ────────────────── 基础移动 ──────────────────
    [Header("=== 基础移动 ===")]
    [SerializeField] private float moveSpeed = 8f;
    [SerializeField] private float jumpForce = 16f;
    [SerializeField] public LayerMask groundLayer;  //改为公有
    [SerializeField] public Transform groundCheck;
    [SerializeField] private float groundCheckRadius = 0.2f;

    // ────────────────── 二段跳 (等级解锁) ──────────────────
    [Header("=== 二段跳解锁 ===")]
    [Tooltip("达到此等级后解锁二段跳")]
    [SerializeField] private int doubleJumpUnlockLevel = 3;
    private int maxJumps = 1;      // 当前最大跳跃次数
    private int currentJumps = 0;  // 当前已跳跃次数

    // ────────────────── 冲刺 (Shift) ──────────────────
    [Header("=== 冲刺 ===")]
    [SerializeField] private float dashSpeed = 25f;
    [SerializeField] private float dashDuration = 0.2f;
    [SerializeField] private float dashCooldown = 2.0f;
    [SerializeField] private float dashAttackWidth = 1f;
    [SerializeField] private LayerMask enemyLayer;

    [Header("=== 冲刺 CD UI ===")]
    [SerializeField] private Image     dashCdMask;
    [SerializeField] private TextMeshProUGUI dashCdText;

    // ────────────────── 近战攻击 (左键) ──────────────────
    [Header("=== 近战攻击 ===")]
    [SerializeField] private float attackRange = 1.5f;
    [SerializeField] private Transform attackPoint;
    [SerializeField] private float attackCooldown = 0.35f;

    // ────────────────── 虚空钩锁 (T / 右键) ──────────────────
    [Header("=== 虚空钩锁 ===")]
    [SerializeField] private float grappleSpeed = 30f;
    [SerializeField] private float grappleMaxDistance = 10f;
    [SerializeField] private float grappleArriveThreshold = 0.5f;
    [SerializeField] private LineRenderer grappleLineRenderer;
    [Tooltip("钩锁线段起点（拖入角色手部骨骼节点）；留空则自动使用角色中心")]
    [SerializeField] private Transform handPoint;
    [SerializeField] private float hookCooldownTime = 2.0f;

    [Header("=== 钩锁 CD UI ===")]
    [SerializeField] private Image     hookCdMask;
    [SerializeField] private TextMeshProUGUI hookCdText;

    // ────────────────── 下坠攻击 (空中单压S) ──────────────────
    [Header("=== 下坠攻击 ===")]
    [SerializeField] private float diveSpeed = 45f;
    [SerializeField] private float diveAOERadius = 2f;
    [SerializeField] private float diveDamageMultiplier = 1.5f;
    [SerializeField] private float diveCooldown = 2.0f;

    [Header("=== 下坠 CD UI ===")]
    [SerializeField] private Image     diveCdMask;
    [SerializeField] private TextMeshProUGUI diveCdText;

    // ────────────────── 弓箭 (Sword Projectile) ──────────────────
    [Header("=== 弓箭 (Sword Projectile) ===")]
    [SerializeField] private float bowCooldown = 0.5f;
    [SerializeField] private float bowProjectileSpeed = 20f;
    [SerializeField] private float bowMaxDistance = 15f;
    [Tooltip("可选：如果没有提供预制体，代码将动态生成蓝色长矩形")]
    [SerializeField] private GameObject swordPrefab;

    // ────────────────── 水魔爆 (Water Bomb) ──────────────────
    [Header("=== 水魔爆 (Water Bomb) ===")]
    [SerializeField] private float waterBombCooldown = 3.0f;
    [SerializeField] private float waterBombWidth = 2f;
    [Tooltip("可选：如果没有提供预制体，代码将动态生成水柱特效")]
    [SerializeField] private GameObject waterBombPrefab;

    [Header("=== 水魔爆 CD UI ===")]
    [SerializeField] private Image waterCdMask;
    [SerializeField] private TextMeshProUGUI waterCdText;

    // ────────────────── 内部状态 ──────────────────
    private Rigidbody2D rb;
    private bool isGrounded;
    private bool isFacingRight = true;
    private float defaultGravity;

    // 冲刺状态
    private bool isDashing;
    private float lastDashTime  = -999f;   // 上次冲刺时间
    public bool IsDashing => isDashing;

    // 下坠状态
    public bool IsDiving { get; private set; }
    private float lastDiveTime  = -999f;   // 上次下坠时间
    private float lastAttackTime = -999f;

    // 钩锁状态
    private bool isGrappling;
    private float lastHookTime  = -999f;   // 上次钩锁时间
    private Vector2 grappleTarget;

    // 新增技能状态
    private float lastBowTime = -999f;
    private float lastWaterBombTime = -999f;

    // 引用
    private PlayerStats playerStats;

    // ══════════════════ 生命周期 ══════════════════

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        defaultGravity = rb.gravityScale;
        playerStats = GetComponent<PlayerStats>();

        if (grappleLineRenderer != null)
        {
            grappleLineRenderer.enabled = false;

            // ── 自动补全材质（面板 Materials 列表为空时线段不可见）──
            if (grappleLineRenderer.sharedMaterial == null)
            {
                grappleLineRenderer.material = new Material(Shader.Find("Sprites/Default"));
                grappleLineRenderer.startColor = new Color(0f,   0.67f, 1f,   1f);  // 蓝色 #00AAFF
                grappleLineRenderer.endColor   = new Color(0.61f, 0.19f, 1f, 0.9f); // 紫色 #9B30FF
            }

            // ── 保证 2D 场景中 Z 轴为 0（面板默认 Point1.Z=1 会偏移画面）──
            grappleLineRenderer.SetPosition(0, Vector3.zero);
            grappleLineRenderer.SetPosition(1, Vector3.zero);
        }

        // Phase 2 失败 / 通关返回 Phase 1 → 恢复之前的坐标
        if (GameManager.Instance != null && GameManager.Instance.hasSavedPhase1Position)
        {
            transform.position = GameManager.Instance.savedPhase1Position;
            if (rb != null) rb.position = GameManager.Instance.savedPhase1Position;
            GameManager.Instance.hasSavedPhase1Position = false;
            Debug.Log("[PlayerController] 已恢复 Phase 1 坐标: " + transform.position);
        }
    }

    void Update()
    {
        // 转场中禁止操作
        if (GameManager.Instance != null && GameManager.Instance.isTransitioning)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        // 地面检测
        isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
        
        // 动态更新最大跳跃次数（根据等级判断是否允许二段跳）
        maxJumps = (playerStats != null && playerStats.CurrentLevel >= doubleJumpUnlockLevel) ? 2 : 1;
        
        if (isGrounded && !IsDiving)
        {
            currentJumps = 0; // 落地重置跳跃次数计数器
        }

        // 落地后结束下坠状态 —— 触发 AOE
        if (isGrounded && IsDiving)
        {
            IsDiving = false;
            PerformDiveAOE();
        }

        // ── CD UI 每帧刷新 ──
        UpdateCooldownUI(dashCdMask, dashCdText, lastDashTime, dashCooldown);
        UpdateCooldownUI(hookCdMask, hookCdText, lastHookTime, hookCooldownTime);
        UpdateCooldownUI(diveCdMask, diveCdText, lastDiveTime, diveCooldown);
        UpdateCooldownUI(waterCdMask, waterCdText, lastWaterBombTime, waterBombCooldown);

        // 钩锁移动中处理
        if (isGrappling)
        {
            HandleGrappleMovement();
            return;
        }

        // 冲刺中禁止其他操作
        if (isDashing) return;

        HandleMovement();
        HandleJump();
        HandleDash();
        HandleAttack();
        HandleWaterBomb();
        HandleGrappleInput();
        HandleDiveInput();
    }

    // ──────────────────── CD UI 通用驱动 ────────────────────
    /// <summary>
    /// 根据上次技能时间和 CD 总时长，刷新灰色遮罩和倒计时文字。
    /// </summary>
    void UpdateCooldownUI(Image mask, TextMeshProUGUI text, float lastUseTime, float cooldown)
    {
        float elapsed  = Time.time - lastUseTime;
        float remaining = cooldown - elapsed;

        if (remaining > 0f)
        {
            // 处于 CD 中
            float fillVal = remaining / cooldown;   // 1 → 0
            if (mask != null)
            {
                mask.enabled     = true;
                mask.fillAmount  = fillVal;
            }
            if (text != null)
            {
                text.enabled = true;
                text.text    = remaining.ToString("F1");
            }
        }
        else
        {
            // CD 结束，隐藏
            if (mask != null) { mask.enabled = false; mask.fillAmount = 0f; }
            if (text != null) { text.enabled = false; text.text = ""; }
        }
    }

    // ══════════════════ 移动与跳跃 ══════════════════

    void HandleMovement()
    {
        float xInput = Input.GetAxisRaw("Horizontal");
        float yVelocity = IsDiving ? -diveSpeed : rb.linearVelocity.y;
        float xVelocity = IsDiving ? rb.linearVelocity.x * 0.95f : xInput * moveSpeed;
        rb.linearVelocity = new Vector2(xVelocity, yVelocity);

        if (!IsDiving)
        {
            if (xInput > 0 && !isFacingRight) Flip();
            else if (xInput < 0 && isFacingRight) Flip();
        }
    }

    void HandleJump()
    {
        // 只要当前跳跃次数未达上限，即可执行跳跃（支持二段跳逻辑）
        if (Input.GetButtonDown("Jump") && currentJumps < maxJumps)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            currentJumps++;
        }
    }

    // ══════════════════ 下坠攻击 ══════════════════

    void HandleDiveInput()
    {
        // 在空中单击 S 键即可触发下坠，CD 检查
        if (!isGrounded && Input.GetKeyDown(KeyCode.S) && Time.time > lastDiveTime + diveCooldown)
        {
            ActivateDive();
        }
    }

    void ActivateDive()
    {
        IsDiving      = true;
        lastDiveTime  = Time.time;   // 记录释放时间
        // 赋予向下的极高初速度
        rb.linearVelocity = new Vector2(rb.linearVelocity.x * 0.3f, -diveSpeed);
    }

    void PerformDiveAOE()
    {
        float dmg = playerStats != null ? playerStats.GetFinalDamage(diveDamageMultiplier) : 15f;
        Collider2D[] enemies = Physics2D.OverlapCircleAll(transform.position, diveAOERadius, enemyLayer);
        foreach (var col in enemies)
        {
            EnemyAI enemy = col.GetComponent<EnemyAI>();
            if (enemy != null) { enemy.TakeDamage(dmg); continue; }
            DarkKnightAI dk = col.GetComponent<DarkKnightAI>();
            if (dk != null) dk.TakeDamage(dmg);
        }
        rb.linearVelocity = new Vector2(0, jumpForce * 0.5f); // 落地微量反弹，增加手感
        // CD 已在 ActivateDive() 记录，无需协程
    }

    // ────────────────── 虚空钩锁逻辑 ──────────────────

    void HandleGrappleInput()
    {
        if ((Input.GetKeyDown(KeyCode.T) || Input.GetMouseButtonDown(1))
            && Time.time > lastHookTime + hookCooldownTime)
        {
            LaunchGrapple();
        }
    }

    void LaunchGrapple()
    {
        lastHookTime = Time.time;  // 记录钩锁释放时间

        Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mouseWorld.z = 0;
        // 优先使用手部节点作为钩锁起点，未指定则回退到角色中心
        Vector2 origin = handPoint != null ? (Vector2)handPoint.position : (Vector2)transform.position;
        Vector2 dir = ((Vector2)mouseWorld - origin).normalized;
        
        // 根据鼠标距离截断最大范围
        float dist = Mathf.Min(Vector2.Distance(origin, mouseWorld), grappleMaxDistance);
        
        // 虚空钩锁，不依赖Layer碰撞，直接设定目标位移点
        grappleTarget = origin + dir * dist;
        
        isGrappling = true;
        rb.gravityScale = 0; // 钩锁期间无重力

        if (grappleLineRenderer != null)
        {
            grappleLineRenderer.enabled = true;
            grappleLineRenderer.SetPosition(0, handPoint != null ? handPoint.position : transform.position);
            grappleLineRenderer.SetPosition(1, grappleTarget);
        }
    }

    void HandleGrappleMovement()
    {
        // 优化3-E：玩家一旦按下任意移动方向键，立刻取消钩锁状态
        if (Input.GetAxisRaw("Horizontal") != 0 || Input.GetAxisRaw("Vertical") != 0)
        {
            EndGrapple();
            return;
        }

        Vector2 pos = transform.position;
        Vector2 direction = (grappleTarget - pos).normalized;
        rb.linearVelocity = direction * grappleSpeed;

        if (grappleLineRenderer != null)
            // 每帧更新线段起点（跟随手部移动）
            grappleLineRenderer.SetPosition(0, handPoint != null ? handPoint.position : transform.position);

        // 优化3-C：到达虚拟目标点，执行结束逻辑
        if (Vector2.Distance(pos, grappleTarget) < grappleArriveThreshold)
        {
            CheckPhase2Transition(grappleTarget); // 为阶段二纸片转场预留的逻辑
            EndGrapple();
        }
    }

    // 优化3-C：在钩锁滑行过程中，如果碰到实体墙壁等障碍物，强制结束并掉落
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (isGrappling)
        {
            EndGrapple();
        }
    }

    void EndGrapple()
    {
        isGrappling = false;
        rb.gravityScale = defaultGravity;
        rb.linearVelocity = Vector2.zero; // 结束后立刻开始受重力下坠
        if (grappleLineRenderer != null) grappleLineRenderer.enabled = false;
    }

    // 优化4：阶段二转场预留占位
    void CheckPhase2Transition(Vector2 arrivePoint)
    {
        // TODO: 第二阶段纸片转场逻辑
        // 此处应检测到达点附近是否有特殊的墙壁标签，触发转场动画
    }

    // ────────────────── 冲刺与攻击 ──────────────────

    void HandleDash()
    {
        if (Input.GetKeyDown(KeyCode.LeftShift) && Time.time > lastDashTime + dashCooldown)
            StartCoroutine(DashCoroutine());
    }

    IEnumerator DashCoroutine()
    {
        lastDashTime = Time.time;   // 记录冲刺释放时间
        isDashing    = true;
        float originalGrav = rb.gravityScale;
        rb.gravityScale = 0;
        float dir = isFacingRight ? 1f : -1f;
        rb.linearVelocity = new Vector2(dir * dashSpeed, 0);

        yield return new WaitForSeconds(dashDuration);

        rb.gravityScale = originalGrav;
        rb.linearVelocity = Vector2.zero;
        isDashing = false;
        // CD 已在协程开始时记录，等待结束由 UpdateCooldownUI 自动判断
    }

    public void InstantResetDash()
    {
        lastDashTime = -999f;   // 立即重置 CD
        StopCoroutine(nameof(DashCoroutine));
        isDashing = false;
        rb.gravityScale = defaultGravity;
    }

    void HandleAttack()
    {
        if (Input.GetMouseButtonDown(0))
        {
            // 获取当前武器状态
            GameManager.WeaponType currentWeapon = GameManager.Instance != null 
                ? GameManager.Instance.CurrentWeapon 
                : GameManager.WeaponType.Melee;

            if (currentWeapon == GameManager.WeaponType.Bow)
            {
                if (Time.time >= lastBowTime + bowCooldown)
                {
                    lastBowTime = Time.time;
                    HandleSwordFire();
                }
            }
            else if (currentWeapon == GameManager.WeaponType.Melee)
            {
                if (Time.time >= lastAttackTime + attackCooldown)
                {
                    lastAttackTime = Time.time;
                    PerformMeleeAttack();
                }
            }
            // 如果是 WaterBomb 模式，左键不触发攻击（按需求使用E键释放水魔爆）
        }
    }

    void PerformMeleeAttack()
    {
        float dmg = playerStats != null ? playerStats.GetFinalDamage(1f) : 10f;
        Collider2D[] enemies = Physics2D.OverlapCircleAll(attackPoint != null ? attackPoint.position : transform.position, attackRange, enemyLayer);
        foreach (var col in enemies)
        {
            EnemyAI enemy = col.GetComponent<EnemyAI>();
            if (enemy != null) { enemy.TakeDamage(dmg); continue; }
            DarkKnightAI dk = col.GetComponent<DarkKnightAI>();
            if (dk != null) dk.TakeDamage(dmg);
        }
    }

    // ────────────────── 弓箭与水魔爆 ──────────────────

    void HandleSwordFire()
    {
        Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mousePos.z = 0f;
        Vector2 fireDir = (mousePos - transform.position).normalized;

        GameObject swordObj;
        if (swordPrefab != null)
        {
            swordObj = Instantiate(swordPrefab, transform.position, Quaternion.identity);
        }
        else
        {
            // 动态生成一个蓝色的长条形Quad来表示剑气
            swordObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
            swordObj.name = "SwordProjectile_Dynamic";
            swordObj.transform.position = transform.position;
            swordObj.transform.localScale = new Vector3(1.5f, 0.15f, 1f); // 极窄长矩形
            
            Renderer r = swordObj.GetComponent<Renderer>();
            if (r != null)
            {
                r.material.color = Color.cyan;
                r.material.shader = Shader.Find("Sprites/Default"); // 使用2D无光照Shader
            }
            
            // 移除默认3D碰撞体（必须立刻销毁，否则可能与后续的2D碰撞体冲突导致 AddComponent 返回空）
            Collider oldCollider = swordObj.GetComponent<Collider>();
            if (oldCollider != null) DestroyImmediate(oldCollider);

            BoxCollider2D bc = swordObj.AddComponent<BoxCollider2D>();
            if (bc != null) bc.isTrigger = true;

            Rigidbody2D rbProj = swordObj.AddComponent<Rigidbody2D>();
            if (rbProj != null) rbProj.isKinematic = true;
        }

        // 计算旋转角度，使其朝向发射方向
        float angle = Mathf.Atan2(fireDir.y, fireDir.x) * Mathf.Rad2Deg - 90f;
        swordObj.transform.rotation = Quaternion.Euler(0, 0, angle);

        // 绑定逻辑脚本并初始化
        SwordProjectile proj = swordObj.GetComponent<SwordProjectile>();
        if (proj == null) proj = swordObj.AddComponent<SwordProjectile>();
        
        float dmg = playerStats != null ? playerStats.GetFinalDamage(1f) : 10f;
        proj.Initialize(fireDir, bowProjectileSpeed, bowMaxDistance, dmg, enemyLayer, groundLayer);
    }

    void HandleWaterBomb()
    {
        // 获取当前武器状态
        GameManager.WeaponType currentWeapon = GameManager.Instance != null 
            ? GameManager.Instance.CurrentWeapon 
            : GameManager.WeaponType.Melee;

        // 只有切换到水魔爆武器时，才能按 E 键释放
        if (currentWeapon == GameManager.WeaponType.WaterBomb)
        {
            if (Input.GetKeyDown(KeyCode.E) && Time.time >= lastWaterBombTime + waterBombCooldown)
            {
                if (playerStats != null && playerStats.UseManaForWaterBomb())
                {
                    lastWaterBombTime = Time.time;
                    PerformWaterBomb();
                }
            }
        }
    }

    void PerformWaterBomb()
    {
        Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        float startX = mousePos.x;
        float endY = transform.position.y;
        float startY = endY + 5f; // 水柱起点设在比玩家高5个单位的位置
        
        Vector3 spawnPos = new Vector3(startX, (startY + endY) / 2f, 0f); // 中心位置

        GameObject waterObj;
        if (waterBombPrefab != null)
        {
            waterObj = Instantiate(waterBombPrefab, spawnPos, Quaternion.identity);
        }
        else
        {
            // 动态生成一个覆盖该区域的半透明长方形来表示水柱
            waterObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
            waterObj.name = "WaterBurst_Dynamic";
            waterObj.transform.position = spawnPos;
            waterObj.transform.localScale = new Vector3(waterBombWidth, startY - endY, 1f);
            
            Renderer r = waterObj.GetComponent<Renderer>();
            if (r != null)
            {
                r.material.color = new Color(0f, 0.5f, 1f, 0.6f); // 半透明深蓝
                r.material.shader = Shader.Find("Sprites/Default");
            }
            Destroy(waterObj.GetComponent<Collider>()); // 移除3D碰撞
        }

        // 伤害判定（直接使用 Physics2D.OverlapBoxAll，完全无视地形）
        float dmg = playerStats != null ? playerStats.GetFinalDamage(3f) : 30f; // 假设造成3倍巨额伤害
        Vector2 size = new Vector2(waterBombWidth, startY - endY);
        Collider2D[] enemies = Physics2D.OverlapBoxAll(spawnPos, size, 0f, enemyLayer);
        foreach (var col in enemies)
        {
            EnemyAI enemy = col.GetComponent<EnemyAI>();
            if (enemy != null) { enemy.TakeDamage(dmg); continue; }
            DarkKnightAI dk = col.GetComponent<DarkKnightAI>();
            if (dk != null) dk.TakeDamage(dmg);
        }

        // 特效显示极短时间后自动销毁
        Destroy(waterObj, 0.5f);
    }

    // ────────────────── 工具方法 ──────────────────

    void Flip()
    {
        isFacingRight = !isFacingRight;
        Vector3 scaler = transform.localScale;
        scaler.x *= -1;
        transform.localScale = scaler;
    }
}