using UnityEngine;
using System.Collections;  //IEnumerator

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
        //砍中怪的瞬间画面卡顿一下，简直是太爽了！
        StartCoroutine(HitStop());
        // 2. 销毁自身
        Destroy(gameObject);
    }
    IEnumerator HitStop()
    {
        // 瞬间变慢（模拟卡顿感）
        Time.timeScale = 0.1f;

        // 等待真实时间的 0.05 秒 (注意要用 WaitForSecondsRealtime，否则会受 timeScale 影响变得超级慢)
        yield return new WaitForSecondsRealtime(0.05f);

        // 恢复正常速度
        Time.timeScale = 1f;
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
