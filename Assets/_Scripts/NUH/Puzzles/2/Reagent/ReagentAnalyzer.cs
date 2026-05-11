using Fusion;
using System.Collections;
using UnityEngine;

/// <summary>
/// 2-3 시약 제조 퍼즐의 판별기 로직.
/// 
/// 멀티플레이 보강
/// - 시약 삽입 시 바닥 드랍을 거치지 않는다.
/// - 현재 삽입 아이템을 NetworkId로 기억한다.
/// - 랜턴 상태와 판별기 잠금 상태를 네트워크로 공유한다.
/// - 실패 점멸은 모든 클라이언트가 Render에서 동일하게 계산해 표시한다.
/// </summary>
public class ReagentAnalyzer : NetworkBehaviour, IInteractable, IChildPuzzleInteractable
{
    [Header("참조")]
    [SerializeField] private ReagentCraftPuzzle ownerPuzzle; // 소속 퍼즐 본체
    [SerializeField] private Transform insertedItemAnchor; // 시약이 삽입되어 고정될 위치
    [SerializeField] private SpriteRenderer lanternRenderer; // 판별기 랜턴 SpriteRenderer

    [Header("설정")]
    [SerializeField] private int interactableId; // 자식 상호작용 ID
    [SerializeField] private float analyzeDuration = 2f; // 검사 처리 시간
    [SerializeField] private float failBlinkInterval = 0.12f; // 실패 랜턴 점멸 간격
    [SerializeField] private int failBlinkCount = 3; // 실패 랜턴 점멸 횟수
    [SerializeField] private string promptText = "시약 판별"; // 상호작용 프롬프트
    [SerializeField] private bool enableDebugLog = true; // 디버그 로그 출력 여부

    [Header("랜턴 색")]
    [SerializeField] private Color lanternIdleColor = Color.white; // 기본 흰색
    [SerializeField] private Color lanternSuccessColor = Color.green; // 성공 초록색
    [SerializeField] private Color lanternFailColor = Color.red; // 실패 빨간색

    [Networked] private NetworkBool NetIsChecking { get; set; } // 현재 검사 중인지 여부
    [Networked] private int NetLanternState { get; set; } // 0=기본, 1=성공, 2=실패점멸중
    [Networked] private NetworkBool NetLockedByCorrectReagent { get; set; } // 정답 시약 성공으로 판별기가 잠겼는지 여부
    [Networked] private NetworkId NetInsertedItemId { get; set; } // 현재 삽입된 시약 NetworkId
    [Networked] private int NetFailBlinkStartTick { get; set; } // 실패 점멸 시작 tick

    private CraftedReagentItem _insertedItem; // 현재 삽입된 시약 참조
    private Coroutine _analyzeRoutine; // 검사 코루틴 참조
    private Coroutine _failBlinkFinishRoutine; // 실패 점멸 종료 타이밍 관리용 코루틴
    private NetworkId _lastResolvedInsertedItemId; // 삽입 아이템 해석 캐시

    public int InteractableId => interactableId; // 외부에서 읽는 자식 상호작용 ID

    public override void Spawned()
    {
        if (HasStateAuthority)
        {
            NetIsChecking = false; // 시작 시 검사 중 아님
            NetLanternState = 0; // 시작 시 기본 랜턴 상태
            NetLockedByCorrectReagent = false; // 시작 시 잠금 아님
            NetInsertedItemId = default; // 시작 시 삽입 아이템 없음
            NetFailBlinkStartTick = 0; // 시작 시 점멸 시작 tick 없음
        }

        _lastResolvedInsertedItemId = default; // 해석 캐시 초기화
        ResolveInsertedItemReference(); // 삽입 아이템 참조 복구 시도
        ApplyLanternStateImmediate(); // 현재 랜턴 상태 반영
    }

    public override void Render()
    {
        ResolveInsertedItemReference(); // 삽입 아이템 참조 복구
        ApplyLanternStateImmediate(); // 현재 랜턴 상태 반영
    }

    /// <summary>
    /// 외부에서 퍼즐 본체를 연결한다.
    /// </summary>
    public void BindOwnerPuzzle(ReagentCraftPuzzle puzzle)
    {
        ownerPuzzle = puzzle; // 퍼즐 본체 참조 연결
    }

    /// <summary>
    /// 현재 플레이어가 판별기와 상호작용 가능한지 검사한다.
    /// </summary>
    public bool CanInteract(PlayerController actor)
    {
        if (actor == null)
            return false;

        if (ownerPuzzle == null)
            return false;

        if (actor.NetPlayerState != PlayerState.Normal)
            return false;

        if (NetIsChecking)
            return false;

        if (NetLockedByCorrectReagent)
            return false;

        if (actor.NetRightHandItem == null)
            return false;

        CraftedReagentItem reagentItem = actor.NetRightHandItem.GetComponent<CraftedReagentItem>();
        if (reagentItem == null)
            return false;

        return true;
    }

    /// <summary>
    /// 판별기 상호작용 시 현재 오른손 시약을 삽입하고 검사 시작한다.
    /// </summary>
    public void Interact(PlayerController actor)
    {
        if (!HasStateAuthority)
            return;

        if (!CanInteract(actor))
            return;

        CraftedReagentItem reagentItem = actor.NetRightHandItem.GetComponent<CraftedReagentItem>();
        if (reagentItem == null)
            return;

        if (!actor.ServerClearRightHandItemWithoutDrop())
            return; // 손 참조 해제 책임은 PlayerController가 갖는다

        reagentItem.ServerInsertIntoAnalyzer(insertedItemAnchor); // 바로 판별기 앵커로 이동

        _insertedItem = reagentItem; // 현재 삽입 시약 참조 저장
        NetInsertedItemId = reagentItem.Object.Id; // 네트워크에 삽입 시약 ID 기록

        //TODO_Sound - 시약 검사기 검사 시작
        if (ownerPuzzle != null && ownerPuzzle.audioModule != null)
        {
            ownerPuzzle.audioModule.PlaySound(SoundType.MechanicalMove); // 철컥 들어가면서 웅- 돌아가는 소리
        }

        if (_analyzeRoutine != null)
            StopCoroutine(_analyzeRoutine);

        _analyzeRoutine = StartCoroutine(CoAnalyzeRoutine(reagentItem)); // 2초 검사 시작
    }

    /// <summary>
    /// 현재 상호작용 프롬프트를 반환한다.
    /// </summary>
    public string GetPromptText(PlayerController actor)
    {
        return promptText;
    }

    /// <summary>
    /// 시약 삽입 후 2초 검사 루틴을 수행한다.
    /// </summary>
    private IEnumerator CoAnalyzeRoutine(CraftedReagentItem item)
    {
        NetIsChecking = true; // 검사 중 상태 시작
        NetLanternState = 0; // 검사 중에는 기본 흰색 유지

        yield return new WaitForSeconds(analyzeDuration); // 2초 검사 대기

        NetIsChecking = false; // 검사 종료

        if (item == null)
            yield break;

        if (item.IsCorrectReagent())
        {
            HandleAnalyzeSuccess(item); // 정답 시약 성공 처리
            yield break;
        }

        HandleAnalyzeFailure(item); // 오답 시약 실패 처리
    }

    /// <summary>
    /// 정답 시약 성공 처리.
    /// </summary>
    private void HandleAnalyzeSuccess(CraftedReagentItem item)
    {
        NetLockedByCorrectReagent = true; // 더 이상 다른 시약 못 넣게 잠금
        NetLanternState = 1; // 성공 랜턴 상태
        ApplyLanternStateImmediate(); // 즉시 반영

        if (ownerPuzzle != null)
            ownerPuzzle.HandleAnalyzerAcceptedCorrectReagent(item); // 퍼즐 본체에 성공 통보

        Log("정답 시약 검사 성공");
    }

    /// <summary>
    /// 오답 시약 실패 처리.
    /// 
    /// 핵심
    /// - 점멸 자체를 서버 코루틴의 로컬 색 변경에 맡기지 않고
    /// - 시작 tick을 네트워크로 기록해서 모든 클라이언트가 Render에서 동일하게 계산한다.
    /// </summary>
    private void HandleAnalyzeFailure(CraftedReagentItem item)
    {
        NetLanternState = 2; // 실패 점멸 상태 시작
        NetFailBlinkStartTick = Runner.Tick; // 점멸 시작 tick 기록

        if (_failBlinkFinishRoutine != null)
            StopCoroutine(_failBlinkFinishRoutine);

        _failBlinkFinishRoutine = StartCoroutine(CoFinishFailBlink()); // 점멸 종료 타이밍만 관리

        if (ownerPuzzle != null)
            ownerPuzzle.HandleAnalyzerAcceptedWrongReagent(item); // 퍼즐 본체에 실패 통보

        item.ServerConsumeInAnalyzer(); // 오답 시약 제거
        _insertedItem = null; // 현재 삽입 시약 참조 해제
        NetInsertedItemId = default; // 삽입 시약 ID 초기화
        _lastResolvedInsertedItemId = default; // 해석 캐시 초기화

        Log("오답 시약 검사 실패");
    }

    /// <summary>
    /// 실패 점멸 상태를 일정 시간 유지한 뒤 기본 흰색으로 복귀시킨다.
    /// 
    /// 주의
    /// - 실제 깜빡임 색 변경은 Render에서 계산한다.
    /// - 여기서는 종료 시점만 관리한다.
    /// </summary>
    private IEnumerator CoFinishFailBlink()
    {
        float totalDuration = failBlinkInterval * 2f * failBlinkCount; // 빨강/흰색 한 쌍 기준 총 점멸 시간 계산
        yield return new WaitForSeconds(totalDuration); // 점멸 전체 시간만큼 대기

        NetLanternState = 0; // 기본 상태로 복귀
        ApplyLanternStateImmediate(); // 흰색 즉시 적용
        _failBlinkFinishRoutine = null; // 종료 코루틴 참조 정리
    }

    /// <summary>
    /// 삽입된 시약 NetworkId를 실제 객체 참조로 복구한다.
    /// </summary>
    private void ResolveInsertedItemReference()
    {
        if (NetInsertedItemId == default)
        {
            _insertedItem = null;
            _lastResolvedInsertedItemId = default;
            return;
        }

        if (_lastResolvedInsertedItemId == NetInsertedItemId && _insertedItem != null)
            return; // 이미 같은 NetworkId를 해석한 상태면 재탐색 안 함

        if (!Runner.TryFindObject(NetInsertedItemId, out NetworkObject found))
            return;

        _insertedItem = found.GetComponent<CraftedReagentItem>();
        _lastResolvedInsertedItemId = NetInsertedItemId;
    }

    /// <summary>
    /// 현재 네트워크 랜턴 상태를 즉시 SpriteRenderer에 반영한다.
    /// 
    /// 중요
    /// - 실패 점멸 상태(2)는 모든 클라이언트가 Render에서 같은 tick 기준으로 계산해서 표시한다.
    /// </summary>
    private void ApplyLanternStateImmediate()
    {
        if (lanternRenderer == null)
            return;

        switch (NetLanternState)
        {
            case 1:
                lanternRenderer.color = lanternSuccessColor; // 성공이면 초록색
                break;

            case 2:
                ApplyFailBlinkColor(); // 실패 점멸은 Render에서 계산
                break;

            default:
                lanternRenderer.color = lanternIdleColor; // 기본은 흰색
                break;
        }
    }

    /// <summary>
    /// 실패 점멸 상태일 때 현재 tick 기준으로 빨강/흰색을 계산해 적용한다.
    /// </summary>
    private void ApplyFailBlinkColor()
    {
        if (Runner == null)
        {
            lanternRenderer.color = lanternIdleColor; // Runner 없으면 안전하게 흰색 유지
            return;
        }

        int ticksSinceStart = Runner.Tick - NetFailBlinkStartTick; // 점멸 시작 후 지난 tick 수
        if (ticksSinceStart < 0)
        {
            lanternRenderer.color = lanternIdleColor; // 비정상 값이면 흰색 유지
            return;
        }

        float elapsedSeconds = ticksSinceStart * Runner.DeltaTime; // 경과 시간을 초 단위로 변환
        int phaseIndex = Mathf.FloorToInt(elapsedSeconds / failBlinkInterval); // 현재 몇 번째 점멸 구간인지 계산

        if (phaseIndex >= failBlinkCount * 2)
        {
            lanternRenderer.color = lanternIdleColor; // 점멸 구간이 끝났으면 흰색 유지
            return;
        }

        bool showFailColor = (phaseIndex % 2) == 0; // 짝수 구간은 빨강, 홀수 구간은 흰색
        lanternRenderer.color = showFailColor ? lanternFailColor : lanternIdleColor; // 현재 구간 색상 적용
    }

    /// <summary>
    /// 일반 디버그 로그 출력.
    /// </summary>
    private void Log(string message)
    {
        if (!enableDebugLog)
            return;

        Debug.Log($"[ReagentAnalyzer] {message}", this);
    }
}