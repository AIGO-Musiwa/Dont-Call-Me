using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class ResultUI : MonoBehaviour
{
    [Header("결과 텍스트")]
    [SerializeField] private TextMeshProUGUI resultText;

    [Header("플레이어 결과 슬롯 (4개)")]
    [SerializeField] private PlayerResultSlotUI[] playerSlots;

    [Header("버튼")]
    [SerializeField] private Button returnButton;

    private bool isActive;

    private void Awake()
    {
        returnButton.onClick.AddListener(OnReturnClicked);
    }

    private void Update()
    {
        if (!isActive) return;

        if (Keyboard.current.enterKey.wasPressedThisFrame ||
            Keyboard.current.numpadEnterKey.wasPressedThisFrame)
        {
            OnReturnClicked();
        }
    }

    public void Setup(bool isClear)
    {
        isActive = true;

        resultText.text = isClear ? "게임 클리어" : "게임 오버";

        foreach (var slot in playerSlots)
            slot.SetEmpty();

        var runner = GameLauncher.Instance?.Runner;
        if (runner == null) return;

        int debugSlot = 0;
        foreach (var player in runner.ActivePlayers)
        {
            // 닉네임, 슬롯 인덱스 -> PlayerData
            // 게임 상태 -> PlayerController
            var data = runner.GetPlayerObject(player)?.GetComponent<PlayerData>();
            PlayerController pc = data.GetPlayerController();

            if (data != null && data.SlotIndex >= 0 && data.SlotIndex < playerSlots.Length)
            {
                playerSlots[data.SlotIndex].SetPlayer(
                    data.Nickname.ToString(),
                    pc.NetPlayerState,
                    player == runner.LocalPlayer
                );
            }
            // PlayerData 없으면 (게임 씬 단독 테스트) 임시 처리
            else if (debugSlot < playerSlots.Length)
            {
                playerSlots[debugSlot].SetPlayer(
                    $"Player {player.PlayerId}",
                    pc.NetPlayerState
                );
                debugSlot++;
            }
        }
    }

    private void OnReturnClicked()
    {
        isActive = false;

        if (GameLauncher.Instance.Runner.IsServer)
            GameLauncher.Instance.ReturnToLobby();
        else
            GameSessionManager.Instance?.Rpc_RequestReturnToLobby();
    }
}
