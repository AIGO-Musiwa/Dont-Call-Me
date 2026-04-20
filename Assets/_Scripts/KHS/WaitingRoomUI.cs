using UnityEngine;

/// <summary>
/// 대기실 UI 전체를 관리하는 메인 제어반.
/// 새로 설계된 LobbyPlayerSlot 부품들과 통신한다.
/// </summary>
public class WaitingRoomUI : MonoBehaviour
{
    [Header("대기실 슬롯 배열 (인스펙터에서 4개 연결)")]
    // [변경] 기존 PlayerSlotUI 대신 LobbyPlayerSlot 규격을 사용함
    [SerializeField] private LobbyPlayerSlot[] playerSlots;

    /// <summary>
    /// 네트워크 매니저 등 외부 시스템으로부터 신호를 받아 로비 UI를 갱신하는 메인 회로.
    /// </summary>
    /// <param name="slotIndex">갱신할 슬롯 번호 (0~3)</param>
    /// <param name="name">플레이어 닉네임</param>
    /// <param name="host">방장 여부</param>
    /// <param name="ready">준비 완료 여부</param>
    public void UpdateLobbyUI(int slotIndex, string name, bool host, bool ready)
    {
        // 인덱스 범위 체크 (배열 범위를 벗어나면 회로 보호를 위해 리턴)
        if (slotIndex >= 0 && slotIndex < playerSlots.Length)
        {
            // [변경] 신규 규격인 UpdateSlotDisplay 메서드를 호출함
            playerSlots[slotIndex].UpdateSlotDisplay(name, host, ready);
        }
        else
        {
            Debug.LogWarning($"<color=yellow>[Lobby]</color> 잘못된 슬롯 인덱스 접근: {slotIndex}");
        }
    }
}