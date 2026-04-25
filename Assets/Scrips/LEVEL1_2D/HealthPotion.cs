using UnityEngine;

public class HealthPotion : MonoBehaviour
{
    [SerializeField] private float healAmount = 20f;
    [Header("掉落时的受力范围")]
    [SerializeField] private float jumpForceMin = 4f;
    [SerializeField] private float jumpForceMax = 7f;
    [SerializeField] private float horizontalForceMin = -2f;
    [SerializeField] private float horizontalForceMax = 2f;

    private void Start()
    {
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            // 给自身的 Rigidbody2D 添加一个向上且带有随机水平分量的力，模拟抛物线掉落
            float upForce = Random.Range(jumpForceMin, jumpForceMax);
            float hForce = Random.Range(horizontalForceMin, horizontalForceMax);
            rb.AddForce(new Vector2(hForce, upForce), ForceMode2D.Impulse);
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            PlayerStats stats = collision.GetComponent<PlayerStats>();
            if (stats != null)
            {
                stats.RestoreHealth(healAmount);
            }
            // 被玩家碰到后销毁自身
            Destroy(gameObject);
        }
    }
}
