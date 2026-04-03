using Fusion;
using Fusion.Sockets;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class GameLauncher : MonoBehaviour
{
    // ── 싱글톤 (Title 씬 내에서만 유효) ──────────────────
    public static GameLauncher Instance { get; private set; }

    // ── Inspector ─────────────────────────────────────────
    [Header("Fusion 프리팹")]
    [SerializeField] private NetworkRunner networkRunnerPrefab;
    [SerializeField] private NetworkObject playerLobbyDataPrefab;

    // ── 외부 접근 ─────────────────────────────────────────
    public NetworkRunner Runner { get; private set; }
    public string RoomCode { get; private set; }
    public string LocalNickname { get; private set; }

    // ── 이벤트 (Lobby 씬 내 UI에서 구독) ─────────────────
    public event Action<string> OnJoinFailed;                           // 방 참가/생성 실패 시 (TitleManager에서 구독)
    public event Action OnHostDisconnected;                             // 호스트 끊김 시 (LobbyManager에서 구독)
    public event Action<NetworkRunner, PlayerRef> OnPlayerJoinedEvent;  // 플레이어 입장 (LobbyManagert에서 구독)
    public event Action<NetworkRunner, PlayerRef> OnPlayerLeftEvent;    // 플레이어 퇴장 (LobbyManager에서 구독)

    // ── 내부 ──────────────────────────────────────────────
    private bool _intentionalShutdown;                  // 본인이 직접 종료했는지 확인
    private FusionCallbackHandler _callbackHandler;     // Fusion 콜백 핸들러
    private bool _isConnecting;                         // Runner.StartGame 진행 중 플래그

    // 서버에서만 사용하는 슬롯  추적
    private readonly Dictionary<PlayerRef, int> _playerSlots = new();

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
        _callbackHandler.OnPlayerJoinedEvent -= HandlePlayerJoined;
        _callbackHandler.OnPlayerLeftEvent -= HandlePlayerLeft;
        _callbackHandler.OnShutdownEvent -= HandleShutdown;
        _callbackHandler.OnDisconnectedEvent -= HadnleDisconnected;
        _callbackHandler.OnConnectFailedEvent -= HandleConnectFailed;
    }

    #endregion

    #region 공개 API

    // 방 생성
    public async void CreateRoom(string nickname)
    {
        LocalNickname = nickname;
        RoomCode = GenerateRoomCode();
        _intentionalShutdown = false;
        await StartFusion(GameMode.Host, RoomCode);
    }

    // 방 참가
    public async void JoinRoom(string nickname, string roomCode)
    {
        LocalNickname = nickname;
        RoomCode = roomCode.ToUpper().Trim();
        _intentionalShutdown = false;
        await StartFusion(GameMode.Client, RoomCode);
    }

    public async Task LeaveRoom()
    {
        if (Runner == null) return;
        _intentionalShutdown = true;
        await Runner.Shutdown();
        Runner = null;
    }

    #endregion

    #region 내부 연결 처리

    private async Task StartFusion(GameMode mode, string sessionName)
    {
        if (Runner != null)
        {
            await Runner.Shutdown();
            Runner = null;
        }

        _callbackHandler = new FusionCallbackHandler();
        _callbackHandler.OnPlayerJoinedEvent += HandlePlayerJoined;
        _callbackHandler.OnPlayerLeftEvent += HandlePlayerLeft;
        _callbackHandler.OnShutdownEvent += HandleShutdown;
        _callbackHandler.OnDisconnectedEvent += HadnleDisconnected;
        _callbackHandler.OnConnectFailedEvent += HandleConnectFailed;

        Runner = Instantiate(networkRunnerPrefab);
        Runner.name = "NetworkRunner";
        Runner.AddCallbacks(_callbackHandler);
        DontDestroyOnLoad(Runner.gameObject);

        var runnerGo = Runner.gameObject;

        _isConnecting = true;
        var result = await Runner.StartGame(new StartGameArgs
        {
            GameMode = mode,
            SessionName = sessionName,
            PlayerCount = Constants.MAX_PLAYERS,
            SceneManager = Runner.GetComponent<INetworkSceneManager>()
                            ?? Runner.gameObject.AddComponent<NetworkSceneManagerDefault>()
        });
        _isConnecting = false;

        if (!result.Ok)
        {
            Debug.LogError($"[GameLauncher] 연결 실패: {result.ShutdownReason}");
            OnJoinFailed?.Invoke(GetJoinFailMessage(result.ShutdownReason));
            Destroy(runnerGo);
            Runner = null;
        }
        else
        {
            Debug.Log($"[GameLauncher] 연결 성공 | Mode={mode} | Session={sessionName}");
        }
    }

    private string GenerateRoomCode()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var code = new char[Constants.ROOM_CODE_LENGTH];
        var rng = new System.Random();
        for (int i = 0; i < Constants.ROOM_CODE_LENGTH; i++)
        {
            code[i] = chars[rng.Next(chars.Length)];
        }
        return new string(code);
    }

    #endregion 

    #region 콜백 처리

    private void HandlePlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        if (runner.IsServer && playerLobbyDataPrefab != null)
        {
            var obj = runner.Spawn(playerLobbyDataPrefab, Vector3.zero, Quaternion.identity, player);
            runner.SetPlayerObject(player, obj);

            // 첫 번째 빈 슬롯 할당
            for (int i = 0; i < Constants.MAX_PLAYERS; i++)
            {
                if (!_playerSlots.ContainsValue(i))
                {
                    _playerSlots[player] = i;
                    obj.GetComponent<PlayerLobbyData>().SlotIndex = i;
                    break;
                }
            }   
        }
        OnPlayerJoinedEvent?.Invoke(runner, player);
    }

    private void HandlePlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        if (runner.IsServer)
        {
            _playerSlots.Remove(player);
        }
        OnPlayerLeftEvent?.Invoke(runner, player);
    }

    private void HandleShutdown(ShutdownReason reason)
    {
        if (_intentionalShutdown) return;
        Runner = null;

        if (_isConnecting) return;

        Debug.LogWarning($"[GameLauncher] 예기치 않은 종료: {reason}");
        OnHostDisconnected?.Invoke();
    }

    private void HadnleDisconnected(NetDisconnectReason reason)
    {
        if (_intentionalShutdown) return;
        if (_isConnecting) return;

        Debug.LogWarning($"[GameLauncher] 서버 연결 끊김: {reason}");
        OnHostDisconnected?.Invoke();
    }

    private void HandleConnectFailed(NetConnectFailedReason reason)
    {
        Debug.LogError($"[GameLauncher] 연결 거부: {reason}");
        OnJoinFailed?.Invoke(GetJoinFailMessage(reason));
    }

    #endregion

    #region 에러 메세지

    private static string GetJoinFailMessage(ShutdownReason reason) => reason switch
    {   
        ShutdownReason.GameNotFound => "존재하지 않는 방 코드입니다.",
        ShutdownReason.GameIsFull => "방이 가득 찼습니다.",
        _ => $"qkd ckark tlfvo ({reason})"
    };

    private static string GetJoinFailMessage(NetConnectFailedReason reason) => reason switch
    {
        NetConnectFailedReason.Timeout => "연결 시간이 초과되었습니다.",
        _ => $"연결 실패 ({reason})"
    };

    #endregion

    #region 에디터 전영

#if UNITY_EDITOR
    internal void SetDevData(string nickname, string roomCode)
    {
        LocalNickname = nickname;
        RoomCode = roomCode;
    }

    internal void SetDevRunner(NetworkRunner runner, FusionCallbackHandler handler)
    {
        Runner = runner;
        _callbackHandler = handler;
        _callbackHandler.OnPlayerJoinedEvent += HandlePlayerJoined;
        _callbackHandler.OnPlayerLeftEvent += HandlePlayerLeft;
        _callbackHandler.OnShutdownEvent += HandleShutdown;
        _callbackHandler.OnDisconnectedEvent += HadnleDisconnected;
    }

#endif

    #endregion
}
