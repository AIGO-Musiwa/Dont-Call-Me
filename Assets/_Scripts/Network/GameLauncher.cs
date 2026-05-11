using Fusion;
using Fusion.Sockets;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameLauncher : MonoBehaviour
{
    // ── 싱글톤 (Title 씬 내에서만 유효) ──────────────────
    public static GameLauncher Instance { get; private set; }

    // ── Inspector ─────────────────────────────────────────
    [Header("Fusion 프리팹")]
    [SerializeField] private NetworkRunner networkRunnerPrefab;
    [SerializeField] private NetworkObject playerLobbyDataPrefab;

    [Header("캐릭터 설정")]
    [SerializeField] private CharacterprefabRegistry characterRegistry;

    // ── 외부 접근 ─────────────────────────────────────────
    public NetworkRunner Runner { get; private set; }
    public string RoomCode { get; private set; }
    public string LocalNickname { get; private set; }
    public bool IsReturningToLobby { get; private set; }
    public string PendingErrorMessage { get; set; } = string.Empty;     // Title 씬으로 전환 후 표시할 에러 메세지 

    // ── 이벤트 (Lobby 씬 내 UI에서 구독) ─────────────────
    public event Action<string> OnJoinFailed;                           // 방 참가/생성 실패 시 (TitleManager에서 구독)
    public event Action OnHostDisconnected;                             // 호스트 끊김 시 (LobbyManager에서 구독)
    public event Action<NetworkRunner, PlayerRef> OnPlayerJoinedEvent;  // 플레이어 입장 (LobbyManagert에서 구독)
    public event Action<NetworkRunner, PlayerRef> OnPlayerLeftEvent;    // 플레이어 퇴장 (LobbyManager에서 구독)

    public event Action OnSceneLoadStarted;
    public event Action OnGameReady;

    // ── 내부 ──────────────────────────────────────────────
    private bool _intentionalShutdown;                  // 본인이 직접 종료했는지 확인
    private FusionCallbackHandler _callbackHandler;     // Fusion 콜백 핸들러
    private bool _isConnecting;                         // Runner.StartGame 진행 중 플래그

    // 서버에서만 사용하는 슬롯  추적
    private readonly Dictionary<PlayerRef, int> _playerSlots = new();

    // 배정 가능한 캐릭터 인덱스 풀 (Host 전용)
    private readonly List<int> _availableCharacterIndices = new();

    #region Unity LifeCycle

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
            UnsubscribeCallbacks();
        }
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
        _playerSlots.Clear();
        _availableCharacterIndices.Clear();
    }

    // 게임 종료 후 대기실로 복귀
    public void ReturnToLobby()
    {
        if (Runner == null) return;

        if (Runner.IsServer)
            Runner.LoadScene(SceneRef.FromIndex(SceneNames.LOBBY_INDEX));
        
    }

    #endregion

    #region 씬 복귀 처리

    // Fusion이 씬 로드를 완료하면 NetworkSceneManager가 OnSceneLoadDone을 호출함.
    // 그걸 받을 수 없으므로 SceneManager 이벤트로 대신 감지.

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.buildIndex == SceneNames.LOBBY_INDEX)
        {
            IsReturningToLobby = true;

            // 로비 복귀 시 방 잠금 해제
            if (Runner != null && Runner.IsServer && Runner.SessionInfo != null)
            {
                Runner.SessionInfo.IsOpen = true;
                Debug.Log("[GameLauncher] 로비 복귀 → 방 다시 열림");
            }
        }
        else if (scene.buildIndex == SceneNames.GAME_INDEX)
        {
            IsReturningToLobby = false;

            if (Runner != null && Runner.IsServer && Runner.SessionInfo != null)
            {
                Runner.SessionInfo.IsOpen = false;
                Debug.Log("[GameLauncher] 게임 시작 → 방 잠금");
            }
        }
        else
        {
            IsReturningToLobby = false;
        }
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

        // 캐릭터 풀 초기화
        InitCharacterPool();

        _callbackHandler = new FusionCallbackHandler();
        SubscribeCallbacks();

        Runner = Instantiate(networkRunnerPrefab);
        Runner.name = "NetworkRunner";
        Runner.AddCallbacks(_callbackHandler);
        DontDestroyOnLoad(Runner.gameObject);

        var inputHandler = Runner.GetComponent<InputHandler>();
        if (inputHandler != null)
        {
            inputHandler.Initialize(_callbackHandler);
        }
        else
        {
            Debug.LogWarning("[GameLauncher] NetworkRunner 프리팹에 InputHandler 컴포넌트가 없습니다.");
        }

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

    #region 캐릭터 풀 관리

    private void InitCharacterPool()
    {
        _availableCharacterIndices.Clear();

        int count = characterRegistry != null
            ? characterRegistry.Count
            : Constants.MAX_PLAYERS;

        if (count == 0)
        {
            Debug.LogWarning("[GameLauncher] CharacterPrefabRegistry에 등록된 캐릭터가 없습니다.");
            return;
        }

        for (int i = 0; i < count; i++)
            _availableCharacterIndices.Add(i);

        Debug.Log($"[GameLauncher] 캐릭터 풀 초기화 | 캐릭터 수={count}");
    }

    #endregion

    #region 콜백 구독/해제

    private void SubscribeCallbacks()
    {
        _callbackHandler.OnPlayerJoinedEvent += HandlePlayerJoined;
        _callbackHandler.OnPlayerLeftEvent += HandlePlayerLeft;
        _callbackHandler.OnShutdownEvent += HandleShutdown;
        _callbackHandler.OnDisconnectedEvent += HandleDisconnected;
        _callbackHandler.OnConnectFailedEvent += HandleConnectFailed;

        _callbackHandler.OnSceneLoadStartEvent += HandleSceneLoadStart;
    }

    private void UnsubscribeCallbacks()
    {
        if (_callbackHandler == null) return;
        _callbackHandler.OnPlayerJoinedEvent -= HandlePlayerJoined;
        _callbackHandler.OnPlayerLeftEvent -= HandlePlayerLeft;
        _callbackHandler.OnShutdownEvent -= HandleShutdown;
        _callbackHandler.OnDisconnectedEvent -= HandleDisconnected;
        _callbackHandler.OnConnectFailedEvent -= HandleConnectFailed;

        _callbackHandler.OnSceneLoadStartEvent -= HandleSceneLoadStart;
    }

    #endregion

    #region 콜백 처리

    private void HandlePlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        if (runner.IsServer && playerLobbyDataPrefab != null)
        {
            var obj = runner.Spawn(playerLobbyDataPrefab, Vector3.zero, Quaternion.identity, player);
            runner.SetPlayerObject(player, obj);
            DontDestroyOnLoad(obj.gameObject);

            var data = obj.GetComponent<PlayerData>();

            // 첫 번째 빈 슬롯 할당
            for (int i = 0; i < Constants.MAX_PLAYERS; i++)
            {
                if (!_playerSlots.ContainsValue(i))
                {
                    _playerSlots[player] = i;
                    obj.GetComponent<PlayerData>().SlotIndex = i;
                    break;
                }
            }   
            if (_availableCharacterIndices.Count > 0)
            {
                int pick = UnityEngine.Random.Range(0, _availableCharacterIndices.Count);
                int characterIndex = _availableCharacterIndices[pick];
                _availableCharacterIndices.RemoveAt(pick);
                data.CharacterIndex = characterIndex;

                Debug.Log($"[GameLauncher] 캐릭터 배정 | Player={player} | SlotIndex={data.SlotIndex} | CharacterIndex={characterIndex}");
            }
            else
            {
                Debug.LogWarning($"[GameLauncher] 배정 가능한 캐릭터 인덱스 없음 | Player={player}");
            }
        }
        OnPlayerJoinedEvent?.Invoke(runner, player);
    }

    private void HandlePlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        if (runner.IsServer)
        {
            var obj = runner.GetPlayerObject(player);
            if (obj != null)
            {
                // 캐릭터 인덱스 풀 반환
                var data = obj.GetComponent<PlayerData>();
                if (data != null && data.CharacterIndex >= 0)
                {
                    _availableCharacterIndices.Add(data.CharacterIndex);
                    Debug.Log($"[GameLauncher] 캐릭터 인덱스 반환 | Player={player} | CharacterIndex={data.CharacterIndex}");
                }
            }

            _playerSlots.Remove(player);
        }
        OnPlayerLeftEvent?.Invoke(runner, player);

        if (runner.IsServer)
        {
            var obj = runner.GetPlayerObject(player);
            if (obj != null)
                runner.Despawn(obj);
        }
    }

    private void HandleShutdown(ShutdownReason reason)
    {
        if (_intentionalShutdown) return;
        Runner = null;

        if (_isConnecting) return;

        Debug.LogWarning($"[GameLauncher] 예기치 않은 종료: {reason}");
        OnHostDisconnected?.Invoke();
    }

    private void HandleDisconnected(NetDisconnectReason reason)
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

    private void HandleSceneLoadStart()
    => OnSceneLoadStarted?.Invoke();

    public void NotifyGameReady()
    {
        OnGameReady?.Invoke();
    }

    #endregion

    #region 에러 메세지

    private static string GetJoinFailMessage(ShutdownReason reason) => reason switch
    {   
        ShutdownReason.GameNotFound => "존재하지 않는 방 코드입니다.",
        ShutdownReason.GameIsFull => "방이 가득 찼습니다.",
        ShutdownReason.GameClosed => "이미 게임이 시작된 방입니다.",
        _ => $"방 참가 실패({reason})"
    };

    private static string GetJoinFailMessage(NetConnectFailedReason reason) => reason switch
    {
        NetConnectFailedReason.Timeout => "연결 시간이 초과되었습니다.",
        _ => $"연결 실패 ({reason})"
    };

    #endregion

    #region 에디터 전영

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
        _callbackHandler.OnDisconnectedEvent += HandleDisconnected;
    }

    internal void SetDevPlayerDataPrefab(NetworkObject prefab)
    {
        playerLobbyDataPrefab = prefab;
    }

    internal void SetDevCharacterRegistry(CharacterprefabRegistry registry)
    {
        characterRegistry = registry;
        InitCharacterPool();
    }

    #endregion
}
