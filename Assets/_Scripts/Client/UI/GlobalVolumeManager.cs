using UnityEngine;

public class GlobalVolumeManager : MonoBehaviour
{
    public static GlobalVolumeManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            //이미 존재하면 새로 로드된 중복 오브젝트 제거
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
}
