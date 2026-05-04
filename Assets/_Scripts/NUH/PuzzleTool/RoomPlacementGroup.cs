using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 배치의 최소 단위인 방 1개 전체를 표현하는 그룹.
/// 
/// 역할
/// - roomId / zone / floor 보관
/// - 이 방 안의 배치 슬롯 목록 보관
/// - 방 점유 여부 판단
/// - 퍼즐 배치 가능한 슬롯 / 힌트 배치 가능한 슬롯 제공
/// - 자식 PlacementSlotMeta 자동 수집
/// 
/// 변경점
/// - 힌트 2차 배치를 위해 방 점유 상태를 무시하고 남은 슬롯만 검사할 수 있는 옵션 추가
/// - 슬롯 단위 점유는 PlacementSlotMeta가 계속 담당
/// </summary>
public class RoomPlacementGroup : MonoBehaviour
{
    [Header("방 정보")]
    [SerializeField] private string roomId;                            // 방 식별용 ID
    [SerializeField] private Zone zone;                                // 이 방이 속한 Zone
    [SerializeField] private int floor = 1;                            // 이 방이 위치한 층수

    [Header("슬롯 목록")]
    [SerializeField] private List<PlacementSlotMeta> slots = new();    // 이 방 안의 배치 슬롯 목록

    [Header("자동 수집")]
    [SerializeField] private bool autoCollectSlots = true;             // 자식 슬롯 자동 수집 여부

    [Header("디버그")]
    [SerializeField] private bool enableDebugLog = false;              // 디버그 로그 출력 여부

    private bool _occupiedAtRuntime;                                   // 이 방이 런타임에서 이미 사용되었는지 여부

    public string RoomId => roomId;                                    // 방 ID 외부 읽기용
    public Zone Zone => zone;                                          // 방 Zone 외부 읽기용
    public int Floor => floor;                                         // 방 층수 외부 읽기용
    public IReadOnlyList<PlacementSlotMeta> Slots => slots;            // 슬롯 목록 외부 읽기용

    private void OnValidate()
    {
        if (!autoCollectSlots)
            return;

        CollectSlotsFromChildren(); // 인스펙터 값 변경 시 자식 슬롯 자동 수집
    }

    /// <summary>
    /// 자식/하위 자식의 PlacementSlotMeta를 자동 수집한다.
    /// </summary>
    [ContextMenu("Collect Slots From Children")]
    public void CollectSlotsFromChildren()
    {
        PlacementSlotMeta[] foundSlots = GetComponentsInChildren<PlacementSlotMeta>(); // 활성 자식 슬롯 수집

        slots.Clear();

        for (int i = 0; i < foundSlots.Length; i++)
        {
            PlacementSlotMeta slot = foundSlots[i];
            if (slot == null)
                continue;

            if (slot.gameObject == gameObject)
                continue; // 자기 자신 제외 방어

            slots.Add(slot);
        }

        SortSlots(); // slotId 기준으로 정렬
        Log($"자식 슬롯 자동 수집 완료 | RoomId={roomId} | SlotCount={slots.Count}");
    }

    /// <summary>
    /// 슬롯 목록이 비어 있으면 한 번 더 자동 수집한다.
    /// </summary>
    public void EnsureSlotsCollected()
    {
        bool hasAnyValidSlot = false;

        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] != null)
            {
                hasAnyValidSlot = true;
                break;
            }
        }

        if (hasAnyValidSlot)
            return;

        CollectSlotsFromChildren();
    }

    /// <summary>
    /// 이 방 데이터가 정상적인지 검사한다.
    /// </summary>
    public bool ValidateRoom()
    {
        EnsureSlotsCollected(); // 검사 전에 슬롯 목록 보정

        if (string.IsNullOrWhiteSpace(roomId))
        {
            LogWarning("roomId가 비어 있습니다.");
            return false;
        }

        if (floor < 1 || floor > 3)
        {
            LogWarning($"floor 값이 잘못되었습니다. | RoomId={roomId} | Floor={floor}");
            return false;
        }

        if (slots.Count == 0)
        {
            LogWarning($"배치 슬롯이 하나도 없습니다. | RoomId={roomId}");
            return false;
        }

        bool hasValidHintOrPuzzleSlot = false;

        for (int i = 0; i < slots.Count; i++)
        {
            PlacementSlotMeta slot = slots[i];
            if (slot == null)
                continue;

            hasValidHintOrPuzzleSlot = true;
            break;
        }

        if (!hasValidHintOrPuzzleSlot)
        {
            LogWarning($"유효한 슬롯 참조가 없습니다. | RoomId={roomId}");
            return false;
        }

        return true;
    }

    /// <summary>
    /// slotId 기준으로 슬롯 목록을 정렬한다.
    /// </summary>
    private void SortSlots()
    {
        slots.Sort((a, b) =>
        {
            if (a == null && b == null) return 0;
            if (a == null) return 1;
            if (b == null) return -1;

            return string.Compare(a.SlotId, b.SlotId, System.StringComparison.Ordinal);
        });
    }

    /// <summary>
    /// 현재 이 방이 점유되었는지 반환한다.
    /// </summary>
    public bool IsOccupied()
    {
        return _occupiedAtRuntime;
    }

    /// <summary>
    /// 현재 이 방에 퍼즐 배치 가능한 슬롯이 하나라도 있는지 반환한다.
    /// 
    /// ignoreRoomOccupancy가 true면 방 점유 여부는 무시하고,
    /// 슬롯 단위 점유 여부만 기준으로 검사한다.
    /// </summary>
    public bool HasAvailablePuzzleSlot(bool ignoreRoomOccupancy = false)
    {
        if (_occupiedAtRuntime && !ignoreRoomOccupancy)
            return false;

        for (int i = 0; i < slots.Count; i++)
        {
            PlacementSlotMeta slot = slots[i];
            if (slot == null)
                continue;

            if (slot.CanPlacePuzzle())
                return true;
        }

        return false;
    }

    /// <summary>
    /// 현재 이 방에 힌트 배치 가능한 슬롯이 하나라도 있는지 반환한다.
    /// 
    /// ignoreRoomOccupancy가 true면 방 점유 여부는 무시하고,
    /// 슬롯 단위 점유 여부만 기준으로 검사한다.
    /// </summary>
    public bool HasAvailableHintSlot(bool ignoreRoomOccupancy = false)
    {
        if (_occupiedAtRuntime && !ignoreRoomOccupancy)
            return false;

        for (int i = 0; i < slots.Count; i++)
        {
            PlacementSlotMeta slot = slots[i];
            if (slot == null)
                continue;

            if (slot.CanPlaceHint())
                return true;
        }

        return false;
    }

    /// <summary>
    /// 현재 방에서 퍼즐 배치 가능한 슬롯 목록을 반환한다.
    /// </summary>
    public List<PlacementSlotMeta> GetAvailablePuzzleSlots(bool ignoreRoomOccupancy = false)
    {
        List<PlacementSlotMeta> result = new();

        if (_occupiedAtRuntime && !ignoreRoomOccupancy)
            return result;

        for (int i = 0; i < slots.Count; i++)
        {
            PlacementSlotMeta slot = slots[i];
            if (slot == null)
                continue;

            if (!slot.CanPlacePuzzle())
                continue;

            result.Add(slot);
        }

        return result;
    }

    /// <summary>
    /// 현재 방에서 힌트 배치 가능한 슬롯 목록을 반환한다.
    /// </summary>
    public List<PlacementSlotMeta> GetAvailableHintSlots(bool ignoreRoomOccupancy = false)
    {
        List<PlacementSlotMeta> result = new();

        if (_occupiedAtRuntime && !ignoreRoomOccupancy)
            return result;

        for (int i = 0; i < slots.Count; i++)
        {
            PlacementSlotMeta slot = slots[i];
            if (slot == null)
                continue;

            if (!slot.CanPlaceHint())
                continue;

            result.Add(slot);
        }

        return result;
    }

    /// <summary>
    /// 현재 방에서 퍼즐 배치 가능한 슬롯 중 1개를 시드 랜덤으로 반환한다.
    /// </summary>
    public PlacementSlotMeta GetRandomAvailablePuzzleSlot(SeedRandom rng, bool ignoreRoomOccupancy = false)
    {
        if (rng == null)
            return null;

        List<PlacementSlotMeta> candidates = GetAvailablePuzzleSlots(ignoreRoomOccupancy);
        if (candidates.Count == 0)
            return null;

        int randomIndex = rng.NextInt(0, candidates.Count);
        return candidates[randomIndex];
    }

    /// <summary>
    /// 현재 방에서 힌트 배치 가능한 슬롯 중 1개를 시드 랜덤으로 반환한다.
    /// 
    /// 규칙:
    /// - HintOnly 슬롯 우선
    /// - 없으면 PuzzleOrHint 슬롯 사용
    /// - ignoreRoomOccupancy가 true면 방이 이미 사용되었어도 남은 슬롯을 후보로 본다.
    /// </summary>
    public PlacementSlotMeta GetRandomAvailableHintSlotPreferHintOnly(
        SeedRandom rng,
        bool ignoreRoomOccupancy = false)
    {
        if (rng == null)
            return null;

        if (_occupiedAtRuntime && !ignoreRoomOccupancy)
            return null;

        List<PlacementSlotMeta> hintOnlyCandidates = new();
        List<PlacementSlotMeta> fallbackCandidates = new();

        for (int i = 0; i < slots.Count; i++)
        {
            PlacementSlotMeta slot = slots[i];
            if (slot == null)
                continue;

            if (!slot.CanPlaceHint())
                continue;

            if (slot.UsageType == PlacementSlotUsageType.HintOnly)
                hintOnlyCandidates.Add(slot);
            else
                fallbackCandidates.Add(slot);
        }

        if (hintOnlyCandidates.Count > 0)
        {
            int randomIndex = rng.NextInt(0, hintOnlyCandidates.Count);
            return hintOnlyCandidates[randomIndex];
        }

        if (fallbackCandidates.Count > 0)
        {
            int randomIndex = rng.NextInt(0, fallbackCandidates.Count);
            return fallbackCandidates[randomIndex];
        }

        return null;
    }

    /// <summary>
    /// 방과 선택된 슬롯을 점유 상태로 만든다.
    /// 
    /// 이미 점유된 방에 힌트를 추가 배치하는 경우에도,
    /// 선택된 슬롯만 추가 점유 처리된다.
    /// </summary>
    public void MarkRoomOccupied(PlacementSlotMeta chosenSlot)
    {
        _occupiedAtRuntime = true;

        if (chosenSlot != null)
            chosenSlot.MarkOccupied();

        Log($"방 점유됨 | RoomId={roomId} | Floor={floor}");
    }

    /// <summary>
    /// 방과 내부 슬롯들의 런타임 점유 상태를 초기화한다.
    /// </summary>
    public void ClearRuntimeOccupancy()
    {
        _occupiedAtRuntime = false;

        for (int i = 0; i < slots.Count; i++)
        {
            PlacementSlotMeta slot = slots[i];
            if (slot == null)
                continue;

            slot.ClearOccupied();
        }

        Log($"방 점유 상태 초기화 | RoomId={roomId}");
    }

    private void Log(string message)
    {
        if (!enableDebugLog)
            return;

        Debug.Log($"[RoomPlacementGroup] {message}", this);
    }

    private void LogWarning(string message)
    {
        if (!enableDebugLog)
            return;

        Debug.LogWarning($"[RoomPlacementGroup] {message}", this);
    }
}