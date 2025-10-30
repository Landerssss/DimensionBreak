// Tower.cs
using UnityEngine;

public class Tower : MonoBehaviour {
    public float range = 5f;
    public float fireRate = 1f;
    public GameObject projectilePrefab;
    private float nextFireTime;

    void Update() {
        if (Time.time > nextFireTime) {
            GameObject enemy = FindNearestEnemy();
            if (enemy) {
                Shoot(enemy);
                nextFireTime = Time.time + 1f / fireRate;
            }
        }
    }

    GameObject FindNearestEnemy() {
        // 用Physics2D.OverlapCircleAll找最近敌人
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, range);
        GameObject nearest = null;
        float minDist = float.MaxValue;
        foreach (var hit in hits) {
            if (hit.tag == "Enemy") {
                float dist = Vector2.Distance(transform.position, hit.transform.position);
                if (dist < minDist) { minDist = dist; nearest = hit.gameObject; }
            }
        }
        return nearest;
    }

    void Shoot(GameObject target) {
        GameObject proj = Instantiate(projectilePrefab, transform.position, Quaternion.identity);
        proj.GetComponent<Projectile>().SetTarget(target); // Projectile脚本处理移动/伤害
    }
}

