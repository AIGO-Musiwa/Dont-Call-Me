using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 이번 판에 배치된 퍼즐 진행도를 Zone별로 집계하는 매니저.
/// 
/// 역할
/// - ZoneA / ZoneB의 Stage1 퍼즐 solved 상태를 개별 집계
/// - 해당 Zone에서 Stage1 퍼즐이 전부 solved 되면 StageManager에 보고
/// - StageManager가 Stage2 해금을 승인하면 해당 Zone의 Stage2 화면만 켠다
/// - ZoneA / ZoneB의 Stage2 퍼즐 solved 개수를 집계한다
/// 
/// 주의
/// - 맵 변화는 하지 않는다
/// - Stage 상태를 직접 바꾸지 않는다
/// - StageManager의 승인을 받아서 화면만 반응한다
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

    private readonly List<GameObject> _zoneAStage2Screens = new(); // ZoneA Stage2 화면 목록
    private readonly List<GameObject> _zoneBStage2Screens = new(); // ZoneB Stage2 화면 목록

    private readonly HashSet<PuzzleInteractableBase> _countedZoneAStage2Solved = new(); // ZoneA Stage2 solved 중복 집계 방지
    private readonly HashSet<PuzzleInteractableBase> _countedZoneBStage2Solved = new(); // ZoneB Stage2 solved 중복 집계 방지

    private bool _isRegistered;                    // 이번 판 등록 완료 여부
    private bool _isZoneAStage2Unlocked;           // ZoneA Stage2 화면 해금 여부
    private bool _isZoneBStage2Unlocked;           // ZoneB Stage2 화면 해금 여부
    private bool _hasReportedZoneAStage1Complete;  // ZoneA Stage1 완료 보고 여부
    private bool _hasReportedZoneBStage1Complete;  // ZoneB Stage1 완료 보고 여부

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
    /// </summary>
    public void InitializeRound(
        List<PuzzleInteractableBase> zoneAStage1Puzzles,
        List<PuzzleInteractableBase> zoneBStage1Puzzles,
        List<GameObject> zoneAStage2Screens,
        List<GameObject> zoneBStage2Screens)
    {
        ResetProgress(); // 이전 판 정보 정리

        RegisterStage1Puzzles(_zoneAStage1Puzzles, zoneAStage1Puzzles); // ZoneA Stage1 퍼즐 등록
        RegisterStage1Puzzles(_zoneBStage1Puzzles, zoneBStage1Puzzles); // ZoneB Stage1 퍼즐 등록

        RegisterStage2Screens(_zoneAStage2Screens, zoneAStage2Screens); // ZoneA Stage2 화면 등록
        RegisterStage2Screens(_zoneBStage2Screens, zoneBStage2Screens); // ZoneB Stage2 화면 등록

        SetZoneStage2ScreensActive(Zone.ZoneA, false); // 시작 시 ZoneA Stage2 화면 OFF
        SetZoneStage2ScreensActive(Zone.ZoneB, false); // 시작 시 ZoneB Stage2 화면 OFF

        SubscribePuzzleEvents(); // solved 이벤트 구독 시작

        _isRegistered = true;                    // 등록 완료 표시
        _isZoneAStage2Unlocked = false;         // ZoneA Stage2 해금 초기화
        _isZoneBStage2Unlocked = false;         // ZoneB Stage2 해금 초기화
        _hasReportedZoneAStage1Complete = false; // ZoneA 보고 플래그 초기화
        _hasReportedZoneBStage1Complete = false; // ZoneB 보고 플래그 초기화
        _zoneAStage2SolvedCount = 0;            // ZoneA Stage2 solved count 초기화
        _zoneBStage2SolvedCount = 0;            // ZoneB Stage2 solved count 초기화

        Log($"InitializeRound 완료 | ZoneA Stage1={_zoneAStage1Puzzles.Count}, ZoneB Stage1={_zoneBStage1Puzzles.Count}, ZoneA Stage2Screen={_zoneAStage2Screens.Count}, ZoneB Stage2Screen={_zoneBStage2Screens.Count}");

        CheckZoneStage1SolvedAndNotifyIfNeeded(Zone.ZoneA); // 등록 직후 보험 검사
        CheckZoneStage1SolvedAndNotifyIfNeeded(Zone.ZoneB); // 등록 직후 보험 검사
        RecalculateStage2SolvedCounts(); // 등록 직후 Stage2 보험 검사
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

        RegisterStage1Puzzles(_zoneAStage2Puzzles, zoneAStage2Puzzles); // ZoneA Stage2 퍼즐 등록
        RegisterStage1Puzzles(_zoneBStage2Puzzles, zoneBStage2Puzzles); // ZoneB Stage2 퍼즐 등록

        SubscribePuzzleList(_zoneAStage2Puzzles); // ZoneA Stage2 이벤트 구독
        SubscribePuzzleList(_zoneBStage2Puzzles); // ZoneB Stage2 이벤트 구독

        RecalculateStage2SolvedCounts(); // 등록 직후 solved count 재계산

        Log($"Stage2 퍼즐 등록 완료 | ZoneA Stage2={_zoneAStage2Puzzles.Count}, ZoneB Stage2={_zoneBStage2Puzzles.Count}");
    }

    /// <summary>
    /// 현재 등록된 진행도 정보를 전부 초기화한다.
    /// </summary>
    public void ResetProgress()
    {
        UnsubscribePuzzleEvents(); // 기존 이벤트 구독 해제

        _zoneAStage1Puzzles.Clear(); // ZoneA Stage1 퍼즐 목록 초기화
        _zoneBStage1Puzzles.Clear(); // ZoneB Stage1 퍼즐 목록 초기화
        _zoneAStage2Puzzles.Clear(); // ZoneA Stage2 퍼즐 목록 초기화
        _zoneBStage2Puzzles.Clear(); // ZoneB Stage2 퍼즐 목록 초기화

        _zoneAStage2Screens.Clear(); // ZoneA Stage2 화면 목록 초기화
        _zoneBStage2Screens.Clear(); // ZoneB Stage2 화면 목록 초기화

        _countedZoneAStage2Solved.Clear(); // ZoneA solved 중복 집계 기록 초기화
        _countedZoneBStage2Solved.Clear(); // ZoneB solved 중복 집계 기록 초기화

        _isRegistered = false;                    // 등록 상태 초기화
        _isZoneAStage2Unlocked = false;          // ZoneA Stage2 해금 상태 초기화
        _isZoneBStage2Unlocked = false;          // ZoneB Stage2 해금 상태 초기화
        _hasReportedZoneAStage1Complete = false; // ZoneA 보고 상태 초기화
        _hasReportedZoneBStage1Complete = false; // ZoneB 보고 상태 초기화
        _zoneAStage2SolvedCount = 0;             // ZoneA Stage2 solved count 초기화
        _zoneBStage2SolvedCount = 0;             // ZoneB Stage2 solved count 초기화
    }

    /// <summary>
    /// ZoneA Stage1 퍼즐이 전부 해결되었는지 반환한다.
    /// </summary>
    public bool AreZoneAStage1PuzzlesSolved()
    {
        return AreAllSolved(_zoneAStage1Puzzles);
    }

    /// <summary>
    /// ZoneB Stage1 퍼즐이 전부 해결되었는지 반환한다.
    /// </summary>
    public bool AreZoneBStage1PuzzlesSolved()
    {
        return AreAllSolved(_zoneBStage1Puzzles);
    }

    /// <summary>
    /// 특정 Zone의 Stage2 solved 개수를 반환한다.
    /// </summary>
    public int GetSolvedStage2Count(Zone zone)
    {
        return zone == Zone.ZoneA ? _zoneAStage2SolvedCount : _zoneBStage2SolvedCount;
    }

    /// <summary>
    /// 특정 Zone의 Stage2 전체 퍼즐 개수를 반환한다.
    /// </summary>
    public int GetTotalStage2Count(Zone zone)
    {
        return zone == Zone.ZoneA ? _zoneAStage2Puzzles.Count : _zoneBStage2Puzzles.Count;
    }

    /// <summary>
    /// 특정 Zone의 Stage2 퍼즐이 전부 해결되었는지 반환한다.
    /// </summary>
    public bool IsZoneStage2FullySolved(Zone zone)
    {
        List<PuzzleInteractableBase> targetList = zone == Zone.ZoneA ? _zoneAStage2Puzzles : _zoneBStage2Puzzles; // Zone별 Stage2 목록 선택
        return AreAllSolved(targetList);
    }

    /// <summary>
    /// StageManager가 특정 Zone의 Stage2 해금을 승인했을 때 호출한다.
    /// 해당 Zone의 Stage2 화면만 켠다.
    /// </summary>
    public void HandleZoneStage2Unlocked(Zone zone)
    {
        if (!_isRegistered)
            return;

        if (zone == Zone.ZoneA)
        {
            if (_isZoneAStage2Unlocked)
                return;

            _isZoneAStage2Unlocked = true; // ZoneA 해금 상태 기록
            SetZoneStage2ScreensActive(Zone.ZoneA, true); // ZoneA Stage2 화면 ON

            Log("ZoneA Stage2 화면 ON");
            return;
        }

        if (_isZoneBStage2Unlocked)
            return;

        _isZoneBStage2Unlocked = true; // ZoneB 해금 상태 기록
        SetZoneStage2ScreensActive(Zone.ZoneB, true); // ZoneB Stage2 화면 ON

        Log("ZoneB Stage2 화면 ON");
    }

    /// <summary>
    /// 디버그/테스트용으로 양쪽 Zone의 Stage2 화면을 모두 켠다.
    /// </summary>
    public void HandleAllZonesStage2Unlocked()
    {
        HandleZoneStage2Unlocked(Zone.ZoneA);
        HandleZoneStage2Unlocked(Zone.ZoneB);
    }

    /// <summary>
    /// 특정 리스트에 Stage1 또는 Stage2 퍼즐을 등록한다.
    /// </summary>
    private void RegisterStage1Puzzles(List<PuzzleInteractableBase> target, List<PuzzleInteractableBase> source)
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

    /// <summary>
    /// 특정 리스트에 Stage2 화면을 등록한다.
    /// </summary>
    private void RegisterStage2Screens(List<GameObject> target, List<GameObject> source)
    {
        if (source == null)
            return;

        for (int i = 0; i < source.Count; i++)
        {
            GameObject screen = source[i];
            if (screen == null)
                continue;

            target.Add(screen);
        }
    }

    /// <summary>
    /// 등록된 모든 Stage1/Stage2 퍼즐의 solved 이벤트를 구독한다.
    /// </summary>
    private void SubscribePuzzleEvents()
    {
        SubscribePuzzleList(_zoneAStage1Puzzles);
        SubscribePuzzleList(_zoneBStage1Puzzles);
        SubscribePuzzleList(_zoneAStage2Puzzles);
        SubscribePuzzleList(_zoneBStage2Puzzles);
    }

    /// <summary>
    /// 등록된 모든 Stage1/Stage2 퍼즐의 solved 이벤트 구독을 해제한다.
    /// </summary>
    private void UnsubscribePuzzleEvents()
    {
        UnsubscribePuzzleList(_zoneAStage1Puzzles);
        UnsubscribePuzzleList(_zoneBStage1Puzzles);
        UnsubscribePuzzleList(_zoneAStage2Puzzles);
        UnsubscribePuzzleList(_zoneBStage2Puzzles);
    }

    /// <summary>
    /// 특정 퍼즐 리스트의 solved 이벤트를 구독한다.
    /// </summary>
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

    /// <summary>
    /// 특정 퍼즐 리스트의 solved 이벤트 구독을 해제한다.
    /// </summary>
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

    /// <summary>
    /// 퍼즐 하나가 solved 되었을 때 호출된다.
    /// 어느 Zone 소속 퍼즐인지 판별해서 해당 Zone 완료 여부만 검사한다.
    /// </summary>
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
    }

    /// <summary>
    /// 특정 Zone의 Stage1 퍼즐이 전부 solved 되었으면 StageManager에 완료 보고를 보낸다.
    /// </summary>
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

    /// <summary>
    /// Stage2 solved count를 중복 없이 집계한다.
    /// </summary>
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

    /// <summary>
    /// 현재 등록된 Stage2 퍼즐 목록을 기준으로 solved count를 다시 계산한다.
    /// </summary>
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

    /// <summary>
    /// 특정 Zone의 퍼즐 리스트가 전부 solved 되었는지 검사한다.
    /// </summary>
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

    /// <summary>
    /// 특정 Zone의 Stage2 화면들을 일괄 활성/비활성 처리한다.
    /// </summary>
    private void SetZoneStage2ScreensActive(Zone zone, bool active)
    {
        List<GameObject> targetScreens = zone == Zone.ZoneA ? _zoneAStage2Screens : _zoneBStage2Screens;

        for (int i = 0; i < targetScreens.Count; i++)
        {
            GameObject screen = targetScreens[i];
            if (screen == null)
                continue;

            screen.SetActive(active);
        }
    }

    /// <summary>
    /// 특정 Zone Stage1 완료를 StageManager에 보고하는 연결 지점.
    /// </summary>
    private void NotifyZoneStage1Completed(Zone zone)
    {
        if (stageManager == null)
        {
            LogWarning($"StageManager 참조가 없어 {zone} Stage1 완료를 보고할 수 없습니다.");
            return;
        }

        Log($"{zone} Stage1 완료 보고");
        stageManager.ReportZoneStage1Completed(zone);
    }

    /// <summary>
    /// 일반 디버그 로그 출력.
    /// </summary>
    private void Log(string message)
    {
        if (!enableDebugLog)
            return;

        Debug.Log($"[PuzzleProgressManager] {message}", this);
    }

    /// <summary>
    /// 경고 디버그 로그 출력.
    /// </summary>
    private void LogWarning(string message)
    {
        if (!enableDebugLog)
            return;

        Debug.LogWarning($"[PuzzleProgressManager] {message}", this);
    }
}