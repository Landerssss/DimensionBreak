// CameraSwitcher.cs (附加到相机)
using UnityEngine;

public class CameraSwitcher : MonoBehaviour {
    public void SwitchTo3D() {
        GetComponent<Camera>().orthographic = false;
        // 调整位置/旋转到3D视角
    }
}

// BossController.cs (附加到BOSS)
using UnityEngine;

public class BossController : MonoBehaviour {
    public int health = 50;
    public GameObject attackPrefab; // 攻击物体
    private float attackTimer;

    void Update() {
        attackTimer += Time.deltaTime;
        if (attackTimer > 2f) {
            Attack();
            attackTimer = 0;
        }
    }

    void Attack() {
        Instantiate(attackPrefab, transform.position + Vector3.forward * 5, Quaternion.identity);
        // 攻击向玩家方向
    }

    public void TakeDamage(int dmg) {
        health -= dmg;
        if (health <= 0) { // 胜负
            Debug.Log("Boss Defeated!");
            // 结束游戏或回菜单
        }
    }
}

// Player3D.cs (3D模式主角)
using UnityEngine;

public class Player3D : MonoBehaviour {
    public float speed = 5f;
    void Update() {
        float moveZ = Input.GetAxis("Vertical");
        transform.Translate(0, 0, moveZ * speed * Time.deltaTime);
        // 锁定朝北：transform.rotation = Quaternion.Euler(0, 0, 0);

        if (Input.GetKeyDown(KeyCode.Space)) { // 攻击
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit)) {
                if (hit.transform.tag == "Boss") {
                    hit.transform.GetComponent<BossController>().TakeDamage(10);
                }
            }
        }
    }
}