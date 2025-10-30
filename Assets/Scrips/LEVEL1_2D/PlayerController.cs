using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerController : MonoBehaviour {
    public float speed = 5f;
    public float jumpPower = 10f;
    public float dashPower = 20f;
    public LayerMask groundLayer;
    private Rigidbody2D rb;
    private bool isGrounded;
    private int kills = 0;

    void Start() { rb = GetComponent<Rigidbody2D>(); }

    void Update() {
        float move = Input.GetAxis("Horizontal");
        rb.velocity = new Vector2(move * speed, rb.velocity.y);

        isGrounded = Physics2D.Raycast(transform.position, Vector2.down, 0.1f, groundLayer);
        if (isGrounded && Input.GetKeyDown(KeyCode.Space)) {
            rb.AddForce(Vector2.up * jumpPower, ForceMode2D.Impulse);
        }

        if (Input.GetKeyDown(KeyCode.C)) { // 突刺
            rb.AddForce(transform.right * dashPower, ForceMode2D.Impulse);
            // 检测碰撞敌人（在OnTriggerEnter2D中处理）
        }

        if (kills >= 3) { SceneManager.LoadScene("TowerDefenseScene"); } // 切换到塔防
    }

    void OnTriggerEnter2D(Collider2D other) {
        if (other.tag == "Enemy") {
            Destroy(other.gameObject);
            kills++;
        }
    }
}