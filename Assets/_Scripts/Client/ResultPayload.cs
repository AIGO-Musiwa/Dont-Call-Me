using Fusion;
using System.Collections.Generic;

public static class ResultPayload
{
    public static PendingResult Pending { get; set; } = null;
}

public class PendingResult
{
    // ── 세션 기본 정보 ────────────────────────────────────
    public bool IsClear { get; set; }
    public float Duration { get; set; }

    // ── 통계 ──────────────────────────────────────────────
    public int PuzzlesSolvedZoneA { get; set; }
    public int PuzzlesSolvedZoneB { get; set; }
    public int RadioUsedZoneA { get; set; }
    public int RadioUsedZoneB { get; set; }

    // ── 타임라인 ──────────────────────────────────────────
    public List<GameEventEntry> TimelineLog { get; set; } = new();

    // ── 플레이어 최종 상태 ─────────────────────────────────
    public List<PlayerResultData> PlayerResults { get; set; } = new();
}

public class PlayerResultData
{
    public string Nickname {  get; set; }
    public int SlotIndex { get; set; }
    public PlayerState FinalState { get; set; }
    public bool IsLocalPlayer { get; set; }
    public Zone PlayerZone { get; set; }
}

public struct GameEventEntry : INetworkStruct
{
    public GameEventType EventType;
    public NetworkString<_32> Nickname;
    public int SlotIndex;
    public float Timestamp;
}