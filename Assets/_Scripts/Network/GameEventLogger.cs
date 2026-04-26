using Fusion;
using UnityEngine;

public class GameEventLogger : NetworkBehaviour
{
    public static GameEventLogger Instance { get; private set; }

    [Networked] public int PuzzlesSolvedZoneA { get; set; }
    [Networked] public int PuzzlesSolvedZoneB { get; set; }
    [Networked] public int RadioUsedZoneA { get; set; }
    [Networked] public int RadioUsedZoneB { get; set; }

    [Networked, Capacity(30)]
    public NetworkLinkedList<GameEventEntry> NetLog { get; }

    private float sessionStartTime;

    public override void Spawned()
    {
        Instance = this;
        sessionStartTime = Time.time;
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        Instance = null;
    }

    // ── 외부에서 호출 ─────────────────────────────────────────────────

    // 포획, 구출, 사망, 탈출할 때, 로그 추가
    public void LogCaptured(string nickname, int slotIndex) => AddLog(GameEventType.Captured, nickname, slotIndex);
    public void LogRescued(string nickname, int slotIndex) => AddLog(GameEventType.Rescued, nickname, slotIndex);
    public void LogDead(string nickname, int slotIndex) => AddLog(GameEventType.Dead, nickname, slotIndex);
    public void LogEscaped(string nickname, int slotIndex) => AddLog(GameEventType.Escaped, nickname, slotIndex);

    // 푼 퍼즐 개수++
    public void AddPuzzleSolved(Zone zone)
    {
        if (!Runner.IsServer) return;
        if (zone == Zone.ZoneA) PuzzlesSolvedZoneA++;
        else PuzzlesSolvedZoneB++;
    }

    // 라디오 사용 횟수++
    public void AddRadioUsed(Zone zone)
    {
        if (!Runner.IsServer) return;
        if (zone == Zone.ZoneA) RadioUsedZoneA++;
        else RadioUsedZoneB++;
    }

    // ── 내부 ─────────────────────────────────────────────────────────

    // 이벤트 로그 추가
    private void AddLog(GameEventType type, string nickname, int slotIndex)
    {
        if (!Runner.IsServer) return;

        // Capacity 초과 방지
        if (NetLog.Count >= 30)
        {
            Debug.LogWarning("[GameEventLogger] 타임라인 로그 Capacity(30) 초과. 기록 생략.");
            return;
        }

        NetLog.Add(new GameEventEntry
        {
            EventType = type,
            Nickname = nickname,
            SlotIndex = slotIndex,
            Timestamp = Time.time - sessionStartTime
        });
    }
}
