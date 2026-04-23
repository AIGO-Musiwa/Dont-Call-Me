using Fusion;
using UnityEngine;

/// <summary>
/// 2-3 시약 제조 퍼즐에서 생성되는 결과 시약 아이템.
/// 
/// 역할
/// - ItemObject 기반으로 월드에 스폰되는 시약 아이템이다.
/// - 정답 시약인지 내부적으로 보관한다.
/// - 플레이어가 실제로 시약을 손에 장착했을 때 원 퍼즐에 회수 사실을 알린다.
/// - 판별기에 삽입된 상태에서는 다시 줍지 못하게 막는다.
/// 
/// 핵심
/// - 부모 ItemObject는 NetIsEquipped만 보고 월드/장착 표현을 바꾼다.
/// - 하지만 시약은 "판별기 삽입 상태"라는 별도 상태가 있으므로,
///   자식 클래스에서 그 상태를 최종 우선으로 다시 덮어써야 한다.
/// </summary>
public class CraftedReagentItem : ItemObject
{
    [Header("시약 내부 정보")]
    [SerializeField] private bool isCorrectReagent = false; // 이 시약이 정답 시약인지 여부

    [Header("월드 표시 보강")]
    [SerializeField] private Renderer[] worldRenderers; // 월드에서 보여야 하는 렌더러들

    [Networked] public NetworkBool NetIsCorrectReagent { get; private set; } // 네트워크 동기화용 정답 시약 여부
    [Networked] public NetworkBool NetIsInsertedInAnalyzer { get; private set; } // 판별기 안에 삽입된 상태인지 여부

    private ReagentCraftPuzzle _sourcePuzzle; // 이 시약을 생성한 원 퍼즐 참조
    private bool _wasPickupNotified; // 회수 통보를 이미 보냈는지 여부

    private int _initialWorldLayer; // 이 프리팹이 처음 생성될 때의 원래 레이어
    private bool _lastRenderedInsertedState; // 이전 렌더 시점의 판별기 삽입 상태 캐시
    private bool _lastRenderedEquippedState; // 이전 렌더 시점의 장착 상태 캐시

    /// <summary>
    /// 기본 아이템 설정과 렌더러/초기 레이어 캐시를 준비한다.
    /// </summary>
    protected override void Awake()
    {
        itemType = ItemType.Reagent; // 아이템 타입을 시약으로 지정
        isRoleItem = false; // 역할 아이템 아님
        itemName = "시약"; // 판별 전까지 공통 이름 유지

        if (worldRenderers == null || worldRenderers.Length == 0)
            worldRenderers = GetComponentsInChildren<Renderer>(true); // 월드 렌더러 자동 수집

        _initialWorldLayer = gameObject.layer; // 프리팹 원래 레이어를 캐시

        base.Awake(); // 부모 초기화 실행
    }

    /// <summary>
    /// 네트워크 스폰 시 기본 네트워크 상태와 월드 표시 상태를 반영한다.
    /// </summary>
    public override void Spawned()
    {
        base.Spawned(); // 부모 스폰 로직 실행

        if (HasStateAuthority)
        {
            NetIsCorrectReagent = isCorrectReagent; // 정답 시약 여부 초기 반영
            NetIsInsertedInAnalyzer = false; // 시작 시 판별기 삽입 상태 아님
        }

        ApplyReagentVisualState(); // 내부 시약 상태를 로컬 캐시에 반영

        _lastRenderedInsertedState = NetIsInsertedInAnalyzer; // 삽입 상태 캐시 초기화
        _lastRenderedEquippedState = NetIsEquipped; // 장착 상태 캐시 초기화

        ApplyWorldPresentationState(); // 스폰 직후 월드에 보이는 상태 강제
    }

    /// <summary>
    /// 렌더 단계에서 시약 시각 상태와 월드 표시 상태를 갱신한다.
    /// </summary>
    public override void Render()
    {
        base.Render(); // 부모 렌더 로직 실행
        ApplyReagentVisualState(); // 시약 내부 상태를 로컬 캐시에 반영

        if (_lastRenderedInsertedState != NetIsInsertedInAnalyzer ||
            _lastRenderedEquippedState != NetIsEquipped)
        {
            ApplyWorldPresentationState(); // 상태 변경 시 월드 표시 보강
            _lastRenderedInsertedState = NetIsInsertedInAnalyzer; // 삽입 상태 캐시 갱신
            _lastRenderedEquippedState = NetIsEquipped; // 장착 상태 캐시 갱신
        }
    }

    /// <summary>
    /// 부모 ItemObject의 기본 표현 로직을 수행한 뒤,
    /// 판별기 삽입 상태라면 그 상태를 최종 우선으로 다시 덮어쓴다.
    /// 
    /// 핵심:
    /// - 부모는 NetIsEquipped=false면 "월드 드랍 아이템"으로 판단해
    ///   Rigidbody를 동적으로 돌리고 Collider를 다시 켠다.
    /// - 하지만 시약이 판별기 안에 꽂힌 상태라면 그 처리는 틀린 상태다.
    /// - 그래서 자식이 마지막에 삽입 상태를 다시 강제한다.
    /// </summary>
    protected override void ApplyPresentationState()
    {
        base.ApplyPresentationState(); // 부모 기본 월드/장착 표현 적용

        if (!NetIsInsertedInAnalyzer)
            return; // 판별기 삽입 상태가 아니면 부모 처리만 유지

        // 판별기 안에 들어간 시약은 모든 클라이언트에서 고정 상태여야 함
        if (_rigidbody != null)
        {
            if (!_rigidbody.isKinematic)
                _rigidbody.isKinematic = true; // 물리 시뮬레이션 끄기
        }

        if (_colliders != null)
        {
            for (int i = 0; i < _colliders.Length; i++)
            {
                if (_colliders[i] == null)
                    continue;

                if (_colliders[i].enabled)
                    _colliders[i].enabled = false; // 다시 줍지 못하게 비활성화
            }
        }

        SetLayerRecursively(gameObject, _initialWorldLayer); // 원래 월드 레이어 유지

        if (worldRenderers != null)
        {
            for (int i = 0; i < worldRenderers.Length; i++)
            {
                if (worldRenderers[i] == null)
                    continue;

                if (!worldRenderers[i].enabled)
                    worldRenderers[i].enabled = true; // 렌더러가 꺼졌다면 다시 켜기
            }
        }

        if (outlineComponent != null)
            outlineComponent.SetOutline(false); // 외곽선은 항상 끔
    }

    /// <summary>
    /// 제조 결과로 스폰된 직후, 원 퍼즐과 시약 정보를 세팅한다.
    /// </summary>
    public void InitializeFromCraftResult(ReagentCraftPuzzle sourcePuzzle, ReagentType visualType, bool isCorrect)
    {
        _sourcePuzzle = sourcePuzzle; // 원 퍼즐 참조 저장
        isCorrectReagent = isCorrect; // 로컬 정답 여부 저장
        _wasPickupNotified = false; // 회수 통보 상태 초기화

        if (HasStateAuthority)
        {
            NetIsCorrectReagent = isCorrect; // 네트워크 정답 여부 반영
            NetIsInsertedInAnalyzer = false; // 판별기 삽입 상태 해제
        }

        ApplyReagentVisualState(); // 내부 상태 로컬 반영
        ApplyWorldPresentationState(); // 스폰 직후 월드 아이템처럼 보이게 보강
    }

    /// <summary>
    /// 현재 플레이어가 이 시약과 상호작용 가능한지 검사한다.
    /// </summary>
    public override bool CanInteract(PlayerController actor)
    {
        if (NetIsInsertedInAnalyzer)
            return false; // 판별기 안에 들어간 시약은 줍기 불가

        if (!base.CanInteract(actor))
            return false; // 부모 공통 조건 탈락 시 상호작용 불가

        return true;
    }

    /// <summary>
    /// 시약 아이템 이름 프롬프트를 반환한다.
    /// 정답/오답을 드러내지 않고 항상 동일하게 보여준다.
    /// </summary>
    public override string GetPromptText(PlayerController actor)
    {
        if (actor != null && actor.NetRightHandItem != null)
            return "시약으로 교체";

        return "시약 줍기";
    }

    /// <summary>
    /// 실제로 플레이어 손에 장착이 완료되었을 때 호출된다.
    /// 이 시점에 원 퍼즐에 회수 통보를 보낸다.
    /// </summary>
    public override void OnEquipped(PlayerController holder)
    {
        base.OnEquipped(holder); // 부모 장착 처리 먼저 수행
        NotifyPickedUp(holder); // 실제 장착 완료 시점에 퍼즐 본체로 회수 통보
    }

    /// <summary>
    /// 시약을 집었음을 원 퍼즐에 한 번만 알린다.
    /// </summary>
    private void NotifyPickedUp(PlayerController actor)
    {
        if (_wasPickupNotified)
            return;

        _wasPickupNotified = true;

        if (_sourcePuzzle != null)
            _sourcePuzzle.HandleCraftedItemPickedUp(this, actor);
    }

    /// <summary>
    /// 판별기 삽입용 상태로 바로 옮긴다.
    /// </summary>
    public void ServerInsertIntoAnalyzer(Transform analyzerAnchor)
    {
        if (!HasStateAuthority)
            return;

        if (analyzerAnchor == null)
            return;

        ApplyDroppedState(); // 장착 상태만 해제, 바닥 드랍은 하지 않음

        transform.position = analyzerAnchor.position; // 판별기 앵커 위치로 이동
        transform.rotation = analyzerAnchor.rotation; // 판별기 앵커 회전으로 정렬

        if (_rigidbody != null)
        {
            _rigidbody.position = analyzerAnchor.position; // 물리 위치 동기화
            _rigidbody.rotation = analyzerAnchor.rotation; // 물리 회전 동기화
            _rigidbody.linearVelocity = Vector3.zero; // 속도 제거
            _rigidbody.angularVelocity = Vector3.zero; // 각속도 제거
            _rigidbody.isKinematic = true; // 판별기 안에서는 고정 상태
        }

        if (_colliders != null)
        {
            for (int i = 0; i < _colliders.Length; i++)
            {
                if (_colliders[i] == null)
                    continue;

                _colliders[i].enabled = false; // 판별기 안에서는 다시 줍지 못하게 비활성화
            }
        }

        if (outlineComponent != null)
            outlineComponent.SetOutline(false); // 외곽선 비활성화

        NetIsInsertedInAnalyzer = true; // 판별기 삽입 상태 기록
        ApplyWorldPresentationState(); // 판별기 안에서도 월드에 보이게 보강
    }

    /// <summary>
    /// 판별기 실패 시 이 시약을 소비(제거)한다.
    /// </summary>
    public void ServerConsumeInAnalyzer()
    {
        if (!HasStateAuthority)
            return;

        if (Runner == null || Object == null || !Object.IsValid)
            return;

        Runner.Despawn(Object);
    }

    /// <summary>
    /// 현재 시약이 정답 시약인지 반환한다.
    /// </summary>
    public bool IsCorrectReagent()
    {
        return NetIsCorrectReagent;
    }

    /// <summary>
    /// 원 퍼즐 참조를 반환한다.
    /// </summary>
    public ReagentCraftPuzzle GetSourcePuzzle()
    {
        return _sourcePuzzle;
    }

    /// <summary>
    /// 정답 여부의 내부 상태를 로컬 캐시에 반영한다.
    /// </summary>
    private void ApplyReagentVisualState()
    {
        isCorrectReagent = NetIsCorrectReagent; // 네트워크값을 로컬 상태에 반영
        itemName = "시약"; // 정답/오답 여부를 노출하지 않는 공통 이름 유지
    }

    /// <summary>
    /// 월드 아이템으로 보일 때의 표현을 강제한다.
    /// </summary>
    private void ApplyWorldPresentationState()
    {
        bool shouldLookLikeWorldItem = !NetIsEquipped || NetIsInsertedInAnalyzer; // 월드 아이템처럼 보여야 하는 상태인지

        if (!shouldLookLikeWorldItem)
            return;

        SetLayerRecursively(gameObject, _initialWorldLayer); // 프리팹 원래 레이어로 복구

        if (worldRenderers != null)
        {
            for (int i = 0; i < worldRenderers.Length; i++)
            {
                if (worldRenderers[i] == null)
                    continue;

                worldRenderers[i].enabled = true; // 월드 렌더러 표시 강제
            }
        }
    }

    /// <summary>
    /// 지정한 오브젝트와 모든 자식의 레이어를 재귀적으로 변경한다.
    /// </summary>
    private void SetLayerRecursively(GameObject obj, int newLayer)
    {
        if (obj == null)
            return;

        obj.layer = newLayer;

        foreach (Transform child in obj.transform)
            SetLayerRecursively(child.gameObject, newLayer);
    }
}