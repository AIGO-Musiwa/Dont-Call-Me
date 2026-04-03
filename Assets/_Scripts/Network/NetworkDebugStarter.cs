using Fusion;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class NetworkDebugStarter : MonoBehaviour
{
    [Header("연결 설정")]
    [SerializeField] private NetworkRunner runnerPrefab;

    [Tooltip("MPM 사용 시 인스턴스끼리 같은 이름으로 접속")]
    [SerializeField] private string sessionName = "DevGame";

    [Header("플레이어")]
    [SerializeField] private NetworkObject playerPrefab;

    [Tooltip("스폰 위치 목록")]
    [SerializeField] private Transform[] spawnPoints;

    [Header("테스트용 플레이어 정보")]
    [SerializeField] private string testNickname = "TestPlayer";

    // ── 내부 ──────────────────────────────────────────────
    private FusionCallbackHandler _callbackHandler;
    private readonly Dictionary<PlayerRef, NetworkObject> _spawnedPlayers = new();

    private async void Start()
    {
        // 정상 경로(Lobby -> Game)로 접속된 상태면 스킵
        if (FusionCallbackHandler.Current != null)
        {
            Debug.Log("[NetworkDebugStarter] 이미 네트워크 연결됨. 스킵.");
            return;
        }

        // GameLauncher 동적 생성
        var go = new GameObject("GameLauncher [Dev]");
        go.AddComponent<GameLauncher>();

        _callbackHandler = new FusionCallbackHandler();

        // 플레이어 입/퇴장 구독
        _callbackHandler.OnPlayerJoinedEvent += HandlePlayerJoined;
        _callbackHandler.OnPlayerLeftEvent += HandlePlayerLeft;

        var runner = Instantiate(runnerPrefab);
        runner.name = "NetworkRunner [Dev]";
        runner.AddCallbacks(_callbackHandler);
        DontDestroyOnLoad(runner.gameObject);

        GameLauncher.Instance.SetDevData(testNickname, sessionName);
        GameLauncher.Instance.SetDevRunner(runner, _callbackHandler);

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

        var inputHandler = runner.GetComponent<InputHandler>();
        if (inputHandler != null)
            inputHandler.Initialize(_callbackHandler);
        else
            Debug.LogWarning("[NetworkDebugStarter] InputHandler를 Runner에서 찾을 수 없습니다. " +
                             "NetworkRunner 프리팹에 InputHandler가 붙어 있는지 확인하세요.");
    }

    private void OnDestroy()
    {
        if (_callbackHandler == null) return;
        _callbackHandler.OnPlayerJoinedEvent -= HandlePlayerJoined;
        _callbackHandler.OnPlayerLeftEvent -= HandlePlayerLeft;
    }

    // ── 플레이어 스폰 ──────────────────────────────────────

    private void HandlePlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        // 서버(Host)만 스폰 권한을 가진다.
        if (!runner.IsServer || playerPrefab == null) return;

        Vector3 pos = Vector3.zero;
        Quaternion rot = Quaternion.identity;

        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            var sp = spawnPoints[_spawnedPlayers.Count % spawnPoints.Length];
            pos = sp.position;
            rot = sp.rotation;
        }

        var obj = runner.Spawn(playerPrefab, pos, rot, player);
        _spawnedPlayers[player] = obj;
        runner.SetPlayerObject(player, obj);
    }

    private void HandlePlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        if (!runner.IsServer) return;

        if (_spawnedPlayers.TryGetValue(player, out var obj))
        {
            runner.Despawn(obj);
            _spawnedPlayers.Remove(player);
        }
    }
}
