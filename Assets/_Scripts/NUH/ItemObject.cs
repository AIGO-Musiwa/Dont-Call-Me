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

    [SerializeField] private Vector3 viewRotationOffset = new Vector3(0, 180, 0); // 1인칭 전용 회전 오프셋
    public Vector3 ViewRotationOffset => viewRotationOffset;

    [Header("시각 효과")]
    [SerializeField] protected MonoBehaviour outlineComponent; // 외곽선 스크립트 참조

    [Networked] public ItemType NetItemType { get; private set; }
    [Networked] public NetworkBool NetIsRoleItem { get; private set; }
    [Networked] public NetworkBool NetIsEquipped { get; private set; }
    [Networked] public PlayerRef NetCurrentHolder { get; private set; }

    protected Collider[] _colliders;
    protected Rigidbody _rigidbody;

    /// <summary>
    /// 아이템의 Collider / Rigidbody 참조를 캐싱한다.
    /// </summary>
    protected virtual void Awake()
    {
        _colliders = GetComponentsInChildren<Collider>(true);
        _rigidbody = GetComponent<Rigidbody>();

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

    /// <summary>
    /// 아이템 상호작용이 성립하면 서버가 플레이어 오른손 장착을 시도한다.
    /// </summary>
    public virtual void Interact(PlayerController actor)
    {
        if (!HasStateAuthority)
            return;

        if (!CanInteract(actor))
            return;

        // 상호작용 시 외곽선 효과 정리
        LoseFocus();
        actor.ServerTryPickupRightHand(this);
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

        transform.position = worldPosition;
        transform.rotation = Quaternion.identity;

        if (_rigidbody != null)
        {
            _rigidbody.position = worldPosition;
            _rigidbody.rotation = Quaternion.identity;
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
            if (_rigidbody.isKinematic != equipped)
                _rigidbody.isKinematic = equipped;
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
    }
}