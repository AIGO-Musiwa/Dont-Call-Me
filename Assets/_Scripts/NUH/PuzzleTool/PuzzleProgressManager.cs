using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 이번 판에 배치된 1단계 퍼즐 진행도를 집계하고
/// 전부 클리어되면 2단계 화면을 활성화하는 매니저
/// 
/// 전제
/// - 씬에 고정된 퍼즐을 직접 찾지 않음
/// - PuzzleSpawnManager가 랜덤 배치 완료 후
///   이번 판의 1단계 퍼즐 목록과 2단계 화면 목록을 등록해줌
/// </summary>
public class PuzzleProgressManager : MonoBehaviour
{

    private readonly List<PuzzleInteractableBase> _stage1Puzzles = new();   // 이번 판 1단계 퍼즐 목록
    private readonly List<GameObject> _stage2Screens = new();               // 이번 판 2단계 화면 목록

    private bool _isRegistered;         // 이번 판 퍼즐 목록이 등록되었는지
    private bool _isStage2Unlocked;     // 2단계가 이미 해금되었는지

    public bool IsRegistered => _isRegistered;

    public bool IsStage2Unlocked => _isStage2Unlocked;

    public int RegisteredPuzzleCount => _stage1Puzzles.Count;


    /// <summary>
    /// 이번 판의 1단계 퍼즐 목록과 2단계 퍼즐 화면 목록을 등록한다
    /// 등록 전 기존 데이터가 있으면 먼저 초기화
    /// </summary>
    public void InitializeRound(List<PuzzleInteractableBase> stage1Puzzles, List<GameObject> stage2Screens)
    {
        // 이전 판 정보가 남아있으면 먼저 정리
        ResetProgress();

        if (stage1Puzzles == null || stage1Puzzles.Count == 0)
        {
            Debug.LogWarning("InitializeRound 실패 : 등록할 1단계 퍼즐이 없습니다.");
            return;
        }

        // 1단계 퍼즐 등록
        for (int i = 0; i < stage1Puzzles.Count; i++)
        {
            PuzzleInteractableBase puzzle = stage1Puzzles[i];

            if (puzzle == null)
                continue;

            _stage1Puzzles.Add(puzzle);
        }

        // 2단계 퍼즐 화면 등록
        if (stage2Screens != null)
        {
            for (int i = 0; i < stage2Screens.Count; i++)
            {
                GameObject screen = stage2Screens[i];

                if (screen == null)
                    continue;

                _stage2Screens.Add(screen);
            }
        }

        // 등록된 화면은 시작 시 꺼둠
        SetStage2ScreensActive(false);

        // 퍼즐 성공 이벤트 구독
        SubscribePuzzleEvents();

        _isRegistered = true;
        _isStage2Unlocked = false;

        Debug.Log($"InitializeRound 완료 : 1단계 퍼즐 {_stage1Puzzles.Count}개 등록, 2단계 퍼즐 화면 {_stage2Screens.Count}개 등록");

        // 등록 시점에 이미 전부 해결된 상태일 수도 있으니 보험용 검사
        CheckAllSolvedAndUnlockIfNeeded();
    }

    /// <summary>
    /// 현재 등록된 진행도 정보를 전부 초기화
    /// 다음판 시작 전에 호출 가능
    /// </summary>
    public void ResetProgress()
    {
        UnsubscribePuzzleEvents();

        _stage1Puzzles.Clear();
        _stage2Screens.Clear();

        _isRegistered = false;
        _isStage2Unlocked = false;
    }

    /// <summary>
    /// 등록된 모든 1단계 퍼즐이 해결되었는지 검사
    /// </summary>
    public bool AreAllStage1PuzzlesSolved()
    {
        if (!_isRegistered)
            return false;

        if (_stage1Puzzles.Count == 0)
            return false;

        for (int i = 0; i < _stage1Puzzles.Count; i++)
        {
            PuzzleInteractableBase puzzle = _stage1Puzzles[i];

            if (puzzle == null)
                return false;

            if (!puzzle.IsSolved)
                return false;
        }

        return true;
    }

    /// <summary>
    /// 등록된 1단계 퍼즐들의 성공 이벤트를 구독
    /// </summary>
    private void SubscribePuzzleEvents()
    {
        for (int i = 0; i < _stage1Puzzles.Count; i++)
        {
            PuzzleInteractableBase puzzle = _stage1Puzzles[i];

            if (puzzle == null)
                continue;

            puzzle.Solved -= HandlePuzzleSolved;
            puzzle.Solved += HandlePuzzleSolved;
        }
    }

    /// <summary>
    /// 등록된 1단계 퍼즐들의 성공 이벤트 구독 해제
    /// </summary>
    private void UnsubscribePuzzleEvents()
    {
        for (int i = 0; i < _stage1Puzzles.Count; i++)
        {
            PuzzleInteractableBase puzzle = _stage1Puzzles[i];

            if (puzzle == null)
                continue;

            puzzle.Solved -= HandlePuzzleSolved;
        }
    }

    /// <summary>
    /// 1단계 퍼즐 하나가 해결되었을 때 호출
    /// 전부 해결되었는지 다시 검사
    /// </summary>
    private void HandlePuzzleSolved(PuzzleInteractableBase solvedPuzzle)
    {
        if (!_isRegistered)
            return;

        if (solvedPuzzle != null)
            Debug.Log($"퍼즐 해결 : {solvedPuzzle.name}");

        CheckAllSolvedAndUnlockIfNeeded();
    }

    /// <summary>
    /// 모든 1단계 퍼즐이 해결되었으면 2단계 해금
    /// </summary>
    private void CheckAllSolvedAndUnlockIfNeeded()
    {
        if (_isStage2Unlocked)
            return;

        if (!AreAllStage1PuzzlesSolved())
            return;

        UnlockStage2();
    }

    /// <summary>
    /// 2단계를 해금하고 2단계 퍼즐 화면 활성화
    /// </summary>
    private void UnlockStage2()
    {
        _isStage2Unlocked = true;
        SetStage2ScreensActive(true);

        Debug.Log("1단계 퍼즐 전부 완료. 2단계 퍼즐 화면 On");
    }

    /// <summary>
    /// 등록된 2단계 화면들을 일괄 활성/비활성 처리
    /// </summary>
    private void SetStage2ScreensActive(bool active)
    {
        for(int i = 0; i < _stage2Screens.Count; i++)
        {
            GameObject screen = _stage2Screens[i];

            if (screen == null)
                continue;

            screen.SetActive(active);
        }
    }
}
