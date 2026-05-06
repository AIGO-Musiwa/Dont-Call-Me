using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Zone 하나에 속한 전체 배치 가능한 방 목록과
/// Stage3 퍼즐 고정 위치를 보관하는 카탈로그.
/// 
/// 역할
/// - Zone별 Room 목록 제공
/// - 현재 사용 가능한 Room 목록 제공
/// - 층별 Room 필터 제공
/// - Stage3 퍼즐 고정 슬롯 제공
/// - 자식 RoomPlacementGroup 자동 수집
/// </summary>
public class ZonePlacementCatalog : MonoBehaviour
{
    [Header("Zone 정보")]
    [SerializeField] private Zone zone; // 이 카탈로그가 속한 Zone

    [Header("Room 목록")]
    [SerializeField] private List<RoomPlacementGroup> rooms = new(); // 이 Zone에 속한 모든 Room 목록

    [Header("자동 수집")]
    [SerializeField] private bool autoCollectRooms = true; // 자식 방 자동 수집 여부

    [Header("Stage3 퍼즐 슬롯")]
    [FormerlySerializedAs("stage3FixedPuzzleSlot")]
    [SerializeField] private PlacementSlotMeta stage3PuzzleSlot; // Stage3 퍼즐 고정 스폰 슬롯

    [Header("디버그")]
    [SerializeField] private bool enableDebugLog = false; // 디버그 로그 출력 여부

    public Zone Zone => zone; // Zone 외부 읽기용

    private void OnValidate()
    {
        if (!autoCollectRooms)
            return;

        CollectRoomsFromChildren(); // 인스펙터 값 변경 시 자식 방 자동 수집
    }

    /// <summary>
    /// 자식/하위 자식의 RoomPlacementGroup을 자동 수집한다.
    /// </summary>
    [ContextMenu("Collect Rooms From Children")]
    public void CollectRoomsFromChildren()
    {
        RoomPlacementGroup[] foundRooms = GetComponentsInChildren<RoomPlacementGroup>(); // 활성 자식 방 수집

        rooms.Clear();

        for (int i = 0; i < foundRooms.Length; i++)
        {
            RoomPlacementGroup room = foundRooms[i];
            if (room == null)
                continue;

            if (room.gameObject == gameObject)
                continue; // 자기 자신 제외 방어

            rooms.Add(room);
        }

        SortRooms(); // 층 + 방ID 기준 정렬
        Log($"자식 방 자동 수집 완료 | Zone={zone} | RoomCount={rooms.Count}");
    }

    /// <summary>
    /// Room 목록이 비어 있으면 한 번 더 자동 수집한다.
    /// </summary>
    public void EnsureRoomsCollected()
    {
        bool hasAnyValidRoom = false;

        for (int i = 0; i < rooms.Count; i++)
        {
            if (rooms[i] != null)
            {
                hasAnyValidRoom = true;
                break;
            }
        }

        if (hasAnyValidRoom)
            return;

        CollectRoomsFromChildren();
    }

    /// <summary>
    /// 스폰 전에 이 카탈로그의 Room/Slot 목록을 준비한다.
    /// </summary>
    public void PrepareCatalog()
    {
        EnsureRoomsCollected(); // 방 목록이 비어 있으면 자동 수집

        for (int i = 0; i < rooms.Count; i++)
        {
            RoomPlacementGroup room = rooms[i];
            if (room == null)
                continue;

            room.EnsureSlotsCollected(); // 각 방의 슬롯 목록도 보정
        }

        SortRooms();             // 정렬 보정
        ClearRuntimeOccupancy(); // 이전 점유 상태 초기화
    }

    /// <summary>
    /// 이 카탈로그가 정상적인지 검사한다.
    /// </summary>
    public bool ValidateCatalog()
    {
        EnsureRoomsCollected(); // 검사 전에 방 목록 보정

        if (rooms.Count == 0)
        {
            LogWarning($"Room이 하나도 없습니다. | Zone={zone}");
            return false;
        }

        if (stage3PuzzleSlot == null)
        {
            LogWarning($"Stage3 퍼즐 슬롯이 비어 있습니다. | Zone={zone}");
            return false;
        }

        if (stage3PuzzleSlot.UsageType != PlacementSlotUsageType.Stage3Puzzle)
        {
            LogWarning(
                $"Stage3 퍼즐 슬롯의 UsageType이 Stage3Puzzle이 아닙니다. " +
                $"| Zone={zone} | SlotId={stage3PuzzleSlot.SlotId} | CurrentType={stage3PuzzleSlot.UsageType}");
            return false;
        }

        bool hasAnyPuzzleRoom = false;
        bool hasAnyHintRoom = false;

        for (int i = 0; i < rooms.Count; i++)
        {
            RoomPlacementGroup room = rooms[i];
            if (room == null)
                continue;

            if (room.Zone != zone)
            {
                LogWarning($"Room의 Zone 값이 카탈로그와 다릅니다. | CatalogZone={zone} | RoomId={room.RoomId} | RoomZone={room.Zone}");
                return false;
            }

            if (!room.ValidateRoom())
            {
                LogWarning($"유효하지 않은 Room이 있습니다. | RoomId={room.RoomId}");
                return false;
            }

            if (room.HasAvailablePuzzleSlot())
                hasAnyPuzzleRoom = true;

            if (room.HasAvailableHintSlot())
                hasAnyHintRoom = true;
        }

        if (!hasAnyPuzzleRoom)
        {
            LogWarning($"퍼즐 배치 가능한 Room이 없습니다. | Zone={zone}");
            return false;
        }

        if (!hasAnyHintRoom)
        {
            LogWarning($"힌트 배치 가능한 Room이 없습니다. | Zone={zone}");
            return false;
        }

        return true;
    }

    /// <summary>
    /// 층수 + roomId 기준으로 Room 목록을 정렬한다.
    /// </summary>
    private void SortRooms()
    {
        rooms.Sort((a, b) =>
        {
            if (a == null && b == null) return 0;
            if (a == null) return 1;
            if (b == null) return -1;

            int floorCompare = a.Floor.CompareTo(b.Floor);
            if (floorCompare != 0)
                return floorCompare;

            return string.Compare(a.RoomId, b.RoomId, System.StringComparison.Ordinal);
        });
    }

    /// <summary>
    /// 이 Zone의 전체 Room 목록을 반환한다.
    /// </summary>
    public List<RoomPlacementGroup> GetAllRooms()
    {
        List<RoomPlacementGroup> result = new();

        for (int i = 0; i < rooms.Count; i++)
        {
            RoomPlacementGroup room = rooms[i];
            if (room == null)
                continue;

            result.Add(room);
        }

        return result;
    }

    /// <summary>
    /// 현재 점유되지 않은 Room 목록을 반환한다.
    /// </summary>
    public List<RoomPlacementGroup> GetAvailableRooms()
    {
        List<RoomPlacementGroup> result = new();

        for (int i = 0; i < rooms.Count; i++)
        {
            RoomPlacementGroup room = rooms[i];
            if (room == null)
                continue;

            if (room.IsOccupied())
                continue;

            result.Add(room);
        }

        return result;
    }

    /// <summary>
    /// 현재 점유되지 않았고, 특정 층에 속한 Room 목록을 반환한다.
    /// </summary>
    public List<RoomPlacementGroup> GetAvailableRoomsByFloor(int floor)
    {
        List<RoomPlacementGroup> result = new();

        for (int i = 0; i < rooms.Count; i++)
        {
            RoomPlacementGroup room = rooms[i];
            if (room == null)
                continue;

            if (room.IsOccupied())
                continue;

            if (room.Floor != floor)
                continue;

            result.Add(room);
        }

        return result;
    }

    /// <summary>
    /// 현재 점유되지 않았고, 퍼즐 배치 가능한 Room 목록을 반환한다.
    /// </summary>
    public List<RoomPlacementGroup> GetAvailablePuzzleRooms()
    {
        List<RoomPlacementGroup> result = new();

        for (int i = 0; i < rooms.Count; i++)
        {
            RoomPlacementGroup room = rooms[i];
            if (room == null)
                continue;

            if (room.IsOccupied())
                continue;

            if (!room.HasAvailablePuzzleSlot())
                continue;

            result.Add(room);
        }

        return result;
    }

    /// <summary>
    /// 현재 점유되지 않았고, 힌트 배치 가능한 Room 목록을 반환한다.
    /// </summary>
    public List<RoomPlacementGroup> GetAvailableHintRooms()
    {
        List<RoomPlacementGroup> result = new();

        for (int i = 0; i < rooms.Count; i++)
        {
            RoomPlacementGroup room = rooms[i];
            if (room == null)
                continue;

            if (room.IsOccupied())
                continue;

            if (!room.HasAvailableHintSlot())
                continue;

            result.Add(room);
        }

        return result;
    }

    /// <summary>
    /// 특정 층에서 퍼즐 배치 가능한 Room 목록을 반환한다.
    /// </summary>
    public List<RoomPlacementGroup> GetAvailablePuzzleRoomsByFloor(int floor)
    {
        List<RoomPlacementGroup> result = new();

        for (int i = 0; i < rooms.Count; i++)
        {
            RoomPlacementGroup room = rooms[i];
            if (room == null)
                continue;

            if (room.IsOccupied())
                continue;

            if (room.Floor != floor)
                continue;

            if (!room.HasAvailablePuzzleSlot())
                continue;

            result.Add(room);
        }

        return result;
    }

    /// <summary>
    /// 특정 층에서 힌트 배치 가능한 Room 목록을 반환한다.
    /// </summary>
    public List<RoomPlacementGroup> GetAvailableHintRoomsByFloor(int floor)
    {
        List<RoomPlacementGroup> result = new();

        for (int i = 0; i < rooms.Count; i++)
        {
            RoomPlacementGroup room = rooms[i];
            if (room == null)
                continue;

            if (room.IsOccupied())
                continue;

            if (room.Floor != floor)
                continue;

            if (!room.HasAvailableHintSlot())
                continue;

            result.Add(room);
        }

        return result;
    }

    /// <summary>
    /// Stage3 퍼즐 고정 슬롯을 반환한다.
    /// </summary>
    public PlacementSlotMeta GetStage3PuzzleSlot()
    {
        return stage3PuzzleSlot;
    }

    /// <summary>
    /// 이 Zone의 모든 Room 런타임 점유 상태를 초기화한다.
    /// Stage3 퍼즐 슬롯은 Room 하위가 아닐 수도 있으므로 별도로 초기화한다.
    /// </summary>
    public void ClearRuntimeOccupancy()
    {
        for (int i = 0; i < rooms.Count; i++)
        {
            RoomPlacementGroup room = rooms[i];
            if (room == null)
                continue;

            room.ClearRuntimeOccupancy();
        }

        if (stage3PuzzleSlot != null)
            stage3PuzzleSlot.ClearOccupied();

        Log("Zone 전체 Room 점유 상태 초기화 완료");
    }

    private void Log(string message)
    {
        if (!enableDebugLog)
            return;

        Debug.Log($"[ZonePlacementCatalog] {message}", this);
    }

    private void LogWarning(string message)
    {
        if (!enableDebugLog)
            return;

        Debug.LogWarning($"[ZonePlacementCatalog] {message}", this);
    }
}