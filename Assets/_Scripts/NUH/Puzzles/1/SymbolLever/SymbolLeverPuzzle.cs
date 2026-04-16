using Fusion;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 1단계 / 문양 레버 퍼즐 본체
/// - 전체 문양 풀에서 6개를 랜덤 선택
/// - 선택된 6개를 레버 6개에 랜덤 배치
/// - 같은 6개로 정답 순서 6개 생성
/// - 레버 입력 시 즉시 현재 단계 판정
/// - 맞은 레버는 계속 내려간 상태 유지
/// - 틀리면 전부 다시 위로 초기화
/// </summary>
public class SymbolLeverPuzzle : PuzzleInteractableBase, IPuzzleSeedReceiver
{
    [Header("설정")]
    [SerializeField] private int leverCount = 6;                // 레버 개수
    [SerializeField] private int totalSymbolCount = 30;         // 전체 문양 종류 수

    [Header("레버 참조")]
    [SerializeField] private List<SymbolLeverView> leverViews = new();                  // 각 레버 뷰
    [SerializeField] private List<SymbolSpriteDisplay> leverSymbolDisplays = new();     // 각 레버 옆 문양 표시

    [Header("문양 스프라이트 풀")]
    [SerializeField] private List<Sprite> symbolSprites = new();                        // 문양 Id와 대응되는 스프라이트 목록

    [Header("디버그")]
    [SerializeField] private bool enableDebugLog = true;

    private readonly List<int> _selectedSymbolIds = new();      // 이번 판에 선택된 6개 문양
    private readonly List<int> _leverSymbolIds = new();         // 레버 0~5에 배치된 문양 ID
    private readonly List<int> _answerSequence = new();         // 정답 순서
    private readonly List<bool> _leverPulledStates = new();     // 각 레버가 내려간 상태인지

    private int _currentStep;       //  현재 입력 단계
    private bool _hasAnswerSeed;    // 시드 적용 완료 여부

    private void Awake()
    {
        EnsureLeverStateSize();
        ResetPuzzleVisualOnly();
    }

    public void ApplyAnswerSeed(int seed)
    {
        _selectedSymbolIds.Clear();
        _leverSymbolIds.Clear();
        _answerSequence.Clear();

        EnsureLeverStateSize();
        ResetLeverStates();

        _currentStep = 0;

        SeedRandom rng = new SeedRandom(seed);

        // 1. 전체 문양 풀 생성
        List<int> allSymbolIds = BuildAllSymbolIds();

        // 2. 이번 판에 사용할 6개 문양 선택
        List<int> selected = rng.PickUnique(allSymbolIds, leverCount);
        _selectedSymbolIds.AddRange(selected);

        // 3. 같은 6개를 셔플해서 레버에 배치
        List<int> leverPlacement = new List<int>(_selectedSymbolIds);
        rng.Shuffle(leverPlacement);
        _leverSymbolIds.AddRange(leverPlacement);

        // 4. 같은 6개를 다시 셔플해서 정답 순서 생성
        List<int> answerPlacement = new List<int>(_selectedSymbolIds);
        rng.Shuffle(answerPlacement);
        _answerSequence.AddRange(answerPlacement);

        _hasAnswerSeed = true;

        ApplyLeverSymbolVisuals();
        ResetPuzzleVisualOnly();

        Log($"정답 시드 적용 완료 | seed = {seed}");
        LogSequenceDebug();
    }

    /// <summary>
    /// 특정 레버가 이미 내려간 상태인지 반환
    /// </summary>
    public bool IsLeverAlreadyPulled(int interactableId)
    {
        if (interactableId < 0 || interactableId >= _leverPulledStates.Count)
            return false;

        return _leverPulledStates[interactableId];
    }


    public void OnLeverPulled(int interactableId)
    {
        if (!HasStateAuthority)
            return;

        if (IsSolved)
            return;

        if (!_hasAnswerSeed)
        {
            Log("아직 정답 시드가 적용되지 않아 입력 무시");
            return;
        }

        if (interactableId < 0 || interactableId >= _leverSymbolIds.Count)
            return;

        if (_leverPulledStates[interactableId])
        {
            Log($"이미 내려간 레버 입력 무시 | lever = {interactableId}");
            return;
        }

        int inputSymbolId = _leverSymbolIds[interactableId];
        int expectedSymbolId = _answerSequence[_currentStep];

        // 현재 단계 즉시 판정
        if (inputSymbolId != expectedSymbolId)
        {
            Log($"오답 입력 | step={_currentStep} | inputSymbol={inputSymbolId} | expectedSymbol={expectedSymbolId}");
            MarkFailed();
            ResetPuzzle();
            return;
        }

        // 정답이면 해당 레버를 내려간 상태로 유지
        _leverPulledStates[interactableId] = true;
        RefreshSingleLeverView(interactableId);

        _currentStep++;
        Log($"정답 입력 | currentStep = {_currentStep} / {leverCount}");

        if (_currentStep >= leverCount)
        {
            MarkSolved();
            Log("문양 레버 퍼즐 성공");
        }
    }

    /// <summary>
    /// 실패 또는 초기화 시 퍼즐 상태를 처음으로 되돌린다
    /// </summary>
    private void ResetPuzzle()
    {
        _currentStep = 0;
        ResetLeverStates();
        ResetPuzzleVisualOnly();
        Log("문양 레버 퍼즐 초기화");    
    }

    /// <summary>
    /// 내부 레버 상태 전부 초기화
    /// </summary>
    private void ResetLeverStates()
    {
        for (int i = 0; i < _leverPulledStates.Count; i++)
            _leverPulledStates[i] = false;
    }

    /// <summary>
    /// 레버 뷰를 현재 상태에 맞춰 반영
    /// </summary>
    private void ResetPuzzleVisualOnly()
    {
        RefreshAllLeverViews();
    }

    private void RefreshAllLeverViews()
    {
        int count = Mathf.Min(leverViews.Count, _leverPulledStates.Count);

        for(int i = 0; i < count; i++)
        {
            if (leverViews[i] == null)
                continue;

            leverViews[i].SetState(_leverPulledStates[i]);
        }
    }


    private void RefreshSingleLeverView(int interactableId)
    {
        if (interactableId < 0 || interactableId >= leverViews.Count)
            return;

        if (leverViews[interactableId] == null)
            return;

        leverViews[interactableId].SetState(_leverPulledStates[interactableId]);
    }

    /// <summary>
    /// 레버 옆 문양 표시에 현재 레버 배치 문양을 적용
    /// </summary>
    private void ApplyLeverSymbolVisuals()
    {
        int count = Mathf.Min(leverSymbolDisplays.Count, _leverSymbolIds.Count);

        for(int i = 0; i < leverSymbolDisplays.Count; i++)
        {
            if (leverSymbolDisplays[i] == null)
                continue;

            if (i > count)
            {
                leverSymbolDisplays[i].Clear();
                continue;
            }

            int symbolId = _leverSymbolIds[i];
            Sprite sprite = GetSymbolSprite(symbolId);
            leverSymbolDisplays[i].SetSprite(sprite);
        }
    }

    private void EnsureLeverStateSize()
    {
        while (_leverPulledStates.Count < leverCount)
            _leverPulledStates.Add(false);

        while (_leverPulledStates.Count > leverCount)
            _leverPulledStates.RemoveAt(_leverPulledStates.Count - 1);
    }

    private List<int> BuildAllSymbolIds()
    {
        List<int> ids = new List<int>(totalSymbolCount);

        for (int i = 0; i < totalSymbolCount; i++)
            ids.Add(i);

        return ids;
    }

    private Sprite GetSymbolSprite(int symbolId)
    {
        if (symbolId < 0 || symbolId >= symbolSprites.Count)
            return null;

        return symbolSprites[symbolId];
    }

    protected override void ServerInteract(PlayerController actor)
    {
        // 루트 직접 상호작용 없음
    }

    private void LogSequenceDebug()
    {
        if (!enableDebugLog) 
            return;

        string leverSymbols = string.Join(", ", _leverSymbolIds);
        string answerSymbols = string.Join(", ", _answerSequence);

        Debug.Log($"레버 배치 문양 IDs = {leverSymbols}", this);
        Debug.Log($"정답 순서 문양 IDs = {answerSymbols}", this);
    }

    private void Log(string m)
    {
        if (!enableDebugLog)
            return;

        Debug.Log($"[SymbolLeverPuzzle] {m}", this);
    }
}
