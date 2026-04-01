using Fusion;
using Fusion.Sockets;
using Photon.Voice.Unity;
using System;
using UnityEngine;

public class NetworkManager : MonoBehaviour
{
    public static NetworkManager Instance { get; private set; }

    [Header("Runner 설정")]
    [SerializeField] private NetworkRunner runnerPrefab;

    // 외부에서 읽기 전용
    public NetworkRunner Runner       { get; private set; }
    public string        LocalNickname { get; private set; }
    public string        RoomCode      { get; private set; }

    // 호스트 연결 끊김 시 발생 — 로비/게임 씬 UI에서 구독
    public event Action        OnHostDisconnected;
    // 방 참가 실패 시 발생 — TitleManager에서 구독
    public event Action<string> OnJoinFailed;

    // LeaveRoom() 호출 시 세팅 → OnShutdown 중복 처리 방지
    private bool _intentionalShutdown;

    // ── 생명주기 ───────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ── 방 생성 (Host) ────────────────────────────────────────
    public async void CreateRoom(string nickname)
    {
        LocalNickname      = nickname;
        RoomCode           = GenerateRoomCode();
        _intentionalShutdown = false;

        Runner = Instantiate(runnerPrefab);
        Runner.AddCallbacks(GetComponent<NetworkRunnerCallbacks>());

        var result = await Runner.StartGame(new StartGameArgs
        {
            GameMode    = GameMode.Host,
            SessionName = RoomCode,
            PlayerCount = 4,
            // 게임 시작 시 Fusion이 로비 씬으로 모든 클라이언트를 이동시킴
            Scene       = SceneRef.FromIndex(SceneNames.LobbyIndex),
            SceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>()
        });

        if (result.Ok)
            InitVoice();
        else
        {
            Debug.LogError($"[NetworkManager] 방 생성 실패: {result.ShutdownReason}");
            Destroy(Runner.gameObject);
            Runner = null;
        }
    }

    // ── 방 참가 (Client) ──────────────────────────────────────
    public async void JoinRoom(string nickname, string code)
    {
        LocalNickname      = nickname;
        RoomCode           = code.ToUpper().Trim();
        _intentionalShutdown = false;

        Runner = Instantiate(runnerPrefab);
        Runner.AddCallbacks(GetComponent<NetworkRunnerCallbacks>());

        var result = await Runner.StartGame(new StartGameArgs
        {
            GameMode    = GameMode.Client,
            SessionName = RoomCode,
            SceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>()
        });

        if (result.Ok)
            InitVoice();
        else
        {
            Debug.LogError($"[NetworkManager] 방 참가 실패: {result.ShutdownReason}");
            OnJoinFailed?.Invoke(GetJoinFailMessage(result.ShutdownReason));
            Destroy(Runner.gameObject);
            Runner = null;
        }
        // 성공 시 Fusion이 호스트의 씬(로비)으로 자동 이동
    }

    // ── 방 나가기 ─────────────────────────────────────────────
    public async void LeaveRoom()
    {
        if (Runner == null) return;
        _intentionalShutdown = true;
        await Runner.Shutdown();
        Runner = null;
    }

    // ── Voice 초기화 ──────────────────────────────────────────
    private void InitVoice()
    {
        var recorder = Runner.GetComponentInChildren<Recorder>();
        GetComponent<VoiceManager>()?.Init(recorder);
    }

    // ── DevGameBootstrap 전용 (에디터 전용) ──────────────────────
#if UNITY_EDITOR
    internal void SetDevData(string nickname, string roomCode)
    {
        LocalNickname = nickname;
        RoomCode      = roomCode;
    }

    internal void SetRunner(NetworkRunner runner)
    {
        Runner = runner;
    }
#endif

    // ── NetworkRunnerCallbacks에서 호출 ───────────────────────
    internal void NotifyShutdown(ShutdownReason reason)
    {
        if (_intentionalShutdown) return;

        Debug.LogWarning($"[NetworkManager] 예기치 않은 종료: {reason}");
        Runner = null;
        OnHostDisconnected?.Invoke();
    }

    internal void NotifyConnectFailed(NetConnectFailedReason reason)
    {
        OnJoinFailed?.Invoke(GetJoinFailMessage(reason));
    }

    // ── 내부 유틸 ─────────────────────────────────────────────
    private string GenerateRoomCode()
    {
        // 혼동하기 쉬운 문자(O, I, 0, 1) 제외
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var code = new char[6];
        var rng  = new System.Random();
        for (int i = 0; i < 6; i++)
            code[i] = chars[rng.Next(chars.Length)];
        return new string(code);
    }

    private string GetJoinFailMessage(ShutdownReason reason) => reason switch
    {
        ShutdownReason.GameNotFound => "존재하지 않는 방 코드입니다.",
        ShutdownReason.GameIsFull   => "방이 가득 찼습니다.",
        _                           => $"방 참가 실패 ({reason})"
    };

    private string GetJoinFailMessage(NetConnectFailedReason reason) => reason switch
    {
        NetConnectFailedReason.Timeout => "연결 시간이 초과되었습니다.",
        _                              => $"연결 실패 ({reason})"
    };
}
