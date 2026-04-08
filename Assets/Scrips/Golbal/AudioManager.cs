using UnityEngine;

public class AudioManager : MonoBehaviour
{
    private static AudioManager instance;

    void Awake()
    {
        // 检查是否已有实例存在
        if (instance == null)
        {
            instance = this;
            // 关键：切换场景时不销毁此物体
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            // 如果场景中重复出现了 AudioManager，直接销毁掉
            Destroy(gameObject);
        }
    }
}