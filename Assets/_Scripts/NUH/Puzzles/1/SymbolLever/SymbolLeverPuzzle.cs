using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 1단계 / 문양 레버 퍼즐 본체
/// - 6개 레버의 현재 상태 관리
/// - 시드 기반으로 정답 상태 생성
/// - 확인 버튼 입력 시 성공/실패 판정
/// </summary>
public class SymbolLeverPuzzle : PuzzleInteractableBase, IPuzzleSeedReceiver
{
    [Header("설정")]
    [SerializeField] private int leverCount = 6;                // 레버 개수
    [SerializeField] private List<bool> currentStates = new();  // 현재 레버 상태
    [SerializeField] private List<bool> answerStates = new();   // 정답 레버 상태

    [Header("레버 시각 표현")]
    [SerializeField] private List<SymbolLeverView> leverViews = new();  //각 레버의 시각 표현 스크립트

    [Header("디버그")]
    [SerializeField] private bool enableDebugLog = true;        // 디버그 로그 출력 여부

    private bool _hasAnswerSeed;                                // 정답 시드 적용 완료 여부

    /// <summary>
    /// 초기 상태 보정
    /// 현재/정답 배열 크기를 leverCount와 맞추기
    /// </summary>
    private void Awake()
    {
        EnsureStateListSize(currentStates, leverCount, false);
        EnsureStateListSize(answerStates, leverCount, false);

        ResetAllLeversToDefault();
    }

    /// <summary>
    /// PuzzleSpawnManager에서 전달한 정답 생성용 시드를 적용
    /// </summary>
    public void ApplyAnswerSeed(int seed)
    {
        System.Random rng = new(seed);

        EnsureStateListSize(answerStates, leverCount, false);

        for (int i = 0; i < leverCount; i++)
        {
            // false = 위, true = 아래
            answerStates[i] = rng.Next(0, 2) == 1;
        }

        _hasAnswerSeed = true;
        Log($"정답 시드 적용 완료 : {seed}");
    }

    /// <summary>
    /// 개별 레버 토글
    /// 레버 조작 오브젝트가 호출하는 함수
    /// </summary>
    public void ToggleLever(int leverIndex)
    {
        if (!HasStateAuthority)
            return;

        if (leverIndex < 0 || leverIndex >= currentStates.Count)
            return;

        currentStates[leverIndex] = !currentStates[leverIndex];

        RefreshSingleLeverView(leverIndex);

        Log($"레버 토글 : {leverIndex} -> {currentStates[leverIndex]}");
    }

    /// <summary>
    /// 확인 버튼에서 호출
    /// 현재 상태가 정답과 일치하는지 검사
    /// </summary>
    public void ConfirmCurrentState(PlayerController actor)
    {
        if (!HasStateAuthority)
            return;

        if (IsSolved) 
            return;

        if (!_hasAnswerSeed)
        {
            Log("정답 시드가 아직 적용되지 않아 확인 불가");
            return;
        }

        if (IsCurrentStateCorrect())
        {
            MarkSolved();
            Log("문양 레버 퍼즐 성공");
            return;
        }

        MarkFailed();
        ResetAllLeversToDefault();
        Log("문양 레버 퍼즐 실패 -> 초기화");
    }

    /// <summary>
    /// 현재 레버 상태와 정답 상태를 비교
    /// </summary>
    private bool IsCurrentStateCorrect()
    {
        if (currentStates.Count != answerStates.Count)
            return false;

        for(int i = 0; i < currentStates.Count; i++)
        {
            if (currentStates[i] != answerStates[i])
                return false;
        }

        return true;
    }

    /// <summary>
    /// 실패 시 초기 상태로 리셋
    /// 문양 레버는 전부 위로 초기화
    /// </summary>
    private void ResetAllLeversToDefault()
    {
        EnsureStateListSize(currentStates, leverCount, false);

        for (int i = 0; i < currentStates.Count; i++)
        {
            currentStates[i] = false;
        }

        RefreshAllLeverViews();
    }
    
    /// <summary>
    /// 전체 레버 뷰를현재 상태 기준으로 갱신
    /// </summary>
    private void RefreshAllLeverViews()
    {
        int count = Mathf.Min(currentStates.Count, leverViews.Count);

        for(int i = 0; i < count; i++)
        {
            SymbolLeverView view = leverViews[i];
            if (view == null)
                continue;

            view.SetState(currentStates[i]);
        }
    }

    /// <summary>
    /// 특정 인덱스의 레버 뷰만 갱신
    /// </summary>
    /// <param name="leverIndex"></param>
    private void RefreshSingleLeverView(int leverIndex)
    {
        if (leverIndex < 0 || leverIndex >= leverViews.Count)
            return;

        SymbolLeverView view = leverViews[leverIndex];
        if (view == null)
            return;

        view.SetState(currentStates[leverIndex]);
    }

    /// <summary>
    /// 리스트의 크기를 LeverCount에 맞춤
    /// </summary>
    private void EnsureStateListSize(List<bool> list, int targetCount, bool defaultValue)
    {
        if (list == null)
            return;

        while (list.Count < targetCount)
            list.Add(defaultValue);

        while (list.Count > targetCount)
            list.RemoveAt(list.Count - 1);
    }

    /// <summary>
    /// 이 퍼즐 루트 자체는 직접 상호작용 하지 않음
    /// 실제 상호작용은 레버/확인 버튼쪽에서 함
    /// </summary>
    protected override void ServerInteract(PlayerController actor)
    {
        
    }

    private void Log(string m)
    {
        if (!enableDebugLog)
            return;

        Debug.Log($"[SymbolLeverPuzzle] {m}", this);
    }
}
