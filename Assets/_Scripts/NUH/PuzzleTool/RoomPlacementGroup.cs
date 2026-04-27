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
/// </summary>
public class RoomPlacementGroup : MonoBehaviour
{
    [Header("방 정보")]
    [SerializeField] private string roomId;                            // 방 식별용 ID
    [SerializeField] private Zone zone;                                // 이 방이 속한 Zone
    [SerializeField] private int floor = 1;                            // 이 방이 위치한 층수

    [Header("슬롯 목록")]
    [SerializeField] private List<PlacementSlotMeta> slots = new();    // 이 방 안의 배치 슬롯 목록

    [Header("디버그")]
    [SerializeField] private bool enableDebugLog = false;              // 디버그 로그 출력 여부

    private bool _occupiedAtRuntime;                                   // 이 방이 런타임에서 이미 사용되었는지 여부

    public string RoomId => roomId;                                    // 방 ID 외부 읽기용
    public Zone Zone => zone;                                          // 방 Zone 외부 읽기용
    public int Floor => floor;                                         // 방 층수 외부 읽기용
    public IReadOnlyList<PlacementSlotMeta> Slots => slots;            // 슬롯 목록 외부 읽기용

    /// <summary>
    /// 현재 이 방이 점유되었는지 반환한다.
    /// </summary>
    public bool IsOccupied()
    {
        return _occupiedAtRuntime;
    }

    /// <summary>
    /// 현재 이 방에 퍼즐 배치 가능한 슬롯이 하나라도 있는지 반환한다.
    /// </summary>
    public bool HasAvailablePuzzleSlot()
    {
        if (_occupiedAtRuntime)
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
    /// </summary>
    public bool HasAvailableHintSlot()
    {
        if (_occupiedAtRuntime)
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
    public List<PlacementSlotMeta> GetAvailablePuzzleSlots()
    {
        List<PlacementSlotMeta> result = new();

        if (_occupiedAtRuntime)
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
    public List<PlacementSlotMeta> GetAvailableHintSlots()
    {
        List<PlacementSlotMeta> result = new();

        if (_occupiedAtRuntime)
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
    public PlacementSlotMeta GetRandomAvailablePuzzleSlot(SeedRandom rng)
    {
        if (rng == null)
            return null;

        List<PlacementSlotMeta> candidates = GetAvailablePuzzleSlots();
        if (candidates.Count == 0)
            return null;

        int randomIndex = rng.NextInt(0, candidates.Count);
        return candidates[randomIndex];
    }

    /// <summary>
    /// 현재 방에서 힌트 배치 가능한 슬롯 중 1개를 시드 랜덤으로 반환한다.
    /// 규칙:
    /// - HintOnly 슬롯 우선
    /// - 없으면 PuzzleOrHint 슬롯 사용
    /// </summary>
    public PlacementSlotMeta GetRandomAvailableHintSlotPreferHintOnly(SeedRandom rng)
    {
        if (rng == null)
            return null;

        if (_occupiedAtRuntime)
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
}