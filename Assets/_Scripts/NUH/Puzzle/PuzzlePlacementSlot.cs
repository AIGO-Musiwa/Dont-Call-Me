using UnityEngine;

/// <summary>
/// 퍼즐 본체를 배치하는 슬롯
/// </summary>
public class PuzzlePlacementSlot : MonoBehaviour
{
    [SerializeField] private string slotId;     // 디버그용 슬롯 ID

    public string SlotId => slotId;
}
