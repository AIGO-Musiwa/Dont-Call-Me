using UnityEngine;

/// <summary>
/// 힌트 오브젝트를 배치하는 슬롯
/// </summary>
public class HintPlacementSlot : MonoBehaviour
{
    [SerializeField] private string slotId;     // 디버그용 슬롯 ID

    public string SlotId => slotId;
}
