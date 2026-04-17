using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 대기실의 개별 슬롯 프리팹에 장착할 부품
public class LobbyPlayerSlot : MonoBehaviour
{
    [Header("모니터링 장치 (RTT 연동용)")]
    public RawImage characterDisplay; // 나중에 Render Texture가 들어올 자리

    [Header("정보 출력부")]
    public TextMeshProUGUI nicknameText;
    public TextMeshProUGUI statusText; // "준비 중...", "준비 완료!"

    [Header("상태 표시 등")]
    public GameObject hostBadge;      // 방장 표시 아이콘
    public GameObject readyCheckIcon; // 준비 완료 체크 표시

    /// <summary>
    /// 외부(네트워크 매니저 등)에서 신호를 받아 슬롯의 출력을 갱신함
    /// </summary>
    public void UpdateSlotDisplay(string name, bool isHost, bool isReady)
    {
        nicknameText.text = name;
        hostBadge.SetActive(isHost);
        readyCheckIcon.SetActive(isReady);

        // 상태에 따른 텍스트 색상 변경 (기공사 스타일: 가독성 강조)
        if (isReady)
        {
            statusText.text = "<color=#00FF00>READY</color>"; // 녹색
        }
        else
        {
            statusText.text = "<color=#FF0000>WAITING</color>"; // 적색
        }
    }
}