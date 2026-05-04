using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 이번 판에 배치된 퍼즐 진행도를 Zone별로 집계하는 매니저.
/// 
/// 역할
/// - ZoneA / ZoneB의 Stage1 퍼즐 solved 상태를 개별 집계
/// - 해당 Zone에서 Stage1 퍼즐이 전부 solved 되면 StageManager에 보고
/// - ZoneA / ZoneB의 Stage2 퍼즐 solved 개수를 집계한다
/// - ZoneA / ZoneB의 Stage3 퍼즐 solved를 감지하고 StageManager에 보고한다
/// 
/// 주의
/// - 맵 변화와 화면 ON/OFF는 직접 처리하지 않는다.
/// - Stage2 화면 표시는 Stage2ScreenGate가 담당한다.
/// - Stage 상태는 StageManager가 Networked 값으로 관리한다.
/// </summary>
public class PuzzleProgressManager : MonoBehaviour
{
    public static PuzzleProgressManager Instance { get; private set; } // 전역 접근용 싱글톤

    [Header("Stage 승인 매니저 참조")]
    [SerializeField] private StageManager stageManager; // Stage 완료 보고를 받을 StageManager 참조

    [Header("디버그")]
    [SerializeField] private bool enableDebugLog = true; // 디버그 로그 출력 여부

    private readonly List<PuzzleInteractableBase> _zoneAStage1Puzzles = new(); // ZoneA Stage1 진행도 대상 퍼즐 목록
    private readonly List<PuzzleInteractableBase> _zoneBStage1Puzzles = new(); // ZoneB Stage1 진행도 대상 퍼즐 목록

    private readonly List<PuzzleInteractableBase> _zoneAStage2Puzzles = new(); // ZoneA Stage2 진행도 대상 퍼즐 목록
    private readonly List<PuzzleInteractableBase> _zoneBStage2Puzzles = new(); // ZoneB Stage2 진행도 대상 퍼즐 목록

    private readonly HashSet<PuzzleInteractableBase> _countedZoneAStage2Solved = new(); // ZoneA Stage2 solved 중복 집계 방지
    private readonly HashSet<PuzzleInteractableBase> _countedZoneBStage2Solved = new(); // ZoneB Stage2 solved 중복 집계 방지

    private PuzzleInteractableBase _zoneAStage3Puzzle; // ZoneA Stage3 퍼즐 본체
    private PuzzleInteractableBase _zoneBStage3Puzzle; // ZoneB Stage3 퍼즐 본체

    private bool _isRegistered;                    // 이번 판 등록 완료 여부
    private bool _hasReportedZoneAStage1Complete;  // ZoneA Stage1 완료 보고 여부
    private bool _hasReportedZoneBStage1Complete;  // ZoneB Stage1 완료 보고 여부
    private bool _hasReportedZoneAStage3Complete;  // ZoneA Stage3 완료 보고 여부
    private bool _hasReportedZoneBStage3Complete;  // ZoneB Stage3 완료 보고 여부

    private int _zoneAStage2SolvedCount;           // ZoneA Stage2 solved 개수
    private int _zoneBStage2SolvedCount;           // ZoneB Stage2 solved 개수

    public bool IsRegistered => _isRegistered; // 외부에서 등록 여부 확인용

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
    }

    /// <summary>
    /// 이번 판 진행도 등록 데이터를 Zone별로 초기화한다.
    /// Stage2 화면 목록 파라미터는 기존 PuzzleSpawnManager 호출부 호환용이며, 현재는 사용하지 않는다.
    /// </summary>
    public void InitializeRound(
        List<PuzzleInteractableBase> zoneAStage1Puzzles,
        List<PuzzleInteractableBase> zoneBStage1Puzzles,
        List<GameObject> zoneAStage2Screens,
        List<GameObject> zoneBStage2Screens)
    {
        ResetProgress(); // 이전 판 정보 정리

        RegisterPuzzles(_zoneAStage1Puzzles, zoneAStage1Puzzles); // ZoneA Stage1 퍼즐 등록
        RegisterPuzzles(_zoneBStage1Puzzles, zoneBStage1Puzzles); // ZoneB Stage1 퍼즐 등록

        SubscribePuzzleEvents(); // solved 이벤트 구독 시작

        _isRegistered = true;                     // 등록 완료 표시
        _hasReportedZoneAStage1Complete = false; // ZoneA Stage1 보고 플래그 초기화
        _hasReportedZoneBStage1Complete = false; // ZoneB Stage1 보고 플래그 초기화
        _hasReportedZoneAStage3Complete = false; // ZoneA Stage3 보고 플래그 초기화
        _hasReportedZoneBStage3Complete = false; // ZoneB Stage3 보고 플래그 초기화
        _zoneAStage2SolvedCount = 0;             // ZoneA Stage2 solved count 초기화
        _zoneBStage2SolvedCount = 0;             // ZoneB Stage2 solved count 초기화

        Log($"InitializeRound 완료 | ZoneA Stage1={_zoneAStage1Puzzles.Count}, ZoneB Stage1={_zoneBStage1Puzzles.Count}");

        CheckZoneStage1SolvedAndNotifyIfNeeded(Zone.ZoneA); // 등록 직후 보험 검사
        CheckZoneStage1SolvedAndNotifyIfNeeded(Zone.ZoneB); // 등록 직후 보험 검사
        RecalculateStage2SolvedCounts(); // 등록 직후 Stage2 보험 검사
        CheckZoneStage3SolvedAndNotifyIfNeeded(Zone.ZoneA); // 등록 직후 Stage3 보험 검사
        CheckZoneStage3SolvedAndNotifyIfNeeded(Zone.ZoneB); // 등록 직후 Stage3 보험 검사
    }

    /// <summary>
    /// Zone별 Stage2 퍼즐 목록을 별도로 등록한다.
    /// FinalCode 보상 판정용 solved count 집계에 사용된다.
    /// </summary>
    public void RegisterStage2Puzzles(
        List<PuzzleInteractableBase> zoneAStage2Puzzles,
        List<PuzzleInteractableBase> zoneBStage2Puzzles)
    {
        UnsubscribePuzzleList(_zoneAStage2Puzzles); // 기존 ZoneA Stage2 이벤트 해제
        UnsubscribePuzzleList(_zoneBStage2Puzzles); // 기존 ZoneB Stage2 이벤트 해제

        _zoneAStage2Puzzles.Clear(); // 기존 ZoneA Stage2 목록 초기화
        _zoneBStage2Puzzles.Clear(); // 기존 ZoneB Stage2 목록 초기화

        RegisterPuzzles(_zoneAStage2Puzzles, zoneAStage2Puzzles); // ZoneA Stage2 퍼즐 등록
        RegisterPuzzles(_zoneBStage2Puzzles, zoneBStage2Puzzles); // ZoneB Stage2 퍼즐 등록

        SubscribePuzzleList(_zoneAStage2Puzzles); // ZoneA Stage2 이벤트 구독
        SubscribePuzzleList(_zoneBStage2Puzzles); // ZoneB Stage2 이벤트 구독

        RecalculateStage2SolvedCounts(); // 등록 직후 solved count 재계산

        Log($"Stage2 퍼즐 등록 완료 | ZoneA Stage2={_zoneAStage2Puzzles.Count}, ZoneB Stage2={_zoneBStage2Puzzles.Count}");
    }

    /// <summary>
    /// Zone별 Stage3 퍼즐을 등록한다.
    /// Stage3는 Zone당 1개 고정 퍼즐을 기준으로 solved를 감지한다.
    /// </summary>
    public void RegisterStage3Puzzles(
        PuzzleInteractableBase zoneAStage3Puzzle,
        PuzzleInteractableBase zoneBStage3Puzzle)
    {
        if (_zoneAStage3Puzzle != null)
            _zoneAStage3Puzzle.Solved -= HandlePuzzleSolved; // 기존 ZoneA Stage3 이벤트 해제

        if (_zoneBStage3Puzzle != null)
            _zoneBStage3Puzzle.Solved -= HandlePuzzleSolved; // 기존 ZoneB Stage3 이벤트 해제

        _zoneAStage3Puzzle = zoneAStage3Puzzle; // 새 ZoneA Stage3 퍼즐 등록
        _zoneBStage3Puzzle = zoneBStage3Puzzle; // 새 ZoneB Stage3 퍼즐 등록

        if (_zoneAStage3Puzzle != null)
        {
            _zoneAStage3Puzzle.Solved -= HandlePuzzleSolved;
            _zoneAStage3Puzzle.Solved += HandlePuzzleSolved;
        }

        if (_zoneBStage3Puzzle != null)
        {
            _zoneBStage3Puzzle.Solved -= HandlePuzzleSolved;
            _zoneBStage3Puzzle.Solved += HandlePuzzleSolved;
        }

        CheckZoneStage3SolvedAndNotifyIfNeeded(Zone.ZoneA); // 등록 직후 보험 검사
        CheckZoneStage3SolvedAndNotifyIfNeeded(Zone.ZoneB); // 등록 직후 보험 검사

        Log("Stage3 퍼즐 등록 완료");
    }

    /// <summary>
    /// 현재 등록된 진행도 정보를 전부 초기화한다.
    /// </summary>
    public void ResetProgress()
    {
        UnsubscribePuzzleEvents(); // 기존 이벤트 구독 해제

        if (_zoneAStage3Puzzle != null)
            _zoneAStage3Puzzle.Solved -= HandlePuzzleSolved; // ZoneA Stage3 이벤트 해제

        if (_zoneBStage3Puzzle != null)
            _zoneBStage3Puzzle.Solved -= HandlePuzzleSolved; // ZoneB Stage3 이벤트 해제

        _zoneAStage1Puzzles.Clear(); // ZoneA Stage1 퍼즐 목록 초기화
        _zoneBStage1Puzzles.Clear(); // ZoneB Stage1 퍼즐 목록 초기화
        _zoneAStage2Puzzles.Clear(); // ZoneA Stage2 퍼즐 목록 초기화
        _zoneBStage2Puzzles.Clear(); // ZoneB Stage2 퍼즐 목록 초기화

        _countedZoneAStage2Solved.Clear(); // ZoneA solved 중복 집계 기록 초기화
        _countedZoneBStage2Solved.Clear(); // ZoneB solved 중복 집계 기록 초기화

        _zoneAStage3Puzzle = null; // ZoneA Stage3 참조 초기화
        _zoneBStage3Puzzle = null; // ZoneB Stage3 참조 초기화

        _isRegistered = false;                    // 등록 상태 초기화
        _hasReportedZoneAStage1Complete = false; // ZoneA Stage1 보고 상태 초기화
        _hasReportedZoneBStage1Complete = false; // ZoneB Stage1 보고 상태 초기화
        _hasReportedZoneAStage3Complete = false; // ZoneA Stage3 보고 상태 초기화
        _hasReportedZoneBStage3Complete = false; // ZoneB Stage3 보고 상태 초기화
        _zoneAStage2SolvedCount = 0;             // ZoneA Stage2 solved count 초기화
        _zoneBStage2SolvedCount = 0;             // ZoneB Stage2 solved count 초기화
    }

    public bool AreZoneAStage1PuzzlesSolved()
    {
        return AreAllSolved(_zoneAStage1Puzzles);
    }

    public bool AreZoneBStage1PuzzlesSolved()
    {
        return AreAllSolved(_zoneBStage1Puzzles);
    }

    public int GetSolvedStage2Count(Zone zone)
    {
        return zone == Zone.ZoneA ? _zoneAStage2SolvedCount : _zoneBStage2SolvedCount;
    }

    public int GetTotalStage2Count(Zone zone)
    {
        return zone == Zone.ZoneA ? _zoneAStage2Puzzles.Count : _zoneBStage2Puzzles.Count;
    }

    public bool IsZoneStage2FullySolved(Zone zone)
    {
        List<PuzzleInteractableBase> targetList = zone == Zone.ZoneA ? _zoneAStage2Puzzles : _zoneBStage2Puzzles;
        return AreAllSolved(targetList);
    }

    private void RegisterPuzzles(List<PuzzleInteractableBase> target, List<PuzzleInteractableBase> source)
    {
        if (source == null)
            return;

        for (int i = 0; i < source.Count; i++)
        {
            PuzzleInteractableBase puzzle = source[i];
            if (puzzle == null)
                continue;

            target.Add(puzzle);
        }
    }

    private void SubscribePuzzleEvents()
    {
        SubscribePuzzleList(_zoneAStage1Puzzles);
        SubscribePuzzleList(_zoneBStage1Puzzles);
        SubscribePuzzleList(_zoneAStage2Puzzles);
        SubscribePuzzleList(_zoneBStage2Puzzles);
    }

    private void UnsubscribePuzzleEvents()
    {
        UnsubscribePuzzleList(_zoneAStage1Puzzles);
        UnsubscribePuzzleList(_zoneBStage1Puzzles);
        UnsubscribePuzzleList(_zoneAStage2Puzzles);
        UnsubscribePuzzleList(_zoneBStage2Puzzles);
    }

    private void SubscribePuzzleList(List<PuzzleInteractableBase> puzzles)
    {
        for (int i = 0; i < puzzles.Count; i++)
        {
            PuzzleInteractableBase puzzle = puzzles[i];
            if (puzzle == null)
                continue;

            puzzle.Solved -= HandlePuzzleSolved;
            puzzle.Solved += HandlePuzzleSolved;
        }
    }

    private void UnsubscribePuzzleList(List<PuzzleInteractableBase> puzzles)
    {
        for (int i = 0; i < puzzles.Count; i++)
        {
            PuzzleInteractableBase puzzle = puzzles[i];
            if (puzzle == null)
                continue;

            puzzle.Solved -= HandlePuzzleSolved;
        }
    }

    private void HandlePuzzleSolved(PuzzleInteractableBase solvedPuzzle)
    {
        if (!_isRegistered || solvedPuzzle == null)
            return;

        Log($"퍼즐 해결 : {solvedPuzzle.name}");

        if (_zoneAStage1Puzzles.Contains(solvedPuzzle))
        {
            GameEventLogger.Instance?.AddPuzzleSolved(Zone.ZoneA);
            CheckZoneStage1SolvedAndNotifyIfNeeded(Zone.ZoneA);
            return;
        }

        if (_zoneBStage1Puzzles.Contains(solvedPuzzle))
        {
            GameEventLogger.Instance?.AddPuzzleSolved(Zone.ZoneB);
            CheckZoneStage1SolvedAndNotifyIfNeeded(Zone.ZoneB);
            return;
        }

        if (_zoneAStage2Puzzles.Contains(solvedPuzzle))
        {
            GameEventLogger.Instance?.AddPuzzleSolved(Zone.ZoneA);
            CountStage2SolvedIfNeeded(Zone.ZoneA, solvedPuzzle);
            return;
        }

        if (_zoneBStage2Puzzles.Contains(solvedPuzzle))
        {
            GameEventLogger.Instance?.AddPuzzleSolved(Zone.ZoneB);
            CountStage2SolvedIfNeeded(Zone.ZoneB, solvedPuzzle);
            return;
        }

        if (_zoneAStage3Puzzle == solvedPuzzle)
        {
            GameEventLogger.Instance?.AddPuzzleSolved(Zone.ZoneA);
            CheckZoneStage3SolvedAndNotifyIfNeeded(Zone.ZoneA);
            return;
        }

        if (_zoneBStage3Puzzle == solvedPuzzle)
        {
            GameEventLogger.Instance?.AddPuzzleSolved(Zone.ZoneB);
            CheckZoneStage3SolvedAndNotifyIfNeeded(Zone.ZoneB);
            return;
        }
    }

    private void CheckZoneStage1SolvedAndNotifyIfNeeded(Zone zone)
    {
        if (zone == Zone.ZoneA)
        {
            if (_hasReportedZoneAStage1Complete)
                return;

            if (!AreAllSolved(_zoneAStage1Puzzles))
                return;

            _hasReportedZoneAStage1Complete = true;
            NotifyZoneStage1Completed(Zone.ZoneA);
            return;
        }

        if (_hasReportedZoneBStage1Complete)
            return;

        if (!AreAllSolved(_zoneBStage1Puzzles))
            return;

        _hasReportedZoneBStage1Complete = true;
        NotifyZoneStage1Completed(Zone.ZoneB);
    }

    private void CheckZoneStage3SolvedAndNotifyIfNeeded(Zone zone)
    {
        if (zone == Zone.ZoneA)
        {
            if (_hasReportedZoneAStage3Complete)
                return;

            if (_zoneAStage3Puzzle == null || !_zoneAStage3Puzzle.IsNetworkReady || !_zoneAStage3Puzzle.IsSolved)
                return;

            _hasReportedZoneAStage3Complete = true;
            NotifyZoneStage3Completed(Zone.ZoneA);
            return;
        }

        if (_hasReportedZoneBStage3Complete)
            return;

        if (_zoneBStage3Puzzle == null || !_zoneBStage3Puzzle.IsNetworkReady || !_zoneBStage3Puzzle.IsSolved)
            return;

        _hasReportedZoneBStage3Complete = true;
        NotifyZoneStage3Completed(Zone.ZoneB);
    }

    private void CountStage2SolvedIfNeeded(Zone zone, PuzzleInteractableBase solvedPuzzle)
    {
        if (zone == Zone.ZoneA)
        {
            if (_countedZoneAStage2Solved.Contains(solvedPuzzle))
                return;

            _countedZoneAStage2Solved.Add(solvedPuzzle);
            _zoneAStage2SolvedCount = _countedZoneAStage2Solved.Count;
            Log($"ZoneA Stage2 solved count 갱신 | {_zoneAStage2SolvedCount}/{_zoneAStage2Puzzles.Count}");
            return;
        }

        if (_countedZoneBStage2Solved.Contains(solvedPuzzle))
            return;

        _countedZoneBStage2Solved.Add(solvedPuzzle);
        _zoneBStage2SolvedCount = _countedZoneBStage2Solved.Count;
        Log($"ZoneB Stage2 solved count 갱신 | {_zoneBStage2SolvedCount}/{_zoneBStage2Puzzles.Count}");
    }

    private void RecalculateStage2SolvedCounts()
    {
        _countedZoneAStage2Solved.Clear();
        _countedZoneBStage2Solved.Clear();

        for (int i = 0; i < _zoneAStage2Puzzles.Count; i++)
        {
            PuzzleInteractableBase puzzle = _zoneAStage2Puzzles[i];
            if (puzzle == null || !puzzle.IsNetworkReady || !puzzle.IsSolved)
                continue;

            _countedZoneAStage2Solved.Add(puzzle);
        }

        for (int i = 0; i < _zoneBStage2Puzzles.Count; i++)
        {
            PuzzleInteractableBase puzzle = _zoneBStage2Puzzles[i];
            if (puzzle == null || !puzzle.IsNetworkReady || !puzzle.IsSolved)
                continue;

            _countedZoneBStage2Solved.Add(puzzle);
        }

        _zoneAStage2SolvedCount = _countedZoneAStage2Solved.Count;
        _zoneBStage2SolvedCount = _countedZoneBStage2Solved.Count;

        Log($"Stage2 solved count 재계산 | ZoneA={_zoneAStage2SolvedCount}/{_zoneAStage2Puzzles.Count}, ZoneB={_zoneBStage2SolvedCount}/{_zoneBStage2Puzzles.Count}");
    }

    private bool AreAllSolved(List<PuzzleInteractableBase> puzzles)
    {
        if (!_isRegistered)
            return false;

        if (puzzles == null || puzzles.Count == 0)
            return false;

        for (int i = 0; i < puzzles.Count; i++)
        {
            PuzzleInteractableBase puzzle = puzzles[i];

            if (puzzle == null)
                return false;

            if (!puzzle.IsNetworkReady)
                return false;

            if (!puzzle.IsSolved)
                return false;
        }

        return true;
    }

    private void NotifyZoneStage1Completed(Zone zone)
    {
        if (stageManager == null)
            stageManager = StageManager.Instance;

        if (stageManager == null)
        {
            LogWarning($"StageManager 참조가 없어 {zone} Stage1 완료를 보고할 수 없습니다.");
            return;
        }

        Log($"{zone} Stage1 완료 보고");
        stageManager.ReportZoneStage1Completed(zone);
    }

    private void NotifyZoneStage3Completed(Zone zone)
    {
        if (stageManager == null)
            stageManager = StageManager.Instance;

        if (stageManager == null)
        {
            LogWarning($"StageManager 참조가 없어 {zone} Stage3 완료를 보고할 수 없습니다.");
            return;
        }

        Log($"{zone} Stage3 완료 보고");
        stageManager.ReportZoneStage3Completed(zone);
    }

    private void Log(string message)
    {
        if (!enableDebugLog)
            return;

        Debug.Log($"[PuzzleProgressManager] {message}", this);
    }

    private void LogWarning(string message)
    {
        if (!enableDebugLog)
            return;

        Debug.LogWarning($"[PuzzleProgressManager] {message}", this);
    }
}