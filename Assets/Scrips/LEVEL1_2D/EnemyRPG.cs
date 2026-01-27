using UnityEngine;

public class EnemyRPG : MonoBehaviour
{
    public int hp = 100;
    
    // 设为 800，因为每级100经验，打死一只升8级
    public float expAmount = 800f; 

    // 触发器检测伤害（突刺或下坠）
    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            PlayerController player = other.GetComponent<PlayerController>();
            if (player == null) return;

            // 情况A：被突刺撞死 (秒杀)
            if (player.IsDashing)
            {
                Die(player, true); // true 表示是被突刺死的，要重置CD
            }
            // 情况B：被下坠砸死 (伤害)
            else if (player.IsDiving)
            {
                TakeDamage(100); // 下坠伤害
                // 简单的反弹效果
                player.GetComponent<Rigidbody2D>().SetVelocity(new Vector2(0, 10f));
            }
        }
    }

    public void TakeDamage(int damage)
    {
        hp -= damage;
        GetComponent<SpriteRenderer>().color = Color.red; // 受击变色
        Invoke("ResetColor", 0.1f);

        if (hp <= 0) Die(null, false);
    }

    void ResetColor() => GetComponent<SpriteRenderer>().color = Color.white;

    void Die(PlayerController killer, bool isDashKill)
    {
        // 1. 给经验
        GameManager.Instance.AddExp(expAmount);

        // 2. 如果是被突刺死的，重置主角技能
        if (isDashKill && killer != null)
        {
            killer.InstantResetDash();
        }

        // 3. 销毁
        Destroy(gameObject);
    }
}