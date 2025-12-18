using UnityEngine;

public class SimpleEnemy : MonoBehaviour
{
    public int health = 100;
    public GameObject deathEffectPrefab; // 死亡时的爆炸特效

    public void TakeDamage(int damage)
    {
        health -= damage;
        
        // 简单的受击反馈：变色一下
        StartCoroutine(FlashRed());

        if (health <= 0)
        {
            Die();
        }
    }

    void Die()
    {
        // 1. 生成特效
        if (deathEffectPrefab != null)
        {
            Instantiate(deathEffectPrefab, transform.position, Quaternion.identity);
        }

        // 2. 销毁自身
        Destroy(gameObject);
    }

    System.Collections.IEnumerator FlashRed()
    {
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if(sr != null) 
        {
            sr.color = Color.red;
            yield return new WaitForSeconds(0.1f);
            sr.color = Color.white;
        }
    }
}
