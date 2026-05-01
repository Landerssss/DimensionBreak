using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TealFalconEnemySeries{

    public class BeamShot : MonoBehaviour
    {

        public Rigidbody2D _rigidBody;
        public float power;
        public Vector2 direction;
        public float duration;

        void Start()
        {
            StartCoroutine(Timer());
        }

        IEnumerator Timer()
        {
            yield return new WaitForSeconds(0.05f);

                if(transform.localScale.x < 0){
                    _rigidBody.AddForce(direction*power*-1, ForceMode2D.Impulse);
                }else{
                    _rigidBody.AddForce(direction*power, ForceMode2D.Impulse);
                }

            yield return new WaitForSeconds(duration);
            Destroy(gameObject);
        }

        // 强力碰撞检测：确保在任何层级下只要碰到玩家就能造成伤害
        private void OnTriggerEnter2D(Collider2D collision)
        {
            // 扫描碰撞体及其所有父对象中的 PlayerStats 脚本
            PlayerStats stats = collision.GetComponentInParent<PlayerStats>();
            
            if (stats != null)
            {
                stats.TakeDamage(35f); 
                Debug.Log("能量球击中玩家！造成伤害。");
                Destroy(gameObject); 
            }
        }
    }
}