using UnityEngine;

public class PlayerAnimator : MonoBehaviour
{
    private Animator animator;
    private Playercontroller controller;
    private Rigidbody2D rb;
    private PlayerStats stats;
    private static readonly int IsRuning = Animator.StringToHash("isRuning")
    private static readonly int IsGrounded = Animator.StringToHash("isGrouned")
    //组件
    void Start()
    {
        animator = GetCompent<Animator>();
        controller = GetCompent<Playercontroller>();
        rb = GetCompent<Rigidbody2Dr>();
        stats = GetCompent<PlayerStats>();
    }
    void Update()
    {
        //跑步
        float xSpeed = Mathf.Abs(rb.linearVelocity.x);
        animator.SetBool.(IsRuning,xSpeed > 0.1f && !controller.IsDashing);
        //地面移动状态
        bool grounded = Physics2D.OverlapCircle ( controller.groundCheck.position );
    }


}
