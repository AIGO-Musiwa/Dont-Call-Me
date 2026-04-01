using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// [수정 권한] 클라이언트 담당자
// 대기실 씬 UI 컨트롤러
public class LobbyUIManager : MonoBehaviour
{
    [Header("플레이어 슬롯 (4개, 순서대로)")]
    [SerializeField] private PlayerSlotUI[] playerSlots;  // Inspector에서 4개 연결

    [Header("버튼")]
    [SerializeField] private Button readyButton;          // 클라이언트에게 표시
    [SerializeField] private Button startButton;          // 호스트에게 표시
    [SerializeField] private Button exitButton;
    [SerializeField] private Button settingsButton;

    [Header("텍스트")]
    [SerializeField] private TMP_Text roomCodeText;

    private LobbyStateManager _lobbyState;
    private NetworkRunner     _runner;
    private bool              _isReady;

    // ── 생명주기 ───────────────────────────────────────────────
    private void Start()
    {
        _runner     = NetworkManager.Instance.Runner;
        _lobbyState = FindFirstObjectByType<LobbyStateManager>();

        roomCodeText.text = $"방 코드: {NetworkManager.Instance.RoomCode}";

        // 호스트 / 클라이언트 버튼 분리
        bool isHost = _runner.IsServer;
        readyButton.gameObject.SetActive(!isHost);
        startButton.gameObject.SetActive(isHost);

        startButton.interactable = false;

        NetworkManager.Instance.OnHostDisconnected += OnHostDisconnected;
    }

    private void OnDestroy()
    {
        if (NetworkManager.Instance != null)
            NetworkManager.Instance.OnHostDisconnected -= OnHostDisconnected;
    }

    // ── 매 프레임 갱신 ────────────────────────────────────────
    private void Update()
    {
        RefreshPlayerSlots();

        // 호스트: CanStart 상태에 따라 시작 버튼 활성화
        if (_runner.IsServer && _lobbyState != null)
            startButton.interactable = _lobbyState.CanStart;
    }

    // ── 버튼 이벤트 (Inspector에서 연결) ─────────────────────
    public void OnClickReady()
    {
        _isReady = !_isReady;
        GetMyLobbyData()?.RPC_SetReady(_isReady);

        // 버튼 텍스트 토글
        readyButton.GetComponentInChildren<TMP_Text>().text = _isReady ? "준비 취소" : "준비";
    }

    public void OnClickStart()
    {
        _lobbyState?.StartGame();
    }

    public void OnClickExit()
    {
        NetworkManager.Instance.LeaveRoom();
        SceneManager.LoadScene(SceneNames.Title);
    }

    public void OnClickSettings()
    {
        // TODO: 설정 패널 열기
    }

    // ── 내부 유틸 ─────────────────────────────────────────────
    private void RefreshPlayerSlots()
    {
        if (_runner == null) return;

        var allData = new System.Collections.Generic.List<PlayerLobbyData>(
            _runner.GetAllBehaviours<PlayerLobbyData>()
        );

        for (int i = 0; i < playerSlots.Length; i++)
        {
            if (i < allData.Count)
                playerSlots[i].SetData(allData[i]);
            else
                playerSlots[i].SetEmpty();
        }
    }

    private PlayerLobbyData GetMyLobbyData()
    {
        foreach (var data in _runner.GetAllBehaviours<PlayerLobbyData>())
        {
            if (data.HasInputAuthority) return data;
        }
        return null;
    }

    private void OnHostDisconnected()
    {
        // NetworkManager.OnShutdown에서 이미 처리하지만
        // UI 메시지 표시가 필요하면 여기에 추가
        SceneManager.LoadScene(SceneNames.Title);
    }
}
