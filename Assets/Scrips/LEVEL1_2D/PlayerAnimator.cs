using UnityEngine;

public class PlayerAnimator : MonoBehaviour
{
    private Animator animator;
    private PlayerController controller;
    private Rigidbody2D rb;
    private PlayerStats stats;

    // Animator 参数名常量，避免拼写错误
    private static readonly int IsRunning  = Animator.StringToHash("isRunning");
    private static readonly int IsGrounded = Animator.StringToHash("isGrounded");
    private static readonly int JumpTrig   = Animator.StringToHash("Jump");
    private static readonly int AttackTrig = Animator.StringToHash("Attack");
    private static readonly int HurtTrig   = Animator.StringToHash("Hurt");
    private static readonly int DieTrig    = Animator.StringToHash("Die");
    private static readonly int LookUpTrig = Animator.StringToHash("LookUp");

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
        animator.SetBool(IsRunning, xSpeed > 0.1f && !controller.IsDashing);

        // ── 地面状态 ──
        bool grounded = Physics2D.OverlapCircle(
            controller.groundCheck.position,   // 需要把 groundCheck 改为 public 或 internal
            0.2f,
            controller.groundLayer             // 同上
        );
        animator.SetBool(IsGrounded, grounded);

        // ── 跳跃 Trigger ──
        if (Input.GetButtonDown("Jump"))
            animator.SetTrigger(JumpTrig);

        // ── 攻击 Trigger ──
        if (Input.GetMouseButtonDown(0))
            animator.SetTrigger(AttackTrig);

        // ── LookUp（按 W 朝上） ──
        if (Input.GetKeyDown(KeyCode.W))
            animator.SetTrigger(LookUpTrig);
    }

    // 供 PlayerStats / 受伤系统外部调用
    public void PlayHurt() => animator.SetTrigger(HurtTrig);
    public void PlayDie()  => animator.SetTrigger(DieTrig);
}