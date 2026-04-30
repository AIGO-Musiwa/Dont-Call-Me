using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerSlotUI : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────
    [Header("캐릭터 디스플레이")]
    public RawImage characterDisplay;

    [Header("패널")]
    [SerializeField] private GameObject filledPanel;

    [Header("플레이어 정보")]
    [SerializeField] private TextMeshProUGUI nicknameText;
    [SerializeField] private TextMeshProUGUI readyText;
    
    [Header("상태 표시 등")]
    public GameObject hostBadge;      // 방장 표시 아이콘

    [Header("레벨 미터")]
    [SerializeField] private PlayerSlotLevelMeter levelMeter;

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
        levelMeter?.setTarget(data);
        Refresh();
    }

    // 슬롯 빈 상태로 표시
    public void SetEmpty()
    {
        _boundPlayer = null;
        filledPanel.SetActive(false);
        levelMeter?.Clear();
    }

    // 바인딩된 데이터로 UI 갱신
    public void Refresh()
    {
        if (_boundPlayer == null || _boundPlayer.Object == null || !_boundPlayer.Object.IsValid) return;

        hostBadge.SetActive(_boundPlayer.IsHost);
        nicknameText.text = _boundPlayer.Nickname.ToString();

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

    // RenderTexture 등록
    public void SetCharacter(RenderTexture renderTexture)
    {
        if (characterDisplay == null) return;
        characterDisplay.texture = renderTexture;
        characterDisplay.gameObject.SetActive(true);
    }

    // 플레이어 퇴장 시 RenderTexture 해제
    public void ClearCharacter()
    {
        if (characterDisplay == null) return;
        characterDisplay.texture = null;
        characterDisplay.gameObject.SetActive(false);
    }

    #endregion
}
