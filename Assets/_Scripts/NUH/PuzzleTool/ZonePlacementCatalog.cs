using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Zone 하나에 속한 전체 배치 가능한 방 목록과
/// Stage3 고정 퍼즐 위치를 보관하는 카탈로그.
/// 
/// 역할
/// - Zone별 Room 목록 제공
/// - 현재 사용 가능한 Room 목록 제공
/// - 층별 Room 필터 제공
/// - Stage3 고정 슬롯 제공
/// </summary>
public class ZonePlacementCatalog : MonoBehaviour
{
    [Header("Zone 정보")]
    [SerializeField] private Zone zone;                                      // 이 카탈로그가 속한 Zone

    [Header("Room 목록")]
    [SerializeField] private List<RoomPlacementGroup> rooms = new();         // 이 Zone에 속한 모든 Room 목록

    [Header("Stage3 고정 슬롯")]
    [SerializeField] private PlacementSlotMeta stage3FixedPuzzleSlot;        // Stage3 퍼즐 고정 스폰 슬롯

    [Header("디버그")]
    [SerializeField] private bool enableDebugLog = false;                    // 디버그 로그 출력 여부

    public Zone Zone => zone;                                                // Zone 외부 읽기용

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
    public PlacementSlotMeta GetStage3FixedPuzzleSlot()
    {
        return stage3FixedPuzzleSlot;
    }

    /// <summary>
    /// 이 Zone의 모든 Room 런타임 점유 상태를 초기화한다.
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

        Log("Zone 전체 Room 점유 상태 초기화 완료");
    }

    private void Log(string message)
    {
        if (!enableDebugLog)
            return;

        Debug.Log($"[ZonePlacementCatalog] {message}", this);
    }
}