using System.Collections.Generic;
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

    [Header("통계 패널")]
    [SerializeField] private TextMeshProUGUI survivorCountText;
    [SerializeField] private TextMeshProUGUI playTimeText;
    [SerializeField] private TextMeshProUGUI puzzlesSolvedText;
    [SerializeField] private TextMeshProUGUI radioUsedText;

    [Header("타임라인")]
    [SerializeField] private Transform timelineContainer;
    [SerializeField] private TimelineEventCardUI timelineCardPrefab;

    [Header("버튼")]
    [SerializeField] private Button returnButton;

    [Header("오버레이 컨트롤러")]
    [SerializeField] private ResultOverlayController overlayController;

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

    // ── 공개 API ─────────────────────────────────────────────────────

    // ResultOverlayController.Show()에서 호출
    public void Setup(PendingResult payload)
    {
        isActive = true;

        resultText.text = payload.IsClear ? "탈출 성공" : "게임 오버";

        foreach (var slot in playerSlots)
            slot.SetEmpty();

        var runner = GameLauncher.Instance?.Runner;

        int survivors = 0;

        foreach (var result in payload.PlayerResults)
        {
            if (result.SlotIndex < 0 || result.SlotIndex >= playerSlots.Length) continue;

            // IsHost는 PlayerData에서 읽음
            bool isHost = false;
            if (runner != null)
            {
                foreach (var player in runner.ActivePlayers)
                {
                    var data = runner.GetPlayerObject(player)?.GetComponent<PlayerData>();
                    if (data != null && data.SlotIndex == result.SlotIndex)
                    {
                        isHost = runner.GetPlayerObject(player)?.InputAuthority == runner.LocalPlayer && runner.IsServer;
                        break;
                    }
                }
            }

            playerSlots[result.SlotIndex].SetPlayer(
                result.Nickname,
                result.FinalState,
                result.IsLocalPlayer,
                isHost
                );

            if (result.FinalState == PlayerState.Escaped)
                survivors++;
        }

        // 통계
        int totalPlayers = Mathf.Max(1, payload.PlayerResults.Count);
        survivorCountText.text = $"생존자 {survivors}/{totalPlayers}";
        playTimeText.text = FormatTime(payload.Duration);
        puzzlesSolvedText.text = $"7개 | {payload.PuzzlesSolved}개";
        radioUsedText.text = $"3회 | {payload.RadioUsed}회";

        // 타임라인
        BuildTimeline(payload.TimelineLog);
    }

    // ── 내부 ──────────────────────────────────────────────────────────

    private void BuildTimeline(List<GameEventEntry> log)
    {
        foreach (Transform child in timelineContainer)
            Destroy(child.gameObject);

        if (log == null || log.Count == 0) return;

        // 로컬 플레이어 SlotIndex 미리 조회
        var runner = GameLauncher.Instance?.Runner;
        var localData = runner?.GetPlayerObject(runner.LocalPlayer).GetComponent<PlayerData>();
        int localSlotIndex = localData != null ? localData.SlotIndex : -1;

        foreach (var entry in log)
        {
            var card = Instantiate(timelineCardPrefab, timelineContainer);
            card.Setup(entry, entry.SlotIndex == localSlotIndex);
        }
    }

    private static string FormatTime(float totalSeconds)
    {
        int m = Mathf.FloorToInt(totalSeconds / 60f);
        int s = Mathf.FloorToInt(totalSeconds % 60f);
        return $"{m:D2} : {s:D2}";
    }

    private void OnReturnClicked()
    {
        isActive = false;
        overlayController.OnReturnToLobbyClicked();
    }
}
