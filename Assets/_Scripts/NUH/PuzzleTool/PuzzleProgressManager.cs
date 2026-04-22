using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 이번 판에 배치된 퍼즐 진행도를 Zone별로 집계하는 매니저.
/// 
/// 역할
/// - ZoneA / ZoneB의 Stage1 퍼즐 solved 상태를 개별 집계
/// - 해당 Zone에서 Stage1 퍼즐이 전부 solved 되면 StageManager에 보고
/// - StageManager가 Stage2 해금을 승인하면 해당 Zone의 Stage2 화면만 켠다
/// 
/// 주의
/// - 맵 변화는 하지 않는다
/// - Stage 상태를 직접 바꾸지 않는다
/// - StageManager의 승인을 받아서 화면만 반응한다
/// </summary>
public class PuzzleProgressManager : MonoBehaviour
{
    [Header("Stage 승인 매니저 참조")]
    [SerializeField] private StageManager stageManager; // Stage 완료 보고를 받을 StageManager 참조

    [Header("디버그")]
    [SerializeField] private bool enableDebugLog = true; // 디버그 로그 출력 여부

    private readonly List<PuzzleInteractableBase> _zoneAStage1Puzzles = new(); // ZoneA Stage1 진행도 대상 퍼즐 목록
    private readonly List<PuzzleInteractableBase> _zoneBStage1Puzzles = new(); // ZoneB Stage1 진행도 대상 퍼즐 목록

    private readonly List<GameObject> _zoneAStage2Screens = new(); // ZoneA Stage2 화면 목록
    private readonly List<GameObject> _zoneBStage2Screens = new(); // ZoneB Stage2 화면 목록

    private bool _isRegistered;                      // 이번 판 등록 완료 여부
    private bool _isZoneAStage2Unlocked;            // ZoneA Stage2 화면 해금 여부
    private bool _isZoneBStage2Unlocked;            // ZoneB Stage2 화면 해금 여부
    private bool _hasReportedZoneAStage1Complete;   // ZoneA Stage1 완료 보고 여부
    private bool _hasReportedZoneBStage1Complete;   // ZoneB Stage1 완료 보고 여부

    public bool IsRegistered => _isRegistered; // 외부에서 등록 여부 확인용

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
        _isZoneAStage2Unlocked = false;          // ZoneA Stage2 해금 초기화
        _isZoneBStage2Unlocked = false;          // ZoneB Stage2 해금 초기화
        _hasReportedZoneAStage1Complete = false; // ZoneA 보고 플래그 초기화
        _hasReportedZoneBStage1Complete = false; // ZoneB 보고 플래그 초기화

        Log($"InitializeRound 완료 | ZoneA Stage1={_zoneAStage1Puzzles.Count}, ZoneB Stage1={_zoneBStage1Puzzles.Count}, ZoneA Stage2Screen={_zoneAStage2Screens.Count}, ZoneB Stage2Screen={_zoneBStage2Screens.Count}");

        CheckZoneStage1SolvedAndNotifyIfNeeded(Zone.ZoneA); // 등록 직후 보험 검사
        CheckZoneStage1SolvedAndNotifyIfNeeded(Zone.ZoneB); // 등록 직후 보험 검사
    }

    /// <summary>
    /// 현재 등록된 진행도 정보를 전부 초기화한다.
    /// </summary>
    public void ResetProgress()
    {
        UnsubscribePuzzleEvents(); // 기존 이벤트 구독 해제

        _zoneAStage1Puzzles.Clear(); // ZoneA Stage1 퍼즐 목록 초기화
        _zoneBStage1Puzzles.Clear(); // ZoneB Stage1 퍼즐 목록 초기화

        _zoneAStage2Screens.Clear(); // ZoneA Stage2 화면 목록 초기화
        _zoneBStage2Screens.Clear(); // ZoneB Stage2 화면 목록 초기화

        _isRegistered = false;                    // 등록 상태 초기화
        _isZoneAStage2Unlocked = false;          // ZoneA Stage2 해금 상태 초기화
        _isZoneBStage2Unlocked = false;          // ZoneB Stage2 해금 상태 초기화
        _hasReportedZoneAStage1Complete = false; // ZoneA 보고 상태 초기화
        _hasReportedZoneBStage1Complete = false; // ZoneB 보고 상태 초기화
    }

    /// <summary>
    /// ZoneA Stage1 퍼즐이 전부 해결되었는지 반환한다.
    /// </summary>
    public bool AreZoneAStage1PuzzlesSolved()
    {
        return AreAllSolved(_zoneAStage1Puzzles); // ZoneA Stage1 전부 solved 여부 반환
    }

    /// <summary>
    /// ZoneB Stage1 퍼즐이 전부 해결되었는지 반환한다.
    /// </summary>
    public bool AreZoneBStage1PuzzlesSolved()
    {
        return AreAllSolved(_zoneBStage1Puzzles); // ZoneB Stage1 전부 solved 여부 반환
    }

    /// <summary>
    /// StageManager가 특정 Zone의 Stage2 해금을 승인했을 때 호출한다.
    /// 해당 Zone의 Stage2 화면만 켠다.
    /// </summary>
    public void HandleZoneStage2Unlocked(Zone zone)
    {
        if (!_isRegistered)
            return; // 등록 전이면 무시

        if (zone == Zone.ZoneA)
        {
            if (_isZoneAStage2Unlocked)
                return; // 이미 해금된 ZoneA는 중복 처리 방지

            _isZoneAStage2Unlocked = true;           // ZoneA 해금 상태 기록
            SetZoneStage2ScreensActive(Zone.ZoneA, true); // ZoneA Stage2 화면 ON

            Log("ZoneA Stage2 화면 ON");
            return;
        }

        if (_isZoneBStage2Unlocked)
            return; // 이미 해금된 ZoneB는 중복 처리 방지

        _isZoneBStage2Unlocked = true;              // ZoneB 해금 상태 기록
        SetZoneStage2ScreensActive(Zone.ZoneB, true); // ZoneB Stage2 화면 ON

        Log("ZoneB Stage2 화면 ON");
    }

    /// <summary>
    /// 디버그/테스트용으로 양쪽 Zone의 Stage2 화면을 모두 켠다.
    /// </summary>
    public void HandleAllZonesStage2Unlocked()
    {
        HandleZoneStage2Unlocked(Zone.ZoneA); // ZoneA 강제 해금
        HandleZoneStage2Unlocked(Zone.ZoneB); // ZoneB 강제 해금
    }

    /// <summary>
    /// 특정 리스트에 Stage1 퍼즐을 등록한다.
    /// </summary>
    private void RegisterStage1Puzzles(List<PuzzleInteractableBase> target, List<PuzzleInteractableBase> source)
    {
        if (source == null)
            return; // 원본 리스트 없으면 종료

        for (int i = 0; i < source.Count; i++)
        {
            PuzzleInteractableBase puzzle = source[i]; // 현재 퍼즐 참조
            if (puzzle == null)
                continue; // null 퍼즐은 스킵

            target.Add(puzzle); // 등록 대상 목록에 추가
        }
    }

    /// <summary>
    /// 특정 리스트에 Stage2 화면을 등록한다.
    /// </summary>
    private void RegisterStage2Screens(List<GameObject> target, List<GameObject> source)
    {
        if (source == null)
            return; // 원본 리스트 없으면 종료

        for (int i = 0; i < source.Count; i++)
        {
            GameObject screen = source[i]; // 현재 화면 루트 참조
            if (screen == null)
                continue; // null 화면은 스킵

            target.Add(screen); // 등록 대상 목록에 추가
        }
    }

    /// <summary>
    /// 등록된 모든 Stage1 퍼즐의 solved 이벤트를 구독한다.
    /// </summary>
    private void SubscribePuzzleEvents()
    {
        SubscribePuzzleList(_zoneAStage1Puzzles); // ZoneA 퍼즐 이벤트 구독
        SubscribePuzzleList(_zoneBStage1Puzzles); // ZoneB 퍼즐 이벤트 구독
    }

    /// <summary>
    /// 등록된 모든 Stage1 퍼즐의 solved 이벤트 구독을 해제한다.
    /// </summary>
    private void UnsubscribePuzzleEvents()
    {
        UnsubscribePuzzleList(_zoneAStage1Puzzles); // ZoneA 퍼즐 이벤트 해제
        UnsubscribePuzzleList(_zoneBStage1Puzzles); // ZoneB 퍼즐 이벤트 해제
    }

    /// <summary>
    /// 특정 퍼즐 리스트의 solved 이벤트를 구독한다.
    /// </summary>
    private void SubscribePuzzleList(List<PuzzleInteractableBase> puzzles)
    {
        for (int i = 0; i < puzzles.Count; i++)
        {
            PuzzleInteractableBase puzzle = puzzles[i]; // 현재 퍼즐 참조
            if (puzzle == null)
                continue; // null 퍼즐은 스킵

            puzzle.Solved -= HandlePuzzleSolved; // 중복 구독 방지용 제거
            puzzle.Solved += HandlePuzzleSolved; // solved 이벤트 구독
        }
    }

    /// <summary>
    /// 특정 퍼즐 리스트의 solved 이벤트 구독을 해제한다.
    /// </summary>
    private void UnsubscribePuzzleList(List<PuzzleInteractableBase> puzzles)
    {
        for (int i = 0; i < puzzles.Count; i++)
        {
            PuzzleInteractableBase puzzle = puzzles[i]; // 현재 퍼즐 참조
            if (puzzle == null)
                continue; // null 퍼즐은 스킵

            puzzle.Solved -= HandlePuzzleSolved; // solved 이벤트 구독 해제
        }
    }

    /// <summary>
    /// 퍼즐 하나가 solved 되었을 때 호출된다.
    /// 어느 Zone 소속 퍼즐인지 판별해서 해당 Zone 완료 여부만 검사한다.
    /// </summary>
    private void HandlePuzzleSolved(PuzzleInteractableBase solvedPuzzle)
    {
        if (!_isRegistered || solvedPuzzle == null)
            return; // 등록 전이거나 퍼즐 참조 없으면 종료

        Log($"퍼즐 해결 감지 | {solvedPuzzle.name}");

        if (_zoneAStage1Puzzles.Contains(solvedPuzzle))
        {
            CheckZoneStage1SolvedAndNotifyIfNeeded(Zone.ZoneA); // ZoneA 퍼즐이면 ZoneA만 검사
            return;
        }

        if (_zoneBStage1Puzzles.Contains(solvedPuzzle))
            CheckZoneStage1SolvedAndNotifyIfNeeded(Zone.ZoneB); // ZoneB 퍼즐이면 ZoneB만 검사
    }

    /// <summary>
    /// 특정 Zone의 Stage1 퍼즐이 전부 solved 되었으면 StageManager에 완료 보고를 보낸다.
    /// </summary>
    private void CheckZoneStage1SolvedAndNotifyIfNeeded(Zone zone)
    {
        if (zone == Zone.ZoneA)
        {
            if (_hasReportedZoneAStage1Complete)
                return; // 이미 보고한 ZoneA는 중복 보고 방지

            if (!AreAllSolved(_zoneAStage1Puzzles))
                return; // 아직 전부 solved가 아니면 종료

            _hasReportedZoneAStage1Complete = true; // ZoneA 보고 완료 표시
            NotifyZoneStage1Completed(Zone.ZoneA);  // StageManager에 ZoneA 완료 보고
            return;
        }

        if (_hasReportedZoneBStage1Complete)
            return; // 이미 보고한 ZoneB는 중복 보고 방지

        if (!AreAllSolved(_zoneBStage1Puzzles))
            return; // 아직 전부 solved가 아니면 종료

        _hasReportedZoneBStage1Complete = true; // ZoneB 보고 완료 표시
        NotifyZoneStage1Completed(Zone.ZoneB);  // StageManager에 ZoneB 완료 보고
    }

    /// <summary>
    /// 특정 Zone의 퍼즐 리스트가 전부 solved 되었는지 검사한다.
    /// </summary>
    private bool AreAllSolved(List<PuzzleInteractableBase> puzzles)
    {
        if (!_isRegistered)
            return false; // 등록 전이면 false

        if (puzzles == null || puzzles.Count == 0)
            return false; // 퍼즐 목록 비어 있으면 false

        for (int i = 0; i < puzzles.Count; i++)
        {
            PuzzleInteractableBase puzzle = puzzles[i]; // 현재 퍼즐 참조

            if (puzzle == null)
                return false; // null 퍼즐 있으면 false

            if (!puzzle.IsNetworkReady)
                return false; // 네트워크 준비 안 됐으면 false

            if (!puzzle.IsSolved)
                return false; // 하나라도 미해결이면 false
        }

        return true; // 전부 solved 상태
    }

    /// <summary>
    /// 특정 Zone의 Stage2 화면들을 일괄 활성/비활성 처리한다.
    /// </summary>
    private void SetZoneStage2ScreensActive(Zone zone, bool active)
    {
        List<GameObject> targetScreens = zone == Zone.ZoneA
            ? _zoneAStage2Screens
            : _zoneBStage2Screens; // Zone별 대상 화면 목록 선택

        for (int i = 0; i < targetScreens.Count; i++)
        {
            GameObject screen = targetScreens[i]; // 현재 화면 루트 참조
            if (screen == null)
                continue; // null 화면은 스킵

            screen.SetActive(active); // 활성/비활성 적용
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

        stageManager.ReportZoneStage1Completed(zone); // StageManager에 해당 Zone 완료 보고
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