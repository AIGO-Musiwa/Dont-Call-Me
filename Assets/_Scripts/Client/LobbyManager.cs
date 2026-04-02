using Fusion;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LobbyManager : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────
    [Header("패널")]
    [SerializeField] private GameObject lobbyPanel;

    [Header("방정보")]
    [SerializeField] private TextMeshProUGUI roomCodeText;

    [Header("플레이어 슬롯 (4개)")]
    [SerializeField] private PlayerSlotUI[] playerSlots;

    [Header("버튼")]
    [SerializeField] private Button readyButton;
    [SerializeField] private Button startButton;
    [SerializeField] private Button exitButton;

    [Header("에러 패널")]
    [SerializeField] private GameObject errorPanel;
    [SerializeField] private TextMeshProUGUI errorText;

    private GameLauncher _launcher;
    private bool _isReady;
    private readonly PlayerLobbyData[] _slots = new PlayerLobbyData[4];

    private void Start()
    {
        _launcher = GameLauncher.Instance;
        if (_launcher == null)
        {
            Debug.LogError("[LobbyManager] GameLauncher가 씬에 없습니다.");
            return;
        }
        _launcher.OnPlayerJoinedEvent += HandlePlayerJoined;
        _launcher.OnPlayerLeftEvent   += HandlePlayerLeft;
        _launcher.OnHostDisconnected  += HandleHostDisconnected;
        
        lobbyPanel.SetActive(false);
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
        {
            slot.Refresh();
        }

        if (_launcher?.Runner != null && _launcher.Runner.IsServer)
        {
            UpdateStartButton();
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
        lobbyPanel.SetActive(false);
        FindFirstObjectByType<TitleManager>()?.Show();
        errorText.text = "호스트 연결이 끊겼습니다.";
        errorPanel.SetActive(true);
    }

    #endregion

    #region 버튼 콜백

    public void OnReadyClicked()
    {
        _isReady = !_isReady;

        var myData = _launcher.Runner
            ?.GetPlayerObject(_launcher.Runner.LocalPlayer)
            ?.GetComponent<PlayerLobbyData>();
        myData?.Rpc_SetReady(_isReady);
    }

    public void OnStartClicked()
    {
        _launcher.Runner.LoadScene(SceneRef.FromIndex(SceneNames.GAME_INDEX));
    }

    public async void OnExitClicked()
    {
        await _launcher.LeaveRoom();
        lobbyPanel.SetActive(false);
        _isReady = false;
        FindFirstObjectByType<TitleManager>()?.Show();
    }
    #endregion

    #region 내부 유틸

    private void ShowLobby(NetworkRunner runner)
    {
        lobbyPanel.SetActive(true);
        roomCodeText.text = $"방 코드  {_launcher.RoomCode}";

        bool isHost = runner.IsServer;
        readyButton.gameObject.SetActive(!isHost);
        startButton.gameObject.SetActive(isHost);
        startButton.interactable = false;
    }

    private IEnumerator AssignSlotNextFrame(PlayerRef player)
    {
        yield return null;
       
        // PlayerLobbyData 찾기
        var allData = FindObjectsByType<PlayerLobbyData>(FindObjectsSortMode.None);
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
            {
                playerSlots[i].SetPlayer(_slots[i]);
            }
            else
            {
                playerSlots[i].SetEmpty();
            }
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