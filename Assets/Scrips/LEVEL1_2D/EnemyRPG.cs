using UnityEngine;

public class EnemyRPG : MonoBehaviour
{
    public int hp = 100;
    
    // 基础经验值（GameManager会根据击杀数计算加成）
    public float baseExpAmount = 800f; 

    [Header("=== 下坠伤害设置 ===")]
    public int diveDamage = 100;        // 下坠伤害
    public float bounceForce = 12f;     // 反弹力度

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
            // 情况B：被下坠砸死 (高伤害)
            else if (player.IsDiving)
            {
                TakeDamage(diveDamage, player);
                
                // 反弹效果
                Rigidbody2D playerRb = player.GetComponent<Rigidbody2D>();
                if (playerRb != null)
                {
                    playerRb.SetVelocity(new Vector2(playerRb.GetVelocity().x * 0.3f, bounceForce));
                }
            }
        }
    }

    public void TakeDamage(int damage, PlayerController attacker = null)
    {
        hp -= damage;
        
        // 受击变色
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null) sr.color = Color.red;
        Invoke("ResetColor", 0.1f);

        if (hp <= 0) 
        {
            Die(attacker, attacker != null && attacker.IsDashing);
        }
    }

    void ResetColor()
    {
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null) sr.color = Color.white;
    }

    void Die(PlayerController killer, bool isDashKill)
    {
        // 1. 通知GameManager处理经验和奖励
        // 使用新的OnEnemyKilled方法（自动处理首杀奖励和连杀加成）
        GameManager.Instance.OnEnemyKilled(baseExpAmount);

        // 2. 如果是被突刺死的，重置主角技能CD
        if (isDashKill && killer != null)
        {
            killer.InstantResetDash();
        }

        // 3. TODO: 这里可以添加死亡特效
        // if (deathEffectPrefab != null) Instantiate(deathEffectPrefab, transform.position, Quaternion.identity);

        // 4. 销毁自身
        Destroy(gameObject);
    }
}