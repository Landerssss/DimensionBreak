using UnityEngine;

public class BgmChoose : MonoBehaviour
{
    [SerializeField] private AudioClip sceneBGM; // 每个场景配各自的音乐

    void Start()
    {
        if (AudioManager.Instance != null && sceneBGM != null)
        {
            AudioManager.Instance.PlayBGM(sceneBGM);
        }
    }
}