using UnityEngine;

public class EnemyRPG : MonoBehaviour
{
    [Header("=== 怪物属性 ===")]
    public int hp = 100;
    public float expAmount = 800f; // 800经验 = 直接升8级 (按100/级算)

    public void TakeDamage(int damage)
    {
        hp -= damage;
        // 简单的受击反馈
        GetComponent<SpriteRenderer>().color = Color.red;
        Invoke("ResetColor", 0.1f);

        if (hp <= 0) Die();
    }

    void ResetColor() => GetComponent<SpriteRenderer>().color = Color.white;

    void Die()
    {
        // 1. 给主角加经验
        GameManager.Instance.AddExp(expAmount);
        
        // 2. 检查是否需要重置主角突刺CD (爽感逻辑)
        PlayerController pc = FindObjectOfType<PlayerController>();
        if (pc != null) pc.ResetDash();

        // 3. 播放特效并销毁
        // Instantiate(deathEffect, transform.position, Quaternion.identity);
        Destroy(gameObject);
    }
}