using Fusion;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 2-3 시약 제조 퍼즐 본체 로직.
/// 
/// 멀티플레이 기준
/// - 다른 플레이어도 현재 슬롯/편집 칸/제조 진행 상태를 볼 수 있어야 한다.
/// - 다른 플레이어도 버튼 상호작용 시 서버 권한에서 실제 입력이 먹어야 한다.
/// - 그래서 "보여줘야 하는 상태"는 전부 Networked로 들고 간다.
/// - 성공 시 Stage3HintRoot에 배정된 3단계 힌트를 표시할 수 있다.
/// </summary>
public class ReagentCraftPuzzle : PuzzleInteractableBase, IPuzzleSeedReceiver
{
    [Header("참조")]
    [SerializeField] private ReagentCraftView craftView; // 제조 모니터 표시 담당 뷰
    [SerializeField] private ReagentAnalyzer analyzer; // 판별기 참조
    [SerializeField] private Transform craftedItemSpawnPoint; // 결과 시약 생성 위치

    [Header("결과 시약 프리팹")]
    [SerializeField] private NetworkObject correctReagentPrefab; // 정답 시약 프리팹
    [SerializeField] private NetworkObject wrongReagentPrefab; // 오답 시약 프리팹

    [Header("설정")]
    [SerializeField] private int recipeSlotCount = 3; // 시약 선택 슬롯 개수
    [SerializeField] private int progressStepCount = 10; // 프로그레스 총 칸 수
    [SerializeField] private int countdownStartNumber = 3; // 제조 시작 카운트다운 시작 숫자
    [SerializeField] private float countdownStepSeconds = 1f; // 카운트다운 한 숫자 유지 시간
    [SerializeField] private float progressStepInterval = 1f; // 프로그레스 한 칸 진행 간격
    [SerializeField] private bool showStage3HintImmediately = true; // 성공 직후 3단계 힌트 화면으로 넘길지 여부
    [SerializeField] private bool enableDebugLog = true; // 디버그 로그 출력 여부

    private ReagentAnswerGenerator.ReagentAnswerData _answerData; // seed 기반 정답 데이터
    private bool _hasAnswerSeedLocal; // 로컬에서 answer seed 생성 완료 여부

    private readonly List<ReagentAnswerGenerator.ReagentActionStep> _playerActionInputs = new(); // 서버 권한 기준 플레이어 입력 기록
    private readonly HashSet<int> _inputUsedProgressIndices = new(); // 서버 권한 기준 같은 칸 중복 입력 방지

    private Coroutine _craftRoutine; // 서버 권한 기준 제조 진행 코루틴
    private CraftedReagentItem _spawnedCraftedItem; // 서버 권한 기준 현재 결과 시약 참조
    private bool _wasCraftInterruptedByCorrectAnalyze; // 정답 시약 판별 성공으로 제조가 중단되었는지 여부

    private bool _lastRenderedSolved = false; // Render 중 성공 상태 캐시
    private int _lastRenderedViewHash = int.MinValue; // Render 중 불필요한 전체 갱신 방지용 캐시

    private readonly List<int> _progressActionStatesCache = new(); // View 전달용 프로그레스 액션 상태 캐시

    private FinalCodeHintData _stage3HintData; // 이 퍼즐이 표시할 3단계 힌트 데이터
    private bool _hasStage3HintData; // 3단계 힌트 데이터 적용 여부

    [Networked] private NetworkBool NetSeedApplied { get; set; } // 시드 적용 완료 여부
    [Networked] private int NetEditingSlotIndex { get; set; } // 현재 편집 중인 슬롯 인덱스, 완료면 recipeSlotCount
    [Networked] private int NetCurrentProgressIndex { get; set; } // 현재 진행 중인 프로그레스 칸 번호(1~10), 미시작은 0
    [Networked] private NetworkBool NetIsCrafting { get; set; } // 제조 진행 중인지 여부
    [Networked] private NetworkBool NetIsCraftStartPending { get; set; } // 제조 시작 대기 중인지 여부
    [Networked] private NetworkBool NetHasSpawnedCraftedItem { get; set; } // 현재 결과 시약이 월드에 남아 있는지 여부
    [Networked] private int NetCountdownNumber { get; set; } // 제조 시작 전 카운트다운 숫자, 0이면 숨김

    [Networked] private int NetSlot0Value { get; set; } // 0번 슬롯 현재 표시 시약
    [Networked] private int NetSlot1Value { get; set; } // 1번 슬롯 현재 표시 시약
    [Networked] private int NetSlot2Value { get; set; } // 2번 슬롯 현재 표시 시약

    [Networked] private NetworkBool NetSlot0Locked { get; set; } // 0번 슬롯 확정 여부
    [Networked] private NetworkBool NetSlot1Locked { get; set; } // 1번 슬롯 확정 여부
    [Networked] private NetworkBool NetSlot2Locked { get; set; } // 2번 슬롯 확정 여부

    [Networked] private int NetProgressAction0 { get; set; } // 0번 프로그레스 액션, 0=None, 1=Heat, 2=Cool
    [Networked] private int NetProgressAction1 { get; set; } // 1번 프로그레스 액션
    [Networked] private int NetProgressAction2 { get; set; } // 2번 프로그레스 액션
    [Networked] private int NetProgressAction3 { get; set; } // 3번 프로그레스 액션
    [Networked] private int NetProgressAction4 { get; set; } // 4번 프로그레스 액션
    [Networked] private int NetProgressAction5 { get; set; } // 5번 프로그레스 액션
    [Networked] private int NetProgressAction6 { get; set; } // 6번 프로그레스 액션
    [Networked] private int NetProgressAction7 { get; set; } // 7번 프로그레스 액션
    [Networked] private int NetProgressAction8 { get; set; } // 8번 프로그레스 액션
    [Networked] private int NetProgressAction9 { get; set; } // 9번 프로그레스 액션

    public override void Spawned()
    {
        base.Spawned();

        if (analyzer != null)
            analyzer.BindOwnerPuzzle(this);

        RefreshAllView();

        if (IsSolved)
            ApplySolvedPresentation();
    }

    public override void Render()
    {
        base.Render();

        bool solvedNow = IsSolved;
        int currentHash = BuildViewStateHash();

        if (_lastRenderedSolved != solvedNow || _lastRenderedViewHash != currentHash)
        {
            if (solvedNow)
                ApplySolvedPresentation();
            else
                RefreshAllView();

            _lastRenderedSolved = solvedNow;
            _lastRenderedViewHash = currentHash;
        }
    }

    /// <summary>
    /// answer seed를 받아 정답 데이터와 퍼즐 상태를 초기화한다.
    /// </summary>
    public void ApplyAnswerSeed(int seed)
    {
        _answerData = ReagentAnswerGenerator.Generate(seed);
        _hasAnswerSeedLocal = true;

        if (HasStateAuthority)
        {
            NetSeedApplied = true;
            ResetPuzzleState();
        }
        else
        {
            RefreshAllView();
        }

        Log($"정답 시드 적용 완료 | seed={seed}");
    }

    /// <summary>
    /// 이 퍼즐이 성공 후 표시할 3단계 힌트 데이터를 세팅한다.
    /// </summary>
    public void SetStage3HintData(FinalCodeHintData hintData)
    {
        _stage3HintData = hintData;
        _hasStage3HintData = hintData != null;

        if (craftView != null && _hasStage3HintData)
            craftView.ApplyStage3Hint(_stage3HintData);

        if (IsSolved)
            ApplySolvedPresentation();
    }

    /// <summary>
    /// 퍼즐 전체 상태를 초기화한다.
    /// </summary>
    public void ResetPuzzleState()
    {
        if (!HasStateAuthority)
            return;

        ResetRecipeSelectionState();
        ResetCraftState();
        RefreshAllView();
    }

    /// <summary>
    /// 시약 슬롯 선택 상태를 초기화한다.
    /// </summary>
    private void ResetRecipeSelectionState()
    {
        SetNetSlotValue(0, ReagentType.None);
        SetNetSlotValue(1, ReagentType.None);
        SetNetSlotValue(2, ReagentType.None);

        SetNetSlotLocked(0, false);
        SetNetSlotLocked(1, false);
        SetNetSlotLocked(2, false);

        NetEditingSlotIndex = 0;
        SetNetSlotValue(0, ReagentType.ReagentA);
    }

    /// <summary>
    /// 제조 진행 상태를 초기화한다.
    /// </summary>
    private void ResetCraftState()
    {
        if (_craftRoutine != null)
        {
            StopCoroutine(_craftRoutine);
            _craftRoutine = null;
        }

        NetIsCrafting = false;
        NetIsCraftStartPending = false;
        NetCurrentProgressIndex = 0;
        NetCountdownNumber = 0;
        NetHasSpawnedCraftedItem = false;

        ClearProgressActionStates();

        _playerActionInputs.Clear();
        _inputUsedProgressIndices.Clear();
        _wasCraftInterruptedByCorrectAnalyze = false;
    }

    public void MoveCurrentSlotSelectionLeft()
    {
        if (!HasStateAuthority)
            return;

        if (!CanEditCurrentSlot())
            return;

        int slotIndex = NetEditingSlotIndex;
        ReagentType current = GetNetSlotValue(slotIndex);
        SetNetSlotValue(slotIndex, GetPreviousReagentType(current));

        //TODO_Sound - 시약 슬롯 좌우 변경

        Log($"현재 슬롯 좌측 변경 | slot={slotIndex} | value={GetNetSlotValue(slotIndex)}");
    }

    public void MoveCurrentSlotSelectionRight()
    {
        if (!HasStateAuthority)
            return;

        if (!CanEditCurrentSlot())
            return;

        int slotIndex = NetEditingSlotIndex;
        ReagentType current = GetNetSlotValue(slotIndex);
        SetNetSlotValue(slotIndex, GetNextReagentType(current));

        //TODO_Sound - 시약 슬롯 좌우 변경

        Log($"현재 슬롯 우측 변경 | slot={slotIndex} | value={GetNetSlotValue(slotIndex)}");
    }

    public void ConfirmCurrentSlotSelection()
    {
        if (!HasStateAuthority)
            return;

        if (!CanEditCurrentSlot())
            return;

        SetNetSlotLocked(NetEditingSlotIndex, true);

        //TODO_Sound - 시약 슬롯 선택 확정

        if (NetEditingSlotIndex < recipeSlotCount - 1)
        {
            NetEditingSlotIndex++;

            if (GetNetSlotValue(NetEditingSlotIndex) == ReagentType.None)
                SetNetSlotValue(NetEditingSlotIndex, ReagentType.ReagentA);
        }
        else
        {
            NetEditingSlotIndex = recipeSlotCount;
        }

        Log($"현재 슬롯 확정 | nextSlot={NetEditingSlotIndex}");
    }

    public bool CanEditCurrentSlot()
    {
        if (!NetSeedApplied && !_hasAnswerSeedLocal)
            return false;

        if (IsSolved)
            return false;

        if (NetIsCrafting || NetIsCraftStartPending)
            return false;

        if (NetEditingSlotIndex < 0 || NetEditingSlotIndex >= recipeSlotCount)
            return false;

        return true;
    }

    public bool CanStartCraft()
    {
        if (!NetSeedApplied && !_hasAnswerSeedLocal)
            return false;

        if (IsSolved)
            return false;

        if (NetIsCrafting || NetIsCraftStartPending)
            return false;

        if (!IsAllRecipeSlotsConfirmed())
            return false;

        if (NetHasSpawnedCraftedItem)
            return false;

        return true;
    }

    public void TryStartCraft()
    {
        if (!HasStateAuthority)
            return;

        if (!CanStartCraft())
            return;

        //TODO_Sound - 시약 제조 시작 버튼

        ResetCraftState();
        _craftRoutine = StartCoroutine(CoStartCraftProcess());

        Log("제조 시작 요청");
    }

    /// <summary>
    /// 제조 시작 버튼 입력 후 3,2,1 카운트다운을 거쳐 제조 프로그레스를 진행한다.
    /// </summary>
    private IEnumerator CoStartCraftProcess()
    {
        NetIsCraftStartPending = true;

        for (int number = countdownStartNumber; number >= 1; number--)
        {
            NetCountdownNumber = number;

            //TODO_Sound - 시약 제조 카운트다운 틱

            yield return new WaitForSeconds(countdownStepSeconds);
        }

        NetCountdownNumber = 0;
        NetIsCraftStartPending = false;

        NetIsCrafting = true;

        for (int step = 1; step <= progressStepCount; step++)
        {
            if (_wasCraftInterruptedByCorrectAnalyze)
                yield break;

            NetCurrentProgressIndex = step;

            //TODO_Sound - 시약 프로그레스 한 칸 진행

            yield return new WaitForSeconds(progressStepInterval);
        }

        NetIsCrafting = false;
        _craftRoutine = null;

        CompleteCrafting();
    }

    public void TryRecordHeatInput()
    {
        TryRecordActionInput(ReagentActionType.Heat);
    }

    public void TryRecordCoolInput()
    {
        TryRecordActionInput(ReagentActionType.Cool);
    }

    private void TryRecordActionInput(ReagentActionType actionType)
    {
        if (!HasStateAuthority)
            return;

        if (!CanRecordActionInput())
            return;

        if (_inputUsedProgressIndices.Contains(NetCurrentProgressIndex))
            return;

        ReagentAnswerGenerator.ReagentActionStep step = new ReagentAnswerGenerator.ReagentActionStep
        {
            ProgressIndex = NetCurrentProgressIndex,
            ActionType = actionType
        };

        _playerActionInputs.Add(step);
        _inputUsedProgressIndices.Add(NetCurrentProgressIndex);

        SetProgressActionState(NetCurrentProgressIndex - 1, ToProgressActionState(actionType));

        if (actionType == ReagentActionType.Heat)
        {
            //TODO_Sound - 시약 가열 입력
        }
        else if (actionType == ReagentActionType.Cool)
        {
            //TODO_Sound - 시약 냉각 입력
        }

        Log($"행동 입력 기록 | step={NetCurrentProgressIndex} | action={actionType}");
    }

    public bool CanRecordActionInput()
    {
        if (!NetIsCrafting)
            return false;

        if (NetCurrentProgressIndex <= 0 || NetCurrentProgressIndex > progressStepCount)
            return false;

        if (_playerActionInputs.Count >= 3)
            return false;

        if (_inputUsedProgressIndices.Contains(NetCurrentProgressIndex))
            return false;

        if (IsSolved)
            return false;

        return true;
    }

    private void CompleteCrafting()
    {
        bool isCorrectRecipe = IsRecipeCorrect();
        bool isCorrectActions = AreActionInputsCorrect();
        bool isCorrectResult = isCorrectRecipe && isCorrectActions;

        //TODO_Sound - 시약 제조 완료

        SpawnCraftResultItem(isCorrectResult);

        Log($"제조 완료 | recipe={isCorrectRecipe} | action={isCorrectActions} | result={isCorrectResult}");
    }

    private bool IsRecipeCorrect()
    {
        if (_answerData == null || _answerData.RecipeSequence.Count < recipeSlotCount)
            return false;

        for (int i = 0; i < recipeSlotCount; i++)
        {
            if (GetNetSlotValue(i) != _answerData.RecipeSequence[i])
                return false;
        }

        return true;
    }

    private bool AreActionInputsCorrect()
    {
        if (_answerData == null)
            return false;

        if (_playerActionInputs.Count != _answerData.ActionSteps.Count)
            return false;

        for (int i = 0; i < _answerData.ActionSteps.Count; i++)
        {
            ReagentAnswerGenerator.ReagentActionStep expected = _answerData.ActionSteps[i];
            ReagentAnswerGenerator.ReagentActionStep actual = _playerActionInputs[i];

            if (expected.ProgressIndex != actual.ProgressIndex)
                return false;

            if (expected.ActionType != actual.ActionType)
                return false;
        }

        return true;
    }

    private void SpawnCraftResultItem(bool isCorrect)
    {
        if (Runner == null)
            return;

        if (craftedItemSpawnPoint == null)
            return;

        if (_spawnedCraftedItem != null)
            return;

        NetworkObject prefab = isCorrect ? correctReagentPrefab : wrongReagentPrefab;
        if (prefab == null)
            return;

        NetworkObject spawned = Runner.Spawn(
            prefab,
            craftedItemSpawnPoint.position,
            craftedItemSpawnPoint.rotation,
            null);

        if (spawned == null)
            return;

        CraftedReagentItem craftedItem = spawned.GetComponent<CraftedReagentItem>();
        if (craftedItem != null)
        {
            ReagentType visualType = isCorrect
                ? _answerData.RecipeSequence[recipeSlotCount - 1]
                : GetNetSlotValue(recipeSlotCount - 1);

            craftedItem.InitializeFromCraftResult(this, visualType, isCorrect);
            _spawnedCraftedItem = craftedItem;
            NetHasSpawnedCraftedItem = true;
        }
    }

    public void HandleCraftedItemPickedUp(CraftedReagentItem item, PlayerController actor)
    {
        if (!HasStateAuthority)
            return;

        if (item == null)
            return;

        if (_spawnedCraftedItem != item)
            return;

        _spawnedCraftedItem = null;
        NetHasSpawnedCraftedItem = false;
        ResetPuzzleState();

        Log($"결과 시약 회수됨 | actor={(actor != null ? actor.name : "null")}");
    }

    public void HandleAnalyzerAcceptedCorrectReagent(CraftedReagentItem item)
    {
        if (!HasStateAuthority)
            return;

        if (item == null)
            return;

        if (NetIsCrafting || NetIsCraftStartPending)
            StopCraftingForCorrectAnalyze();

        MarkSolved();
        ApplySolvedPresentation();

        Log("정답 시약 판별 성공");
    }

    public void HandleAnalyzerAcceptedWrongReagent(CraftedReagentItem item)
    {
        if (!HasStateAuthority)
            return;

        Log("오답 시약 판별 실패");
    }

    private void StopCraftingForCorrectAnalyze()
    {
        _wasCraftInterruptedByCorrectAnalyze = true;

        if (_craftRoutine != null)
        {
            StopCoroutine(_craftRoutine);
            _craftRoutine = null;
        }

        NetIsCrafting = false;
        NetIsCraftStartPending = false;
        NetCurrentProgressIndex = 0;
        NetCountdownNumber = 0;
    }

    private bool IsAllRecipeSlotsConfirmed()
    {
        for (int i = 0; i < recipeSlotCount; i++)
        {
            if (!GetNetSlotLocked(i))
                return false;
        }

        return true;
    }

    private void RefreshAllView()
    {
        if (craftView == null)
            return;

        craftView.ResetToDefault();
        craftView.ApplyRecipeSlotStates(BuildCurrentSlotArray());

        bool isEditingSlot = NetEditingSlotIndex >= 0 && NetEditingSlotIndex < recipeSlotCount;
        bool canShowStartButton = IsAllRecipeSlotsConfirmed() &&
                                  !NetIsCrafting &&
                                  !NetIsCraftStartPending &&
                                  !NetHasSpawnedCraftedItem &&
                                  !IsSolved;

        if (isEditingSlot)
            craftView.SetEditingSlotControls(NetEditingSlotIndex);
        else
            craftView.ClearEditingSlotControls();

        craftView.SetStartCraftButtonVisible(canShowStartButton);
        craftView.SetCountdownNumber(NetCountdownNumber);
        craftView.SetActionButtonsVisible(NetIsCrafting);

        BuildProgressActionStateCache();
        craftView.ApplyProgressPresentation(NetCurrentProgressIndex, _progressActionStatesCache);
    }

    private ReagentType GetNextReagentType(ReagentType current)
    {
        return current switch
        {
            ReagentType.None => ReagentType.ReagentA,
            ReagentType.ReagentA => ReagentType.ReagentB,
            ReagentType.ReagentB => ReagentType.ReagentC,
            ReagentType.ReagentC => ReagentType.ReagentD,
            ReagentType.ReagentD => ReagentType.ReagentE,
            ReagentType.ReagentE => ReagentType.ReagentF,
            ReagentType.ReagentF => ReagentType.ReagentA,
            _ => ReagentType.ReagentA
        };
    }

    private ReagentType GetPreviousReagentType(ReagentType current)
    {
        return current switch
        {
            ReagentType.None => ReagentType.ReagentF,
            ReagentType.ReagentA => ReagentType.ReagentF,
            ReagentType.ReagentB => ReagentType.ReagentA,
            ReagentType.ReagentC => ReagentType.ReagentB,
            ReagentType.ReagentD => ReagentType.ReagentC,
            ReagentType.ReagentE => ReagentType.ReagentD,
            ReagentType.ReagentF => ReagentType.ReagentE,
            _ => ReagentType.ReagentA
        };
    }

    private void ApplySolvedPresentation()
    {
        if (craftView == null)
            return;

        if (_hasStage3HintData)
            craftView.ApplyStage3Hint(_stage3HintData);

        if (showStage3HintImmediately)
        {
            craftView.ShowStage3HintState();
            return;
        }

        craftView.ShowSolvedState();
    }

    protected override void HandleSolvedStateChanged()
    {
        if (!IsSolved)
            return;

        ApplySolvedPresentation();
    }

    protected override void ServerInteract(PlayerController actor)
    {
        // 루트 직접 상호작용 없음
    }

    private ReagentType[] BuildCurrentSlotArray()
    {
        return new[]
        {
            GetNetSlotValue(0),
            GetNetSlotValue(1),
            GetNetSlotValue(2)
        };
    }

    private int BuildViewStateHash()
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + NetEditingSlotIndex;
            hash = hash * 31 + NetCurrentProgressIndex;
            hash = hash * 31 + (NetIsCrafting ? 1 : 0);
            hash = hash * 31 + (NetIsCraftStartPending ? 1 : 0);
            hash = hash * 31 + NetCountdownNumber;
            hash = hash * 31 + (NetHasSpawnedCraftedItem ? 1 : 0);
            hash = hash * 31 + NetSlot0Value;
            hash = hash * 31 + NetSlot1Value;
            hash = hash * 31 + NetSlot2Value;
            hash = hash * 31 + (NetSlot0Locked ? 1 : 0);
            hash = hash * 31 + (NetSlot1Locked ? 1 : 0);
            hash = hash * 31 + (NetSlot2Locked ? 1 : 0);
            hash = hash * 31 + NetProgressAction0;
            hash = hash * 31 + NetProgressAction1;
            hash = hash * 31 + NetProgressAction2;
            hash = hash * 31 + NetProgressAction3;
            hash = hash * 31 + NetProgressAction4;
            hash = hash * 31 + NetProgressAction5;
            hash = hash * 31 + NetProgressAction6;
            hash = hash * 31 + NetProgressAction7;
            hash = hash * 31 + NetProgressAction8;
            hash = hash * 31 + NetProgressAction9;
            return hash;
        }
    }

    private ReagentType GetNetSlotValue(int slotIndex)
    {
        return slotIndex switch
        {
            0 => (ReagentType)NetSlot0Value,
            1 => (ReagentType)NetSlot1Value,
            2 => (ReagentType)NetSlot2Value,
            _ => ReagentType.None
        };
    }

    private void SetNetSlotValue(int slotIndex, ReagentType value)
    {
        switch (slotIndex)
        {
            case 0:
                NetSlot0Value = (int)value;
                break;
            case 1:
                NetSlot1Value = (int)value;
                break;
            case 2:
                NetSlot2Value = (int)value;
                break;
        }
    }

    private bool GetNetSlotLocked(int slotIndex)
    {
        return slotIndex switch
        {
            0 => NetSlot0Locked,
            1 => NetSlot1Locked,
            2 => NetSlot2Locked,
            _ => false
        };
    }

    private void SetNetSlotLocked(int slotIndex, bool value)
    {
        switch (slotIndex)
        {
            case 0:
                NetSlot0Locked = value;
                break;
            case 1:
                NetSlot1Locked = value;
                break;
            case 2:
                NetSlot2Locked = value;
                break;
        }
    }

    /// <summary>
    /// 프로그레스 액션 상태를 전부 초기화한다.
    /// 0=None, 1=Heat, 2=Cool.
    /// </summary>
    private void ClearProgressActionStates()
    {
        NetProgressAction0 = 0;
        NetProgressAction1 = 0;
        NetProgressAction2 = 0;
        NetProgressAction3 = 0;
        NetProgressAction4 = 0;
        NetProgressAction5 = 0;
        NetProgressAction6 = 0;
        NetProgressAction7 = 0;
        NetProgressAction8 = 0;
        NetProgressAction9 = 0;
    }

    /// <summary>
    /// 특정 프로그레스 칸의 액션 상태를 저장한다.
    /// progressIndex는 0~9 기준이다.
    /// </summary>
    private void SetProgressActionState(int progressIndex, int actionState)
    {
        switch (progressIndex)
        {
            case 0: NetProgressAction0 = actionState; break;
            case 1: NetProgressAction1 = actionState; break;
            case 2: NetProgressAction2 = actionState; break;
            case 3: NetProgressAction3 = actionState; break;
            case 4: NetProgressAction4 = actionState; break;
            case 5: NetProgressAction5 = actionState; break;
            case 6: NetProgressAction6 = actionState; break;
            case 7: NetProgressAction7 = actionState; break;
            case 8: NetProgressAction8 = actionState; break;
            case 9: NetProgressAction9 = actionState; break;
        }
    }

    /// <summary>
    /// 특정 프로그레스 칸의 액션 상태를 반환한다.
    /// progressIndex는 0~9 기준이다.
    /// </summary>
    private int GetProgressActionState(int progressIndex)
    {
        return progressIndex switch
        {
            0 => NetProgressAction0,
            1 => NetProgressAction1,
            2 => NetProgressAction2,
            3 => NetProgressAction3,
            4 => NetProgressAction4,
            5 => NetProgressAction5,
            6 => NetProgressAction6,
            7 => NetProgressAction7,
            8 => NetProgressAction8,
            9 => NetProgressAction9,
            _ => 0
        };
    }

    /// <summary>
    /// View에 넘길 프로그레스 액션 상태 캐시를 만든다.
    /// </summary>
    private void BuildProgressActionStateCache()
    {
        _progressActionStatesCache.Clear();

        for (int i = 0; i < progressStepCount; i++)
            _progressActionStatesCache.Add(GetProgressActionState(i));
    }

    /// <summary>
    /// ReagentActionType을 View 표시용 정수 상태로 변환한다.
    /// 0=None, 1=Heat, 2=Cool.
    /// </summary>
    private int ToProgressActionState(ReagentActionType actionType)
    {
        return actionType == ReagentActionType.Heat ? 1 : 2;
    }

    private void Log(string message)
    {
        if (!enableDebugLog)
            return;

        Debug.Log($"[ReagentCraftPuzzle] {message}", this);
    }
}