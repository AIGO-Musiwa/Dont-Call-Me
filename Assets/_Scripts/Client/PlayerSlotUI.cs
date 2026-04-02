using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerSlotUI : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────
    [Header("패널")]
    [SerializeField] private GameObject filledPanel;
    [SerializeField] private GameObject emptyPanel;

    [Header("플레이어 정보")]
    [SerializeField] private TextMeshProUGUI nicknameText;
    [SerializeField] private Image readyIcon;
    [SerializeField] private Image micIcon;

    // ── 내부 ──────────────────────────────────────────────
    private PlayerLobbyData _boundPlayer;

    private void Start()
    {
        SetEmpty();
    }

    #region 외부 공개 메서드

    // 슬롯에 플레이어 데이터 바인딩 후 UI 갱신
    public void SetPlayer(PlayerLobbyData data)
    {
        _boundPlayer = data;

        filledPanel.SetActive(true);
        emptyPanel.SetActive(false);

        Refresh();
    }

    // 슬롯 빈 상태로 표시
    public void SetEmpty()
    {
        _boundPlayer = null;

        filledPanel.SetActive(false);
        emptyPanel.SetActive(true);
    }

    // 바인딩된 데이터로 UI 갱신
    public void Refresh()
    {
        if (_boundPlayer == null || !_boundPlayer.Object.IsValid) return;

        nicknameText.text = _boundPlayer.Nickname.ToString();
        readyIcon.enabled = _boundPlayer.IsReady;

        // TODO: VoiceManager 연동 후 마이크 상태 반영
        micIcon.enabled = false;
    }

    #endregion
}
