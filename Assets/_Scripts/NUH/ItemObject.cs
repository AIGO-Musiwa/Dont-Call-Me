using Fusion;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class ItemObject : NetworkBehaviour, IInteractable
{
    [Header("아이템 정보")]
    [SerializeField] protected ItemType itemType = ItemType.None;
    [SerializeField] protected bool isRoleItem = false;
    [SerializeField] protected string itemName = "알 수 없는 아이템";
    public string ItemName => itemName; // 외부(HUD)에서 이 이름을 읽어감!

    [Header("1인칭 뷰모델 설정")]
    [SerializeField] private GameObject localViewPrefab; // 내 화면(1인칭) 전용 모델 프리팹
    public GameObject LocalViewPrefab => localViewPrefab;

    [SerializeField] private Vector3 viewRotationOffset = new Vector3(0, 0, 0); // 1인칭 전용 회전 오프셋
    public Vector3 ViewRotationOffset => viewRotationOffset;

    [Header("시각 효과")]
    [SerializeField] protected MonoBehaviour outlineComponent; // 외곽선 스크립트 참조

    [Networked] public ItemType NetItemType { get; private set; }
    [Networked] public NetworkBool NetIsRoleItem { get; private set; }
    [Networked] public NetworkBool NetIsEquipped { get; private set; }
    [Networked] public PlayerRef NetCurrentHolder { get; private set; }

    protected Collider[] _colliders;
    protected Rigidbody _rigidbody;
    private int _initialLayer; // [추가] 초기 레이어 저장용

    /// <summary>
    /// 아이템의 Collider / Rigidbody 참조를 캐싱한다.
    /// </summary>
    protected virtual void Awake()
    {
        _colliders = GetComponentsInChildren<Collider>(true);
        _rigidbody = GetComponent<Rigidbody>();

        // [추가] 스폰 전 초기 레이어를 기억 (보통 Default)
        _initialLayer = gameObject.layer;

        // 초기화 시 외곽선 비활성화
        if (outlineComponent != null) outlineComponent.enabled = false;
    }

    /// <summary>
    /// 네트워크 스폰 시 아이템 기본 네트워크 상태를 초기화한다.
    /// </summary>
    public override void Spawned()
    {
        if (HasStateAuthority)
        {
            NetItemType = itemType;
            NetIsRoleItem = isRoleItem;
            NetIsEquipped = false;
            NetCurrentHolder = PlayerRef.None;
        }

        ApplyPresentationState();
    }

    /// <summary>
    /// 렌더 단계에서 아이템의 월드/장착 표현 상태를 반영한다.
    /// </summary>
    public override void Render()
    {
        ApplyPresentationState();
    }

    /// <summary>
    /// 플레이어의 시선이 아이템에 머물 때 외곽선을 활성화한다.
    /// </summary>
    public virtual void OnFocus()
    {
        if (outlineComponent != null && !NetIsEquipped)
            outlineComponent.enabled = true;
    }

    /// <summary>
    /// 플레이어의 시선이 아이템에서 벗어날 때 외곽선을 비활성화한다.
    /// </summary>
    public virtual void LoseFocus()
    {
        if (outlineComponent != null)
            outlineComponent.enabled = false;
    }

    /// <summary>
    /// 현재 플레이어가 이 아이템을 상호작용 가능한지 검사한다.
    /// </summary>
    public virtual bool CanInteract(PlayerController actor)
    {
        if (actor == null)
            return false;

        if (actor.NetPlayerState != PlayerState.Normal)
            return false;

        if (actor.NetHideState != HideState.None)
            return false;

        if (NetIsEquipped)
            return false;

        return true;
    }

    //// <summary>
    /// 아이템 상호작용이 성립하면 서버가 플레이어의 적절한 손(왼손/오른손)에 장착을 시도한다.
    /// </summary>
    public virtual void Interact(PlayerController actor)
    {
        if (!HasStateAuthority)
            return;

        if (!CanInteract(actor))
            return;

        // 상호작용 시 외곽선 효과 정리
        LoseFocus();

        // 🛠️ [회로 수리] 역할 아이템은 왼손, 일반 아이템은 오른손으로 배선 분기!
        if (NetIsRoleItem)
        {
            actor.ServerTryPickupLeftHand(this);
        }
        else
        {
            actor.ServerTryPickupRightHand(this);
        }
    }

    /// <summary>
    /// 현재 아이템 상호작용 프롬프트 문구를 반환한다.
    /// </summary>
    public virtual string GetPromptText(PlayerController actor)
    {
        if (actor != null && actor.NetRightHandItem != null)
            return $"{itemName}으로 교체";

        return $"{itemName} 줍기";
    }

    /// <summary>
    /// 아이템이 플레이어 슬롯에 장착될 때 호출된다.
    /// </summary>
    public virtual void OnEquipped(PlayerController holder)
    {
        if (!HasStateAuthority || holder == null)
            return;

        if (_rigidbody != null)
        {
            _rigidbody.linearVelocity = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;
        }

        NetIsEquipped = true;
        NetCurrentHolder = holder.Object.InputAuthority;

        // 장착 시 외곽선 강제 종료
        if (outlineComponent != null) outlineComponent.enabled = false;

        ApplyPresentationState();
    }

    /// <summary>
    /// 내부 상태만 드랍 상태로 정리한다.
    /// 실제 이동과 물리 적용은 별도 함수에서 처리한다.
    /// </summary>
    protected virtual void ApplyDroppedState()
    {
        if (!HasStateAuthority)
            return;

        NetIsEquipped = false;
        NetCurrentHolder = PlayerRef.None;
        ApplyPresentationState();
    }

    /// <summary>
    /// 월드 위치로 아이템을 드랍하고 초기 속도/힘을 적용한다.
    /// </summary>
    public virtual void OnDropped(Vector3 worldPosition, Vector3 worldForward, float impulse)
    {
        if (!HasStateAuthority)
            return;

        ApplyDroppedState();

        // 드랍 시 레이어를 원래대로 복구하여 카메라에 보이게 함
        SetLayerRecursively(gameObject, _initialLayer);

        // [수정] 플레이어가 바라보는 방향(worldForward)을 바라보도록 회전값을 계산
        // 만약 아이템이 바닥에 수평하게 눕기를 원한다면 worldForward.y를 0으로 잡는 로직이 추가될 수 있어.
        Quaternion dropRotation = Quaternion.LookRotation(worldForward);

        transform.position = worldPosition;
        transform.rotation = dropRotation; // [수정] 정면 고정 대신 보고 있는 방향 적용

        if (_rigidbody != null)
        {
            _rigidbody.position = worldPosition;
            _rigidbody.rotation = dropRotation; // [수정] 물리 엔진 회전값도 동기화
            _rigidbody.linearVelocity = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;
            _rigidbody.AddForce(worldForward.normalized * impulse, ForceMode.VelocityChange);
        }
    }

    /// <summary>
    /// 장착 여부에 따라 Collider / Rigidbody 표현 상태를 갱신한다.
    /// </summary>
    protected virtual void ApplyPresentationState()
    {
        if (Object == null || !Object.IsValid)
            return;

        bool equipped = NetIsEquipped;

        if (_rigidbody != null)
        {
            // 물리 엔진 가동 권한 통제
            // 1. 장착 중(equipped)일 때는 내 손을 따라가야 하니 모두가 물리 연산 정지(true).
            // 2. 떨어졌을 때(!equipped), 물리 연산(false)은 오직 '호스트(StateAuthority)'만 가동한다.
            // 3. 클라이언트(프록시)들은 물리를 켜지 않고 호스트가 보내는 좌표만 수신한다.
            bool shouldBeKinematic = equipped || !HasStateAuthority;

            if (_rigidbody.isKinematic != shouldBeKinematic)
                _rigidbody.isKinematic = shouldBeKinematic;
        }

        if (_colliders != null)
        {
            for (int i = 0; i < _colliders.Length; i++)
            {
                if (_colliders[i] == null)
                    continue;

                bool shouldEnable = !equipped;
                if (_colliders[i].enabled != shouldEnable)
                    _colliders[i].enabled = shouldEnable;
            }
        }

        // 레이어 동기화
        if (!equipped)
        {
            // 바닥에 떨어졌을 때: '모든 클라이언트'가 원래 레이어로 복구!
            SetLayerRecursively(gameObject, _initialLayer);
        }
    }

    // [추가] 레이어 일괄 변경용 보조 함수
    private void SetLayerRecursively(GameObject obj, int newLayer)
    {
        if (obj == null) return;
        obj.layer = newLayer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, newLayer);
        }
    }
}