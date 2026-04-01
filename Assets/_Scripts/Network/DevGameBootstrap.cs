using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 게임 씬 직접 실행 전용 부트스트랩 (에디터 전용)
/// 타이틀/로비를 거치지 않고 게임 씬에서 바로 테스트할 때 사용
///
/// [사용법]
/// 1. 게임 씬의 빈 오브젝트에 이 컴포넌트 추가
/// 2. runnerPrefab 슬롯에 NetworkRunner 프리팹 연결
/// 3. 에디터에서 게임 씬 바로 실행
/// </summary>
public class DevGameBootstrap : MonoBehaviour
{
#if UNITY_EDITOR
    [Header("연결 설정")]
    [SerializeField] private NetworkRunner runnerPrefab;

    [Tooltip("에디터끼리 같은 이름으로 접속")]
    [SerializeField] private string sessionName = "DevGame";

    [Header("테스트용 플레이어 정보")]
    [SerializeField] private string testNickname = "TestPlayer";

    private async void Start()
    {
        // 타이틀을 거치지 않았으면 NetworkManager가 없으므로 직접 생성
        if (NetworkManager.Instance == null)
        {
            var nmGo = new GameObject("NetworkManager [Dev]");
            // NetworkManager는 DontDestroyOnLoad이므로 씬 전환 후에도 유지
            var nm = nmGo.AddComponent<NetworkManager>();
            nmGo.AddComponent<NetworkRunnerCallbacks>();
            nm.SetDevData(testNickname, sessionName);
        }

        var runner = Instantiate(runnerPrefab);
        NetworkManager.Instance.SetRunner(runner);
        runner.AddCallbacks(FindFirstObjectByType<NetworkRunnerCallbacks>());

        var result = await runner.StartGame(new StartGameArgs
        {
            GameMode    = GameMode.AutoHostOrClient,
            SessionName = sessionName,
            Scene       = SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex),
            SceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>()
        });

        if (!result.Ok)
            Debug.LogError($"[DevGameBootstrap] 연결 실패: {result.ShutdownReason}");
        else
            Debug.Log($"[DevGameBootstrap] 연결 성공 — {runner.GameMode}");
    }
#endif
}
