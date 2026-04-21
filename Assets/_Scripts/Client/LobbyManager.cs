using Fusion;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
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

    [Header("플레이어 슬롯 (4개)")]
    [SerializeField] private PlayerSlotUI[] playerSlots;

    [Header("버튼")]
    [SerializeField] private Button readyButton;
    [SerializeField] private Button startButton;
    [SerializeField] private Button exitButton;

    private GameLauncher _launcher;
    private bool _isReady;
    private readonly PlayerData[] _slots = new PlayerData[4];

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
    }

    private void Update()
    {
        if (!lobbyPanel.activeSelf) return;

        foreach (var slot in playerSlots)
            slot.Refresh();

        if (_launcher?.Runner != null && _launcher.Runner.IsServer)
            UpdateStartButton();
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
        for (int i = 0; i < _slots.Length; i++)
        {
            if (_slots[i] != null && _slots[i].Object.InputAuthority == player)
            {
                _slots[i] = null;
                break;
            }
        }
        RefreshSlots();
    }
    
    private void HandleHostDisconnected()
    {
        // TODO : 호스트 연결 끊김 알람
        SceneManager.LoadScene(SceneNames.TITLE_INDEX);
    }

    private IEnumerator RebuildSlotsAndShowOverlay()
    {
        yield return null;

        var allData = FindObjectsByType<PlayerData>(FindObjectsSortMode.None);
        foreach (var data in allData)
        {
            if (data.SlotIndex >= 0 && data.SlotIndex < _slots.Length)
                _slots[data.SlotIndex] = data;
        }
        RefreshSlots();

        // 슬롯 바인딩 완료 후 오버레이 표시
        if (ResultPayload.Pending != null && resultOverlay != null)
            resultOverlay.Show(ResultPayload.Pending);
    }

    #endregion

    #region 버튼 콜백

    public void OnReadyClicked()
    {
        _isReady = !_isReady;

        var myData = _launcher.Runner
            ?.GetPlayerObject(_launcher.Runner.LocalPlayer)
            ?.GetComponent<PlayerData>();
        myData?.Rpc_SetReady(_isReady);
    }

    public void OnStartClicked()
    {
        _launcher.Runner.LoadScene(SceneRef.FromIndex(SceneNames.GAME_INDEX));
    }

    public async void OnExitClicked()
    {
        await _launcher.LeaveRoom();
        _isReady = false;
        SceneManager.LoadScene(SceneNames.TITLE_INDEX);

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
    }

    private void UpdateStartButton()
    {
        int count = _slots.Count(s => s != null);
        bool canStart = count == 4 && _slots.All(s => s == null || s.IsReady);
        startButton.interactable = canStart;
    }

    #endregion
}