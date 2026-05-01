using Fusion;
using NUnit.Framework.Constraints;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LobbyManager : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────
    [Header("패널")]
    [SerializeField] private GameObject lobbyPanel;
    [SerializeField] private GameObject resultPanel;

    [Header("결과 오버레이")]
    [SerializeField] private ResultOverlayController resultOverlay;

    [Header("방정보")]
    [SerializeField] private TextMeshProUGUI roomCodeText;
    [SerializeField] private TextMeshProUGUI playerCountText;

    [Header("방 코드 복사")]
    [SerializeField] private Button copyRoomCodeButton;
    [SerializeField] private TextMeshProUGUI copyFeedbackText;  // "복사됨" 표시용

    [Header("플레이어 슬롯 (4개)")]
    [SerializeField] private PlayerSlotUI[] playerSlots;

    [Header("버튼")]
    [SerializeField] private Button readyButton;
    [SerializeField] private Button startButton;
    [SerializeField] private Button exitButton;

    private GameLauncher _launcher;
    private bool _isReady;
    private readonly PlayerData[] _slots = new PlayerData[4];

    private readonly Dictionary<PlayerRef, int> _playerRefToSlot = new();

    private Coroutine copyFeedbackCoroutine;        // 복사 피드백 코루틴 핸들

    private void Start()
    {
        resultPanel.SetActive(false);

        _launcher = GameLauncher.Instance;
        if (_launcher == null)
        {
            Debug.LogError("[LobbyManager] GameLauncher가 씬에 없습니다.");
            return;
        }
        _launcher.OnPlayerJoinedEvent += HandlePlayerJoined;
        _launcher.OnPlayerLeftEvent   += HandlePlayerLeft;
        _launcher.OnHostDisconnected  += HandleHostDisconnected;

        // 로비 복귀 시 커서 복원
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (_launcher.Runner != null)
            ShowLobby(_launcher.Runner);

        if (copyRoomCodeButton != null)
            copyRoomCodeButton.onClick.AddListener(OnCopyRoomCodeClicked);

        // 복사 피드백 텍스트 초기화
        if (copyFeedbackText != null)
            copyFeedbackText.gameObject.SetActive(false);

        // 슬롯 재스캔
        StartCoroutine(RebuildSlotsAndShowOverlay());

        Debug.Log($"[LobbyManager] Start() 실행 | Runner={_launcher.Runner != null}");
    }

    private void OnDestroy()
    {
        if (_launcher == null) return;
        _launcher.OnPlayerJoinedEvent -= HandlePlayerJoined;
        _launcher.OnPlayerLeftEvent   -= HandlePlayerLeft;
        _launcher.OnHostDisconnected  -= HandleHostDisconnected;

        if (copyRoomCodeButton != null)
            copyRoomCodeButton.onClick.RemoveListener(OnCopyRoomCodeClicked);
    }

    private void Update()
    {
        foreach (var slot in playerSlots)
            slot.Refresh();

        if (_launcher?.Runner != null && _launcher.Runner.IsServer)
            UpdateStartButton();

        // 준비/시작 단축키
        HadnleShortcutKey();
    }

    // 단축키 
    private void HadnleShortcutKey()
    {
        if (!Keyboard.current.f5Key.wasPressedThisFrame) return;
        if (_launcher?.Runner == null) return;

        if (_launcher.Runner.IsServer)
        {
            // 호스트: 시작 버튼이 활성화된 상태일 때만 시작
            if (startButton != null && startButton.interactable)
                OnStartClicked();
        }
        else
        {
            // 클라이언트: 준비 토글
            OnReadyClicked();
        }
    }

    #region 이벤트 처리

    private void HandlePlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        if (player == runner.LocalPlayer)
        {
            ShowLobby(runner);
        }
        StartCoroutine(AssignSlotNextFrame(player));
    }

    private void HandlePlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        // 슬롯 해제
        if (_playerRefToSlot.TryGetValue(player, out var slotIndex))
        {
            LobbyCharacterViewer.Instance?.OnPlayerLeft(slotIndex);

            _slots[slotIndex] = null;
            _playerRefToSlot.Remove(player);
        }
        RefreshSlots();
    }
    
    private void HandleHostDisconnected()
    {
        if (GameLauncher.Instance != null)
            GameLauncher.Instance.PendingErrorMessage = "호스트 연결이 끊겼습니다.";
        SceneManager.LoadScene(SceneNames.TITLE_INDEX);
    }

    private IEnumerator RebuildSlotsAndShowOverlay()
    {
        yield return null;

        var allData = FindObjectsByType<PlayerData>(FindObjectsSortMode.None);
        foreach (var data in allData)
        {
            if (data.SlotIndex >= 0 && data.SlotIndex < _slots.Length)
            {
                _slots[data.SlotIndex] = data;
                if (data.Object != null)
                    _playerRefToSlot[data.Object.InputAuthority] = data.SlotIndex;
            }
        }
        RefreshSlots();

        LobbyCharacterViewer.Instance?.RebuildFromExistingPlayerData();

        // 슬롯 바인딩 완료 후 오버레이 표시
        if (ResultPayload.Pending != null && resultOverlay != null)
            resultOverlay.Show(ResultPayload.Pending);
    }

    #endregion

    #region 버튼 콜백

    // 레디 버튼
    public void OnReadyClicked()
    {
        _isReady = !_isReady;

        var myData = _launcher.Runner
            ?.GetPlayerObject(_launcher.Runner.LocalPlayer)
            ?.GetComponent<PlayerData>();
        myData?.Rpc_SetReady(_isReady);
    }
    
    // 시작 버튼
    public void OnStartClicked()
    {
        _launcher.Runner.LoadScene(SceneRef.FromIndex(SceneNames.GAME_INDEX));
    }

    // 나가기 버튼
    public async void OnExitClicked()
    {
        await _launcher.LeaveRoom();
        _isReady = false;
        SceneManager.LoadScene(SceneNames.TITLE_INDEX);

    }

    // 복사 버튾ㄱ
    public void OnCopyRoomCodeClicked()
    {
        if (string.IsNullOrEmpty(_launcher?.RoomCode)) return;

        GUIUtility.systemCopyBuffer = _launcher.RoomCode;
        Debug.Log($"[LobbyManager] 방 코드 복사됨: {_launcher.RoomCode}");

        // 피드백 텍스트가 있으면 잠깐 표시
        if (copyFeedbackText != null)
        {
            if (copyFeedbackCoroutine != null)
                StopCoroutine(copyFeedbackCoroutine);
            copyFeedbackCoroutine = StartCoroutine(ShowCopyFeedback());
        }
    }

    private IEnumerator ShowCopyFeedback()
    {
        copyFeedbackText.gameObject.SetActive(true);
        yield return new WaitForSeconds(1.5f);
        copyFeedbackText.gameObject.SetActive(false);
        copyFeedbackCoroutine = null;
    }
    #endregion

    #region 내부 유틸

    private void ShowLobby(NetworkRunner runner)
    {
        lobbyPanel.SetActive(true);
        roomCodeText.text = $"{_launcher.RoomCode}";

        bool isHost = runner.IsServer;
        readyButton.gameObject.SetActive(!isHost);
        startButton.gameObject.SetActive(isHost);
        startButton.interactable = false;
    }

    private IEnumerator AssignSlotNextFrame(PlayerRef player)
    {
        yield return null;
       
        // PlayerData 찾기
        var allData = FindObjectsByType<PlayerData>(FindObjectsSortMode.None);
        var data = allData.FirstOrDefault(d => d.Object.InputAuthority == player);
        if (data == null || data.SlotIndex < 0) yield break;

        _slots[data.SlotIndex] = data;
        _playerRefToSlot[player] = data.SlotIndex;

        if (data.CharacterIndex >= 0)
            LobbyCharacterViewer.Instance?.OnCharacterAssigned(data.SlotIndex, data.CharacterIndex);

        RefreshSlots();
    }

    private void RefreshSlots()
    {
        for (int i = 0; i < playerSlots.Length; i++)
        {
            if (_slots[i] != null)
                playerSlots[i].SetPlayer(_slots[i]);
            else
                playerSlots[i].SetEmpty();           
        }

        int count = _slots.Count(s => s != null);
        if (playerCountText != null)
            playerCountText.text = $"플레이어 ({count}/{Constants.MAX_PLAYERS})";
    }

    private void UpdateStartButton()
    {
        int count = _slots.Count(s => s != null);
        bool canStart = count == 4 && _slots.All(s => s == null || s.IsReady);
        startButton.interactable = canStart;
    }

    #endregion
}