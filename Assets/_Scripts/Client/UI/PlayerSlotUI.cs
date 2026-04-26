using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerSlotUI : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────
    [Header("모니터링 장치 (RTT 연동용)")]
    public RawImage characterDisplay; // 나중에 Render Texture가 들어올 자리

    [Header("패널")]
    [SerializeField] private GameObject filledPanel;

    [Header("플레이어 정보")]
    [SerializeField] private TextMeshProUGUI nicknameText;
    [SerializeField] private TextMeshProUGUI readyText;
    [SerializeField] private Image micIcon;
    
    [Header("상태 표시 등")]
    public GameObject hostBadge;      // 방장 표시 아이콘

    // ── 내부 ──────────────────────────────────────────────
    private PlayerData _boundPlayer;

    private void Start()
    {
        SetEmpty();
    }

    #region 외부 공개 메서드

    // 슬롯에 플레이어 데이터 바인딩 후 UI 갱신
    public void SetPlayer(PlayerData data)
    {
        _boundPlayer = data;
        filledPanel.SetActive(true);
        Refresh();
    }

    // 슬롯 빈 상태로 표시
    public void SetEmpty()
    {
        _boundPlayer = null;
        filledPanel.SetActive(false);
    }

    // 바인딩된 데이터로 UI 갱신
    public void Refresh()
    {
        if (_boundPlayer == null || _boundPlayer.Object == null || !_boundPlayer.Object.IsValid) return;

        hostBadge.SetActive(_boundPlayer.IsHost);
        nicknameText.text = _boundPlayer.Nickname.ToString();
        micIcon.enabled = _boundPlayer.IsMicActive;

        // 결과 화면을 확인 중인 플레이어는 상태 텍스트만 교체
        if (_boundPlayer.IsReviewingResult)
        {
            readyText.text = "<color=#AAAAAA>결과 확인중</color>";
            return;
        }

        readyText.text = _boundPlayer.IsReady
            ? "<color=#00FF00>READY</color>"        // 녹색
            : "<color=#FF0000>WAITING</color>";     // 적색
    }

    #endregion
}
