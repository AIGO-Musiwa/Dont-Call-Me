using Fusion;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 1단계 / 문양 레버 퍼즐 본체
/// - 전체 문양 풀에서 6개를 랜덤 선택
/// - 선택된 6개를 레버 6개에 랜덤 배치
/// - 같은 6개로 정답 순서 6개 생성
/// - 레버 입력 시 즉시 현재 단계 판정
/// - 맞은 레버는 Networked 마스크로 유지
/// - 틀리면 전부 다시 위로 초기화
/// - 다른 플레이어도 현재 레버 상태를 보고 이어서 풀 수 있다
/// </summary>
public class SymbolLeverPuzzle : PuzzleInteractableBase, IPuzzleSeedReceiver
{
    [Header("설정")]
    [SerializeField] private int leverCount = 6;                // 레버 개수
    [SerializeField] private int totalSymbolCount = 30;         // 전체 문양 종류 수

    [Header("레버 참조")]
    [SerializeField] private List<SymbolLeverView> leverViews = new();              // 각 레버 뷰
    [SerializeField] private List<SymbolSpriteDisplay> leverSymbolDisplays = new(); // 각 레버 옆 문양 표시

    [Header("문양 스프라이트 풀")]
    [SerializeField] private List<Sprite> symbolSprites = new(); // 문양 ID와 대응되는 스프라이트 목록

    [Header("디버그")]
    [SerializeField] private bool enableDebugLog = true;

    private readonly List<int> _selectedSymbolIds = new(); // 이번 판에 선택된 6개 문양
    private readonly List<int> _leverSymbolIds = new();    // 레버 0~5에 배치된 문양 ID
    private readonly List<int> _answerSequence = new();    // 정답 순서
    private bool _hasAnswerSeed;                           // 시드 적용 완료 여부

    [Networked] private int NetCurrentStep { get; set; } // 현재 몇 단계까지 맞췄는지
    [Networked, OnChangedRender(nameof(OnPulledMaskChanged))]
    private int NetPulledMask { get; set; } // 내려간 레버 비트마스크

    private void Awake()
    {
        // Awake에서는 Networked 값 접근 금지
        ResetAllLeverViewsToUpImmediate();
    }

    public override void Spawned()
    {
        ApplyPulledMaskToViews();
    }

    /// <summary>
    /// 시드 기반으로 레버 배치와 정답 순서를 생성한다.
    /// </summary>
    public void ApplyAnswerSeed(int seed)
    {
        _selectedSymbolIds.Clear();
        _leverSymbolIds.Clear();
        _answerSequence.Clear();

        SeedRandom rng = new SeedRandom(seed);

        List<int> allSymbolIds = BuildAllSymbolIds();

        // 1. 사용할 문양 6개 선택
        List<int> selected = rng.PickUnique(allSymbolIds, leverCount);
        _selectedSymbolIds.AddRange(selected);

        // 2. 레버 배치 문양 생성
        List<int> leverPlacement = new List<int>(_selectedSymbolIds);
        rng.Shuffle(leverPlacement);
        _leverSymbolIds.AddRange(leverPlacement);

        // 3. 정답 순서 생성
        List<int> answerPlacement = new List<int>(_selectedSymbolIds);
        rng.Shuffle(answerPlacement);
        _answerSequence.AddRange(answerPlacement);

        _hasAnswerSeed = true;

        if (HasStateAuthority)
        {
            NetCurrentStep = 0;
            NetPulledMask = 0;
        }

        ApplyLeverSymbolVisuals();
        ApplyPulledMaskToViews();

        Log($"정답 시드 적용 완료 | seed = {seed}");
        LogSequenceDebug();
    }

    /// <summary>
    /// 특정 레버가 이미 내려간 상태인지 확인
    /// </summary>
    public bool IsLeverAlreadyPulled(int interactableId)
    {
        return IsMaskBitOn(NetPulledMask, interactableId);
    }

    /// <summary>
    /// 레버 입력 처리
    /// </summary>
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

        if (!IsValidLeverIndex(interactableId))
            return;

        if (IsLeverAlreadyPulled(interactableId))
        {
            Log($"이미 내려간 레버 입력 무시 | lever = {interactableId}");
            return;
        }

        int inputSymbolId = _leverSymbolIds[interactableId];
        int expectedSymbolId = _answerSequence[NetCurrentStep];

        // 오답이면 즉시 실패 + 초기화
        if (inputSymbolId != expectedSymbolId)
        {
            Log($"오답 입력 | step={NetCurrentStep} | inputSymbol={inputSymbolId} | expectedSymbol={expectedSymbolId}");
            MarkFailed();
            ResetPuzzle();
            return;
        }

        NetPulledMask = SetMaskBit(NetPulledMask, interactableId, true);
        NetCurrentStep++;

        // 호스트 즉시 반영
        ApplyPulledMaskToViews();

        Log($"정답 입력 | currentStep = {NetCurrentStep} / {leverCount}");

        if (NetCurrentStep >= leverCount)
        {
            MarkSolved();
            Log("문양 레버 퍼즐 성공");
        }
    }

    /// <summary>
    /// 실패/초기화 처리
    /// </summary>
    private void ResetPuzzle()
    {
        NetCurrentStep = 0;
        NetPulledMask = 0;
        ApplyPulledMaskToViews();
        Log("문양 레버 퍼즐 초기화");
    }

    /// <summary>
    /// Networked 마스크 변경 시 모든 클라이언트에서 뷰 갱신
    /// </summary>
    private void OnPulledMaskChanged()
    {
        ApplyPulledMaskToViews();
    }

    /// <summary>
    /// 현재 마스크 상태를 레버 뷰들에 반영
    /// </summary>
    private void ApplyPulledMaskToViews()
    {
        int count = Mathf.Min(leverViews.Count, leverCount);

        for (int i = 0; i < count; i++)
        {
            if (leverViews[i] == null)
                continue;

            leverViews[i].SetState(IsMaskBitOn(NetPulledMask, i));
        }
    }

    /// <summary>
    /// 스폰 전 기본 상태에서는 모든 레버를 위로 맞춘다.
    /// </summary>
    private void ResetAllLeverViewsToUpImmediate()
    {
        int count = Mathf.Min(leverViews.Count, leverCount);

        for (int i = 0; i < count; i++)
        {
            if (leverViews[i] == null)
                continue;

            leverViews[i].ResetToDefaultImmediate();
        }
    }

    /// <summary>
    /// 레버 옆 문양 스프라이트 적용
    /// </summary>
    private void ApplyLeverSymbolVisuals()
    {
        int count = Mathf.Min(leverSymbolDisplays.Count, _leverSymbolIds.Count);

        for (int i = 0; i < leverSymbolDisplays.Count; i++)
        {
            if (leverSymbolDisplays[i] == null)
                continue;

            if (i >= count)
            {
                leverSymbolDisplays[i].Clear();
                continue;
            }

            int symbolId = _leverSymbolIds[i];
            Sprite sprite = GetSymbolSprite(symbolId);
            leverSymbolDisplays[i].SetSprite(sprite);
        }
    }

    private bool IsValidLeverIndex(int index)
    {
        return index >= 0 && index < _leverSymbolIds.Count;
    }

    private static bool IsMaskBitOn(int mask, int bitIndex)
    {
        if (bitIndex < 0)
            return false;

        return (mask & (1 << bitIndex)) != 0;
    }

    private static int SetMaskBit(int mask, int bitIndex, bool enabled)
    {
        if (bitIndex < 0)
            return mask;

        return enabled ? (mask | (1 << bitIndex)) : (mask & ~(1 << bitIndex));
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

        Debug.Log($"[SymbolLeverPuzzle] 레버 배치 문양 IDs = [{leverSymbols}]", this);
        Debug.Log($"[SymbolLeverPuzzle] 정답 순서 문양 IDs = [{answerSymbols}]", this);
    }

    private void Log(string message)
    {
        if (!enableDebugLog)
            return;

        Debug.Log($"[SymbolLeverPuzzle] {message}", this);
    }
}