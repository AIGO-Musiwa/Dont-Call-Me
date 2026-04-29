using Fusion;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 1단계 / 문양 레버 퍼즐 본체.
/// - 전체 문양 풀에서 6개를 랜덤 선택
/// - 선택된 6개를 레버 6개에 랜덤 배치
/// - 같은 6개로 정답 순서 6개 생성
/// - 정답 레버는 부드럽게 내려간다
/// - 오답이면 전체 문양 빨간색 깜빡임 후 레버 전체를 부드럽게 올린다
/// - 성공하면 전체 문양을 초록색으로 유지한다
/// </summary>
public class SymbolLeverPuzzle : PuzzleInteractableBase, IPuzzleSeedReceiver
{
    [Header("설정")]
    [SerializeField] private int leverCount = 6;                         // 레버 개수
    [SerializeField] private int totalSymbolCount = 30;                  // 전체 문양 종류 수

    [Header("레버 참조")]
    [SerializeField] private List<SymbolLeverView> leverViews = new();   // 각 레버 뷰
    [SerializeField] private List<SymbolSpriteDisplay> leverSymbolDisplays = new(); // 각 레버 옆 문양 표시

    [Header("문양 스프라이트 풀")]
    [SerializeField] private List<Sprite> symbolSprites = new();         // 문양 ID와 대응되는 스프라이트 목록

    [Header("문양 색상")]
    [SerializeField] private Color normalSymbolColor = Color.white;      // 기본 문양 색상
    [SerializeField] private Color failSymbolColor = Color.red;          // 실패 깜빡임 색상
    [SerializeField] private Color solvedSymbolColor = Color.green;      // 성공 색상

    [Header("실패 연출")]
    [SerializeField] private int failBlinkCount = 3;                     // 실패 시 깜빡임 횟수
    [SerializeField] private float failBlinkOnTime = 0.15f;              // 빨간색 유지 시간
    [SerializeField] private float failBlinkOffTime = 0.15f;             // 기본색 유지 시간
    [SerializeField] private bool lockInputDuringFailEffect = true;      // 실패 연출 중 입력 잠금 여부

    [Header("디버그")]
    [SerializeField] private bool enableDebugLog = true;                 // 디버그 로그 여부

    private readonly List<int> _selectedSymbolIds = new();               // 이번 판에 선택된 6개 문양
    private readonly List<int> _leverSymbolIds = new();                  // 레버 0~5에 배치된 문양 ID
    private readonly List<int> _answerSequence = new();                  // 정답 순서
    private bool _hasAnswerSeed;                                         // 시드 적용 완료 여부

    private Coroutine _failVisualRoutine;                                // 실패 시각 연출 코루틴
    private int _lastHandledFailSerial = -1;                             // 중복 실패 연출 방지용

    [Networked] private int NetCurrentStep { get; set; }                 // 현재 몇 단계까지 맞췄는지

    [Networked, OnChangedRender(nameof(OnPulledMaskChanged))]
    private int NetPulledMask { get; set; }                              // 내려간 레버 비트마스크

    [Networked, OnChangedRender(nameof(OnFailEffectTriggered))]
    private int NetFailEffectSerial { get; set; }                        // 실패 연출 이벤트 카운터

    [Networked, OnChangedRender(nameof(OnSolvedVisualChanged))]
    private NetworkBool NetSolvedVisual { get; set; }                    // 성공 문양 색상 표시 여부

    [Networked] private NetworkBool NetInputLocked { get; set; }         // 입력 잠금 여부

    /// <summary>
    /// 현재 레버 입력이 잠겨 있는지 반환한다.
    /// Interactable에서 상호작용 가능 여부를 검사할 때 사용한다.
    /// </summary>
    public bool IsInputLocked => NetInputLocked;

    private void Awake()
    {
        // Awake에서는 Networked 값 접근 금지.
        ResetAllLeverViewsToUpImmediate();
        SetAllSymbolColors(normalSymbolColor);
    }

    public override void Spawned()
    {
        base.Spawned();

        ApplyPulledMaskToViewsImmediate();
        ApplySolvedSymbolVisual();
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

        // 1. 사용할 문양 6개 선택.
        List<int> selected = rng.PickUnique(allSymbolIds, leverCount);
        _selectedSymbolIds.AddRange(selected);

        // 2. 레버 배치 문양 생성.
        List<int> leverPlacement = new List<int>(_selectedSymbolIds);
        rng.Shuffle(leverPlacement);
        _leverSymbolIds.AddRange(leverPlacement);

        // 3. 정답 순서 생성.
        List<int> answerPlacement = new List<int>(_selectedSymbolIds);
        rng.Shuffle(answerPlacement);
        _answerSequence.AddRange(answerPlacement);

        _hasAnswerSeed = true;

        if (HasStateAuthority)
        {
            NetCurrentStep = 0;
            NetPulledMask = 0;
            NetFailEffectSerial = 0;
            NetSolvedVisual = false;
            NetInputLocked = false;
        }

        ApplyLeverSymbolVisuals();
        ApplyPulledMaskToViewsImmediate();
        SetAllSymbolColors(normalSymbolColor);

        Log($"정답 시드 적용 완료 | seed = {seed}");
        LogSequenceDebug();
    }

    /// <summary>
    /// 특정 레버가 이미 내려간 상태인지 확인한다.
    /// </summary>
    public bool IsLeverAlreadyPulled(int interactableId)
    {
        return IsMaskBitOn(NetPulledMask, interactableId);
    }

    /// <summary>
    /// 레버 입력을 처리한다.
    /// </summary>
    public void OnLeverPulled(int interactableId)
    {
        if (!HasStateAuthority)
            return;

        if (IsSolved)
            return;

        if (NetInputLocked && lockInputDuringFailEffect)
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

        //TODO_Sound - 문양 레버 조작

        int inputSymbolId = _leverSymbolIds[interactableId];
        int expectedSymbolId = _answerSequence[NetCurrentStep];

        // 오답이면 실패 연출 후 초기화.
        if (inputSymbolId != expectedSymbolId)
        {
            Log($"오답 입력 | step={NetCurrentStep} | inputSymbol={inputSymbolId} | expectedSymbol={expectedSymbolId}");
            MarkFailed();
            StartFailSequence();
            return;
        }

        NetPulledMask = SetMaskBit(NetPulledMask, interactableId, true);
        NetCurrentStep++;

        // 호스트 즉시 반영.
        ApplyPulledMaskToViewsAnimated();

        Log($"정답 입력 | currentStep = {NetCurrentStep} / {leverCount}");

        if (NetCurrentStep >= leverCount)
        {
            MarkSolved();
            NetSolvedVisual = true;
            ApplySolvedSymbolVisual();

            Log("문양 레버 퍼즐 성공");
        }
    }

    /// <summary>
    /// 서버에서 실패 연출을 시작한다.
    /// 짧은 실패 이벤트는 Serial로 모든 클라이언트에 전달한다.
    /// </summary>
    private void StartFailSequence()
    {
        if (!HasStateAuthority)
            return;

        NetInputLocked = true;
        NetFailEffectSerial++;

        HandleFailEffectVisual(NetFailEffectSerial);
        StartCoroutine(CoResetAfterFailVisual());
    }

    /// <summary>
    /// 실패 시각 연출이 끝난 뒤 퍼즐 진행 상태를 초기화한다.
    /// 서버에서만 상태를 변경한다.
    /// </summary>
    private IEnumerator CoResetAfterFailVisual()
    {
        float waitSeconds = (failBlinkOnTime + failBlinkOffTime) * failBlinkCount;
        yield return new WaitForSeconds(waitSeconds);

        NetCurrentStep = 0;
        NetPulledMask = 0;

        // 호스트 즉시 반영.
        ApplyPulledMaskToViewsAnimated();

        NetInputLocked = false;

        Log("문양 레버 퍼즐 초기화");
    }

    /// <summary>
    /// 실패 이벤트가 들어오면 모든 클라이언트에서 문양 깜빡임을 재생한다.
    /// </summary>
    private void OnFailEffectTriggered()
    {
        HandleFailEffectVisual(NetFailEffectSerial);
    }

    /// <summary>
    /// 실패 시각 연출 중복 실행을 막고 코루틴을 시작한다.
    /// </summary>
    private void HandleFailEffectVisual(int serial)
    {
        if (_lastHandledFailSerial == serial)
            return;

        _lastHandledFailSerial = serial;

        if (_failVisualRoutine != null)
            StopCoroutine(_failVisualRoutine);

        _failVisualRoutine = StartCoroutine(CoFailSymbolBlink());
    }

    /// <summary>
    /// 전체 문양을 빨간색으로 깜빡인다.
    /// </summary>
    private IEnumerator CoFailSymbolBlink()
    {
        for (int i = 0; i < failBlinkCount; i++)
        {
            SetAllSymbolColors(failSymbolColor);
            yield return new WaitForSeconds(failBlinkOnTime);

            SetAllSymbolColors(normalSymbolColor);
            yield return new WaitForSeconds(failBlinkOffTime);
        }

        SetAllSymbolColors(normalSymbolColor);
        _failVisualRoutine = null;
    }

    /// <summary>
    /// Networked 마스크 변경 시 모든 클라이언트에서 레버 뷰를 애니메이션으로 갱신한다.
    /// </summary>
    private void OnPulledMaskChanged()
    {
        ApplyPulledMaskToViewsAnimated();
    }

    /// <summary>
    /// 성공 시각 상태 변경 시 전체 문양 색상을 갱신한다.
    /// </summary>
    private void OnSolvedVisualChanged()
    {
        ApplySolvedSymbolVisual();
    }

    /// <summary>
    /// 현재 마스크 상태를 레버 뷰들에 애니메이션으로 반영한다.
    /// </summary>
    private void ApplyPulledMaskToViewsAnimated()
    {
        int count = Mathf.Min(leverViews.Count, leverCount);

        for (int i = 0; i < count; i++)
        {
            if (leverViews[i] == null)
                continue;

            leverViews[i].SetStateAnimated(IsMaskBitOn(NetPulledMask, i));
        }
    }

    /// <summary>
    /// 현재 마스크 상태를 레버 뷰들에 즉시 반영한다.
    /// 스폰 초기화용이다.
    /// </summary>
    private void ApplyPulledMaskToViewsImmediate()
    {
        int count = Mathf.Min(leverViews.Count, leverCount);

        for (int i = 0; i < count; i++)
        {
            if (leverViews[i] == null)
                continue;

            leverViews[i].SetStateImmediate(IsMaskBitOn(NetPulledMask, i));
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
    /// 레버 옆 문양 스프라이트를 적용한다.
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
            leverSymbolDisplays[i].SetColor(normalSymbolColor);
        }
    }

    /// <summary>
    /// 성공 상태에 맞춰 문양 색상을 적용한다.
    /// </summary>
    private void ApplySolvedSymbolVisual()
    {
        if (NetSolvedVisual)
            SetAllSymbolColors(solvedSymbolColor);
        else
            SetAllSymbolColors(normalSymbolColor);
    }

    /// <summary>
    /// 모든 레버 문양 색상을 변경한다.
    /// </summary>
    private void SetAllSymbolColors(Color color)
    {
        for (int i = 0; i < leverSymbolDisplays.Count; i++)
        {
            if (leverSymbolDisplays[i] == null)
                continue;

            leverSymbolDisplays[i].SetColor(color);
        }
    }

    /// <summary>
    /// 유효한 레버 인덱스인지 검사한다.
    /// </summary>
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
        // 루트 직접 상호작용 없음.
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