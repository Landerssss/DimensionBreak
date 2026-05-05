using UnityEngine;

public class PlayerAnimator : MonoBehaviour
{
    private Animator animator;
    private PlayerController controller;
    private Rigidbody2D rb;
    private PlayerStats stats;

    // Animator 参数名常量，避免拼写错误
    private static readonly int isRun  = Animator.StringToHash("isRun");
    private static readonly int IsGrounded = Animator.StringToHash("isGrounded");
    private static readonly int isJumpBool   = Animator.StringToHash("isJump");
    private static readonly int aTrig = Animator.StringToHash("attack");
    private static readonly int hurtTrig   = Animator.StringToHash("hurt");
    private static readonly int dieTrig    = Animator.StringToHash("die");
    private static readonly int LookUpTrig = Animator.StringToHash("isLookUp");

    void Start()
    {
        animator   = GetComponent<Animator>();
        controller = GetComponent<PlayerController>();
        rb         = GetComponent<Rigidbody2D>();
        stats      = GetComponent<PlayerStats>();
    }

    void Update()
    {
        // ── 跑步 ──
        float xSpeed = Mathf.Abs(rb.linearVelocity.x);
        animator.SetBool(isRun, xSpeed > 0.1f && !controller.IsDashing);

        // ── 地面状态 ──
        bool grounded = Physics2D.OverlapCircle(
            controller.groundCheck.position,   // 需要把 groundCheck 改为 public 或 internal
            0.2f,
            controller.groundLayer             // 同上
        );
        animator.SetBool(IsGrounded, grounded);

        // ── 跳跃状态（Bool 持续同步，落地即复位）──
        // isJump 在 AnimatorController 中是 Bool 类型（Jump→Idle 条件 isJump=false），
        // 必须用 SetBool 驱动，不能用 SetTrigger，否则落地后无法退出 Jump 状态。
        animator.SetBool(isJumpBool, !grounded);

        // ── 攻击 Trigger ──
        if (Input.GetMouseButtonDown(0))
            animator.SetTrigger(aTrig);

        // ── LookUp（按 W 朝上） ──
        if (Input.GetKeyDown(KeyCode.W))
            animator.SetTrigger(LookUpTrig);
    }

    // 供 PlayerStats / 受伤系统外部调用
    public void Playhurt() => animator.SetTrigger(hurtTrig);
    public void Playdie()  => animator.SetTrigger(dieTrig);
}