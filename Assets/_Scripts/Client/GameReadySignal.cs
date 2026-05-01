using UnityEngine;

public class GameReadySignal : MonoBehaviour
{
    private void Start()
    {
        // 이 오브젝트의 Start()가 끝나면 로딩 완료로 간주
        // 원하는 초기화 로직을 여기서 다 끝낸 뒤 호출
        NotifyReady();
    }

    private void NotifyReady()
    {
        if (GameLauncher.Instance != null)
            GameLauncher.Instance.NotifyGameReady(); // 아래에서 추가
    }
}