using Fusion;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class NetworkDebugStarter : MonoBehaviour
{
    [Header("연결 설정")]
    [SerializeField] private NetworkRunner runnerPrefab;

    [Tooltip("MPM 사용 시 인스턴스끼리 같은 이름으로 접속")]
    [SerializeField] private string sessionName;

    [Header("테스트용 플레이어 정보")]
    [SerializeField] private string testNickname = "TestPlayer";

    private async void Start()
    {
        // 정상 경로(Lobby -> Game)로 접속된 상태면 스킵
        if (FusionCallbackHandler.Current != null)
        {
            Debug.Log("[NetworkDebugStarter] 이미 네트워크 연결됨. 스킵.");
            return;
        }

        // 매번 새 세션 이름 생성 → 이전 세션 캐시 충돌 방지
        string sessionName = "Dev_" + System.Guid.NewGuid().ToString("N")[..8];

        // GameLauncher 동적 생성
        var go = new GameObject("GameLauncher [Dev]");
        go.AddComponent<GameLauncher>();

        var callbackHandler = new FusionCallbackHandler();

        var runner = Instantiate(runnerPrefab);
        runner.name = "NetworkRunner [Dev]";
        runner.AddCallbacks(callbackHandler);
        DontDestroyOnLoad(runner.gameObject);

        // InputHandler를 StartGame 전에 초기화
        var inputHandler = runner.GetComponent<InputHandler>();
        if (inputHandler != null)
            inputHandler.Initialize(callbackHandler);
        else
            Debug.LogWarning("[NetworkDebugStarter] InputHandler를 Runner에서 찾을 수 없습니다. " +
                             "NetworkRunner 프리팹에 InputHandler가 붙어 있는지 확인하세요.");

        GameLauncher.Instance.SetDevData(testNickname, sessionName);
        GameLauncher.Instance.SetDevRunner(runner, callbackHandler);

        var result = await runner.StartGame(new StartGameArgs
        {
            GameMode = GameMode.AutoHostOrClient,           // MPM: 첫 인스턴스: Host, 나머지 Client
            SessionName = sessionName,
            Scene = SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex),
            SceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>()
        });

        if (!result.Ok)
            Debug.LogError($"[NetworkDebugStarter] 연결 실패: {result.ShutdownReason}");

        Debug.Log($"[NetworkDebugStarter] 연결 성공 — {runner.GameMode}");
    }
}
