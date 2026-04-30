using UnityEngine;

/// <summary>
/// 弓箭（剑气）抛射物脚本
/// 控制剑气直线飞行、伤害判定与销毁逻辑
/// </summary>
public class SwordProjectile : MonoBehaviour
{
    private Vector2 direction;
    private float speed;
    private float maxDistance;
    private float damage;
    private LayerMask enemyLayer;
    private LayerMask groundLayer;
    
    private Vector2 startPos;

    /// <summary>
    /// 初始化抛射物参数
    /// </summary>
    public void Initialize(Vector2 dir, float spd, float maxDist, float dmg, LayerMask enemyMask, LayerMask groundMask)
    {
        direction = dir.normalized;
        speed = spd;
        maxDistance = maxDist;
        damage = dmg;
        enemyLayer = enemyMask;
        groundLayer = groundMask;
        
        startPos = transform.position;
    }

    void Update()
    {
        // 直线飞行
        transform.Translate(direction * speed * Time.deltaTime, Space.World);

        // 超过极限距离后销毁
        if (Vector2.Distance(startPos, transform.position) > maxDistance)
        {
            Destroy(gameObject);
        }
    }

    void OnTriggerEnter2D(Collider2D col)
    {
        // 如果碰到敌人
        if (((1 << col.gameObject.layer) & enemyLayer) != 0)
        {
            EnemyAI enemy = col.GetComponent<EnemyAI>();
            if (enemy != null)
            {
                enemy.TakeDamage(damage);
            }
            Destroy(gameObject); // 造成伤害后立即销毁
        }
        // 如果碰到地面或障碍物
        else if (((1 << col.gameObject.layer) & groundLayer) != 0)
        {
            Destroy(gameObject); // 立即销毁
        }
    }
}
