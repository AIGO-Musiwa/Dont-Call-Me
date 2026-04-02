using System.Collections.Generic;
using System.Threading.Tasks;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 이동 / 시야 테스트용 최소 Fusion 실행기.
///
/// 이 스크립트가 하는 일:
/// 1. NetworkRunner 시작
/// 2. 현재 씬을 네트워크 씬으로 등록해서 세션 시작
/// 3. 플레이어가 들어오면 playerPrefab을 스폰
/// 4. runner.SetPlayerObject()로 로컬 플레이어 오브젝트 등록
///
/// 목적:
/// - FirstPersonController + PlayerController + InputHandler 조합이
///   실제로 WASD / 마우스 시선 이동 되는지 빠르게 확인
/// - 이후 아이템 / 상호작용 / 포획 시스템 붙이기 전에
///   가장 기본이 되는 플레이어 조작만 검증
///
/// 사용 전제:
/// - 같은 GameObject에 NetworkRunner가 있어야 함
/// - 같은 GameObject(또는 자식)에 InputHandler가 있어야 함
/// - playerPrefab에는 최소 아래가 있어야 함
///   NetworkObject / CharacterController / PlayerController / FirstPersonController
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkRunner))]
public class FusionMovementTestLauncher : MonoBehaviour, INetworkRunnerCallbacks
{
    [Header("Fusion 시작 설정")]
    [SerializeField] private GameMode gameMode = GameMode.AutoHostOrClient;
    [SerializeField] private string sessionName = "MovementTestRoom";
    [SerializeField] private int maxPlayers = 4;
    [SerializeField] private bool startOnPlay = true;

    [Header("플레이어 프리팹")]
    [SerializeField] private NetworkObject playerPrefab;

    [Header("테스트용 스폰 위치")]
    [SerializeField] private Transform[] spawnPoints;

    private NetworkRunner _runner;
    private NetworkSceneManagerDefault _sceneManager;

    /// <summary>
    /// 이미 스폰한 플레이어를 추적하기 위한 딕셔너리.
    /// 플레이어 퇴장 시 정리할 때 사용.
    /// </summary>
    private readonly Dictionary<PlayerRef, NetworkObject> _spawnedPlayers = new();

    private async void Start()
    {
        if (!startOnPlay)
            return;

        await StartSession();
    }

    /// <summary>
    /// Fusion 세션 시작.
    /// 현재 활성 씬을 네트워크 씬으로 등록해서 실행한다.
    /// </summary>
    public async Task StartSession()
    {
        _runner = GetComponent<NetworkRunner>();

        if (_runner == null)
        {
            Debug.LogError("[FusionMovementTestLauncher] NetworkRunner가 없습니다.", this);
            return;
        }

        if (playerPrefab == null)
        {
            Debug.LogError("[FusionMovementTestLauncher] playerPrefab이 비어 있습니다.", this);
            return;
        }

        // 중복 시작 방지
        if (_runner.IsRunning)
        {
            Debug.LogWarning("[FusionMovementTestLauncher] 이미 Runner가 실행 중입니다.", this);
            return;
        }

        // InputProvider 역할을 할 경우 입력 수집 허용
        _runner.ProvideInput = true;

        // SceneManagerDefault가 없으면 자동 추가
        _sceneManager = GetComponent<NetworkSceneManagerDefault>();
        if (_sceneManager == null)
            _sceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>();

        Scene activeScene = SceneManager.GetActiveScene();
        int buildIndex = activeScene.buildIndex;

        if (buildIndex < 0)
        {
            Debug.LogError("[FusionMovementTestLauncher] 현재 씬이 Build Settings에 없습니다.", this);
            return;
        }

        var sceneRef = SceneRef.FromIndex(buildIndex);

        var args = new StartGameArgs
        {
            GameMode = gameMode,
            SessionName = sessionName,
            PlayerCount = maxPlayers,
            Scene = sceneRef,
            SceneManager = _sceneManager
        };

        var result = await _runner.StartGame(args);

        if (result.Ok)
        {
            Debug.Log($"[FusionMovementTestLauncher] 세션 시작 성공 / Mode={gameMode} / Session={sessionName}");
        }
        else
        {
            Debug.LogError($"[FusionMovementTestLauncher] 세션 시작 실패: {result.ShutdownReason}");
        }
    }

    /// <summary>
    /// 플레이어 입장 시 서버(Host)가 플레이어 프리팹을 스폰한다.
    /// Host/Server 권한에서만 Spawn 해야 중복 생성이 안 난다.
    /// </summary>
    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        if (!runner.IsServer)
            return;

        Vector3 spawnPos = Vector3.zero;
        Quaternion spawnRot = Quaternion.identity;

        Transform spawn = GetSpawnPoint(_spawnedPlayers.Count);
        if (spawn != null)
        {
            spawnPos = spawn.position;
            spawnRot = spawn.rotation;
        }

        NetworkObject playerObj = runner.Spawn(
            playerPrefab,
            spawnPos,
            spawnRot,
            player
        );

        _spawnedPlayers[player] = playerObj;
        runner.SetPlayerObject(player, playerObj);

        Debug.Log($"[FusionMovementTestLauncher] Player Spawn 완료: {player}");
    }

    /// <summary>
    /// 플레이어 퇴장 시 해당 오브젝트 정리.
    /// </summary>
    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        if (!runner.IsServer)
            return;

        if (_spawnedPlayers.TryGetValue(player, out var playerObj))
        {
            runner.Despawn(playerObj);
            _spawnedPlayers.Remove(player);
        }
    }

    /// <summary>
    /// 인덱스 기준으로 테스트용 스폰 포인트 반환.
    /// spawnPoints가 비어 있으면 월드 원점 사용.
    /// </summary>
    private Transform GetSpawnPoint(int index)
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
            return null;

        return spawnPoints[index % spawnPoints.Length];
    }

    // ------------------------------------------------------------
    // 아래는 INetworkRunnerCallbacks 필수 구현용 빈 메서드들
    // ------------------------------------------------------------
    public void OnInput(NetworkRunner runner, NetworkInput input) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
    public void OnConnectedToServer(NetworkRunner runner) { }
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnSceneLoadDone(NetworkRunner runner) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, System.ArraySegment<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
}