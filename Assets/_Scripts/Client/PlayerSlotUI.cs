using TMPro;
using UnityEngine;

// 대기실 플레이어 슬롯 1개 UI
// LobbyUIManager가 PlayerLobbyData를 연결해줌
public class PlayerSlotUI : MonoBehaviour
{
    [SerializeField] private GameObject  filledPanel;   // 플레이어 있을 때
    [SerializeField] private GameObject  emptyPanel;    // 빈 슬롯
    [SerializeField] private TMP_Text    nicknameText;
    [SerializeField] private GameObject  readyCheckmark; // 준비 완료 표시
    [SerializeField] private GameObject  micActiveIcon;  // 마이크 활성화 스피커 아이콘

    private PlayerLobbyData _data;

    // ── LobbyUIManager에서 호출 ───────────────────────────────
    public void SetData(PlayerLobbyData data)
    {
        _data = data;
        Refresh();
    }

    public void SetEmpty()
    {
        _data = null;
        filledPanel.SetActive(false);
        emptyPanel.SetActive(true);
    }

    // ── 매 프레임 갱신 ────────────────────────────────────────
    private void Update()
    {
        if (_data != null) Refresh();
    }

    private void Refresh()
    {
        bool hasData = _data != null && _data.Object != null;
        filledPanel.SetActive(hasData);
        emptyPanel.SetActive(!hasData);

        if (!hasData) return;

        nicknameText.text = _data.Nickname.ToString();
        readyCheckmark.SetActive(_data.IsReady);
        micActiveIcon.SetActive(_data.IsMicActive);
    }
}
