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
/// - 현재 Stage 상태를 직접 바꾸지 않는다
/// - StageManager의 승인을 받아서 화면만 반응한다
/// </summary>
public class PuzzleProgressManager : MonoBehaviour
{
    // ZoneA에 속한 1단계 진행도 대상 퍼즐 목록
    private readonly List<PuzzleInteractableBase> _zoneAStage1Puzzles = new();

    // ZoneB에 속한 1단계 진행도 대상 퍼즐 목록
    private readonly List<PuzzleInteractableBase> _zoneBStage1Puzzles = new();

    // ZoneA 2단계 화면 목록
    private readonly List<GameObject> _zoneAStage2Screens = new();

    // ZoneB 2단계 화면 목록
    private readonly List<GameObject> _zoneBStage2Screens = new();

    // 이번 판 퍼즐 목록 등록이 끝났는지 여부
    private bool _isRegistered;

    // ZoneA Stage2 화면이 이미 해금되었는지 여부
    private bool _isZoneAStage2Unlocked;

    // ZoneB Stage2 화면이 이미 해금되었는지 여부
    private bool _isZoneBStage2Unlocked;

    // ZoneA Stage1 완료를 StageManager에 이미 보고했는지 여부
    private bool _hasReportedZoneAStage1Complete;

    // ZoneB Stage1 완료를 StageManager에 이미 보고했는지 여부
    private bool _hasReportedZoneBStage1Complete;

    // 외부에서 등록 여부를 확인할 때 사용
    public bool IsRegistered => _isRegistered;

    /// <summary>
    /// 이번 판 진행도 등록 데이터를 Zone별로 초기화한다.
    /// </summary>
    public void InitializeRound(
        List<PuzzleInteractableBase> zoneAStage1Puzzles,
        List<PuzzleInteractableBase> zoneBStage1Puzzles,
        List<GameObject> zoneAStage2Screens,
        List<GameObject> zoneBStage2Screens)
    {
        // 이전 판 정보 제거
        ResetProgress();

        // ZoneA Stage1 퍼즐 등록
        RegisterStage1Puzzles(_zoneAStage1Puzzles, zoneAStage1Puzzles);

        // ZoneB Stage1 퍼즐 등록
        RegisterStage1Puzzles(_zoneBStage1Puzzles, zoneBStage1Puzzles);

        // ZoneA Stage2 화면 등록
        RegisterStage2Screens(_zoneAStage2Screens, zoneAStage2Screens);

        // ZoneB Stage2 화면 등록
        RegisterStage2Screens(_zoneBStage2Screens, zoneBStage2Screens);

        // 초기에는 양쪽 Stage2 화면을 모두 꺼둔다.
        SetZoneStage2ScreensActive(Zone.ZoneA, false);
        SetZoneStage2ScreensActive(Zone.ZoneB, false);

        // solved 이벤트 구독
        SubscribePuzzleEvents();

        _isRegistered = true;
        _isZoneAStage2Unlocked = false;
        _isZoneBStage2Unlocked = false;
        _hasReportedZoneAStage1Complete = false;
        _hasReportedZoneBStage1Complete = false;

        Debug.Log($"[PuzzleProgressManager] InitializeRound 완료 | ZoneA Stage1={_zoneAStage1Puzzles.Count}, ZoneB Stage1={_zoneBStage1Puzzles.Count}, ZoneA Stage2Screen={_zoneAStage2Screens.Count}, ZoneB Stage2Screen={_zoneBStage2Screens.Count}");

        // 등록 시점에 이미 solved 상태일 수도 있으므로 보험 검사
        CheckZoneStage1SolvedAndNotifyIfNeeded(Zone.ZoneA);
        CheckZoneStage1SolvedAndNotifyIfNeeded(Zone.ZoneB);
    }

    /// <summary>
    /// 현재 등록된 진행도 정보를 전부 초기화한다.
    /// </summary>
    public void ResetProgress()
    {
        UnsubscribePuzzleEvents();

        _zoneAStage1Puzzles.Clear();
        _zoneBStage1Puzzles.Clear();

        _zoneAStage2Screens.Clear();
        _zoneBStage2Screens.Clear();

        _isRegistered = false;
        _isZoneAStage2Unlocked = false;
        _isZoneBStage2Unlocked = false;
        _hasReportedZoneAStage1Complete = false;
        _hasReportedZoneBStage1Complete = false;
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
    /// StageManager가 ZoneA Stage2 해금을 승인했을 때 호출할 함수.
    /// 해당 Zone Stage2 화면만 켠다.
    /// </summary>
    public void HandleZoneStage2Unlocked(Zone zone)
    {
        if (!_isRegistered)
            return;

        if (zone == Zone.ZoneA)
        {
            if (_isZoneAStage2Unlocked)
                return;

            _isZoneAStage2Unlocked = true;
            SetZoneStage2ScreensActive(Zone.ZoneA, true);
            Debug.Log("[PuzzleProgressManager] ZoneA Stage2 화면 On");
            return;
        }

        if (_isZoneBStage2Unlocked)
            return;

        _isZoneBStage2Unlocked = true;
        SetZoneStage2ScreensActive(Zone.ZoneB, true);
        Debug.Log("[PuzzleProgressManager] ZoneB Stage2 화면 On");
    }

    /// <summary>
    /// StageManager가 테스트용으로 양쪽 Zone을 모두 Stage2 해금 처리할 때 사용할 보조 함수.
    /// 이 함수는 맵 변화가 아니라 화면 반응만 처리한다.
    /// </summary>
    public void HandleAllZonesStage2Unlocked()
    {
        HandleZoneStage2Unlocked(Zone.ZoneA);
        HandleZoneStage2Unlocked(Zone.ZoneB);
    }

    /// <summary>
    /// 특정 리스트에 Stage1 퍼즐을 등록한다.
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
    /// 등록된 모든 Stage1 퍼즐의 solved 이벤트를 구독한다.
    /// </summary>
    private void SubscribePuzzleEvents()
    {
        SubscribePuzzleList(_zoneAStage1Puzzles);
        SubscribePuzzleList(_zoneBStage1Puzzles);
    }

    /// <summary>
    /// 등록된 모든 Stage1 퍼즐의 solved 이벤트 구독을 해제한다.
    /// </summary>
    private void UnsubscribePuzzleEvents()
    {
        UnsubscribePuzzleList(_zoneAStage1Puzzles);
        UnsubscribePuzzleList(_zoneBStage1Puzzles);
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

        Debug.Log($"[PuzzleProgressManager] 퍼즐 해결 : {solvedPuzzle.name}");

        if (_zoneAStage1Puzzles.Contains(solvedPuzzle))
        {
            CheckZoneStage1SolvedAndNotifyIfNeeded(Zone.ZoneA);
            return;
        }

        if (_zoneBStage1Puzzles.Contains(solvedPuzzle))
        {
            CheckZoneStage1SolvedAndNotifyIfNeeded(Zone.ZoneB);
        }
    }

    /// <summary>
    /// 특정 Zone의 Stage1 퍼즐이 전부 solved 되었으면
    /// StageManager에 완료 보고를 보낸다.
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
        List<GameObject> targetScreens = zone == Zone.ZoneA
            ? _zoneAStage2Screens
            : _zoneBStage2Screens;

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
    /// 
    /// 현재는 StageManager가 아직 없으므로 로그만 찍는다.
    /// 나중에 StageManager 담당자가 만들면 여기서 실제 보고 호출로 바꾸면 된다.
    /// </summary>
    private void NotifyZoneStage1Completed(Zone zone)
    {
        Debug.Log($"[PuzzleProgressManager] StageManager 보고 필요 : {zone} Stage1 완료");

        // TODO:
        // StageManager 담당자가 구현 후 여기서 호출 연결
        // 예시:
        // stageManager.ReportZoneStage1Completed(zone);
    }
}