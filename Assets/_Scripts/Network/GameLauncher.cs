using Fusion;
using Fusion.Sockets;
using System;
using UnityEngine;

public class GameLauncher : MonoBehaviour
{
    // ── 싱글톤 (Lobby 씬 내에서만 유효) ──────────────────
    public static GameLauncher Instance { get; private set; }

    // ── Inspector ─────────────────────────────────────────
    [Header("Fusion 프리팹")]
    [SerializeField] private NetworkRunner networkRunnerPrefab;
    [SerializeField] private NetworkObject playerLobbyDataPrefab;

    // ── 외부 접근 ─────────────────────────────────────────
    public NetworkRunner Runner { get; private set; }
    public string RoomCode { get; private set; }
    public string Nickname { get; private set; }

    // ── 이벤트 (Lobby 씬 내 UI에서 구독) ─────────────────
    public event Action<string> OnJoinFailed;                           // 방 참가/생성 실패 시 (TitleManager에서 구독)
    public event Action OnHostDisconnected;                             // 호스트 끊김 시 (LobbyManager에서 구독)
    public event Action<NetworkRunner, PlayerRef> OnPlayerJoinedEvent;  // 플레이어 입장 (LobbyManagert에서 구독)
    public event Action<NetworkRunner, PlayerRef> OnPlayerLeftEvent;    // 플레이어 퇴장 (LobbyManager에서 구독)

    // ── 내부 ──────────────────────────────────────────────
    private bool _intentionalShutdown;                  // 본인이 직접 종료했는지 확인
    private FusionCallbackHandler _callbackHandler;     // Fusion 콜백 핸들러

    #region Unity LifeCycle

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        Instance = null;

        if (_callbackHandler == null) return;
        _callbackHandler.OnPlayerJoinedEvent -= 
    }

    #endregion


    #region 콜백 처리

    private void HandlePlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        if (runner.IsServer && playerLobbyDataPrefab != null)
        {
            runner.Spawn(playerLobbyDataPrefab, Vector3.zero, Quaternion.identity, player);
        }
        OnPlayerJoinedEvent?.Invoke(runner, player);
    }

    private void HandlePlayerLeft(NetworkRunner runner, PlayerRef player)
        => OnPlayerLeftEvent?.Invoke(runner, player);

    private void HandleShutdown(ShutdownReason reason)
    {
        if (_intentionalShutdown) return;
        Debug.LogWarning($"[GameLauncher] 예기치 않은 종료: {reason}");
        Runner = null;
        OnHostDisconnected?.Invoke();
    }

    private void HadnleDisconnected(NetDisconnectReason reason)
    {
        if (_intentionalShutdown) return;
        Debug.LogWarning($"[GameLauncher] 서버 연결 끊김: {reason}");
        OnHostDisconnected?.Invoke();
    }

    private void HandleConnectFailed(NetConnectFailedReason reason)
    {
        Debug.LogError($"[GameLauncher] 연결 거부: {reason}");
        OnJoinFailed?.Invoke();
    }

    #endregion
}
