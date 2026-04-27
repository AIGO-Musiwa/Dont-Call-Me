using UnityEngine;

/// <summary>
/// 방 내부의 실제 배치 가능 위치 1개를 표현하는 공통 슬롯 메타.
/// 
/// 역할
/// - 이 슬롯이 힌트 전용인지, 퍼즐/힌트 공용인지 보관
/// - 실제 스폰 기준 위치(anchor)를 제공
/// - 런타임 점유 여부를 관리
/// </summary>
public class PlacementSlotMeta : MonoBehaviour
{
    [Header("기본 정보")]
    [SerializeField] private string slotId;                         // 슬롯 식별용 ID
    [SerializeField] private PlacementSlotUsageType usageType;      // 슬롯 사용 타입

    [Header("스폰 기준 위치")]
    [SerializeField] private Transform anchor;                      // 실제 스폰 위치 기준점, 비어 있으면 자기 transform 사용

    [Header("디버그")]
    [SerializeField] private bool enableDebugLog = false;           // 디버그 로그 출력 여부

    private bool _occupiedAtRuntime;                                // 런타임 점유 여부

    public string SlotId => slotId;                                 // 슬롯 ID 외부 읽기용
    public PlacementSlotUsageType UsageType => usageType;           // 슬롯 타입 외부 읽기용
    public Transform Anchor => anchor != null ? anchor : transform; // anchor 우선, 비어 있으면 자기 transform 반환

    /// <summary>
    /// 현재 슬롯이 점유되었는지 반환한다.
    /// </summary>
    public bool IsOccupied()
    {
        return _occupiedAtRuntime;
    }

    /// <summary>
    /// 현재 슬롯에 퍼즐 배치가 가능한지 반환한다.
    /// </summary>
    public bool CanPlacePuzzle()
    {
        if (_occupiedAtRuntime)
            return false;

        return usageType == PlacementSlotUsageType.PuzzleOrHint;
    }

    /// <summary>
    /// 현재 슬롯에 힌트 배치가 가능한지 반환한다.
    /// </summary>
    public bool CanPlaceHint()
    {
        if (_occupiedAtRuntime)
            return false;

        return usageType == PlacementSlotUsageType.HintOnly ||
               usageType == PlacementSlotUsageType.PuzzleOrHint;
    }

    /// <summary>
    /// 런타임 점유 상태를 true로 설정한다.
    /// </summary>
    public void MarkOccupied()
    {
        _occupiedAtRuntime = true;
        Log($"슬롯 점유됨 | SlotId={slotId}");
    }

    /// <summary>
    /// 런타임 점유 상태를 false로 초기화한다.
    /// </summary>
    public void ClearOccupied()
    {
        _occupiedAtRuntime = false;
        Log($"슬롯 점유 해제 | SlotId={slotId}");
    }

    private void Log(string message)
    {
        if (!enableDebugLog)
            return;

        Debug.Log($"[PlacementSlotMeta] {message}", this);
    }
}