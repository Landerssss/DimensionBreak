// WaveSpawner.cs (附加到空对象)
using UnityEngine;
using UnityEngine.SceneManagement;

public class WaveSpawner : MonoBehaviour {
    public GameObject enemyPrefab;
    public Transform[] waypoints; // 路径点
    public int waveCount = 5;
    private int enemiesAlive = 0;
    private int waveIndex = 0;

    void Update() {
        if (enemiesAlive == 0 && waveIndex < waveCount) {
            SpawnWave();
            waveIndex++;
        }
        if (waveIndex >= waveCount) { SceneManager.LoadScene("BossScene"); } // 切3D
    }

    void SpawnWave() {
        for (int i = 0; i < waveIndex + 1; i++) {
            GameObject enemy = Instantiate(enemyPrefab, waypoints[0].position, Quaternion.identity);
            enemy.GetComponent<EnemyPathing>().waypoints = waypoints; // EnemyPathing脚本跟随路径
            enemiesAlive++;
        }
    }

    public void EnemyDied() { enemiesAlive--; }
}