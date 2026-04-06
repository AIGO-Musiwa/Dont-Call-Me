using Fusion;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class ItemObject : NetworkBehaviour, IInteractable
{
    [Header("아이템 정보")]
    [SerializeField] protected ItemType itemType = ItemType.None;
    [SerializeField] protected bool isRoleItem = false;

    [Networked] public ItemType NetItemType { get; private set; }
    [Networked] public NetworkBool NetIsRoleItem { get; private set; }
    [Networked] public NetworkBool NetIsEquipped { get; private set; }
    [Networked] public PlayerRef NetCurrentHolder { get; private set; }

    protected Collider[] _colliders;
    protected Rigidbody _rigidbody;

    protected virtual void Awake()
    {
        _colliders = GetComponentsInChildren<Collider>(true);
        _rigidbody = GetComponent<Rigidbody>();
    }

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

    public override void Render()
    {
        ApplyPresentationState();
    }

    public virtual bool CanInteract(PlayerController actor)
    {
        if (actor == null)
            return false;

        if (actor.NetPlayerState != PlayerState.Alive)
            return false;

        // 이미 누가 들고 있는 월드 아이템은 직접 줍지 못함
        if (NetIsEquipped)
            return false;

        return true;
    }

    public virtual void Interact(PlayerController actor)
    {
        if (!HasStateAuthority)
            return;

        if (!CanInteract(actor))
            return;

        actor.ServerTryPickupRightHand(this);
    }

    public virtual string GetPromptText(PlayerController actor)
    {
        if (actor != null && actor.NetRightHandItem != null)
            return "기존 아이템 내려놓고 줍기";

        return "줍기";
    }

    public virtual void OnEquipped(PlayerController holder)
    {
        if (!HasStateAuthority || holder == null)
            return;

        NetIsEquipped = true;
        NetCurrentHolder = holder.Object.InputAuthority;
        ApplyPresentationState();
    }

    /// <summary>
    /// 내부 상태만 "드랍된 상태"로 정리한다.
    /// 실제 위치 이동/물리 힘 적용은 하지 않는다.
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
    /// 실제 월드 드랍 처리.
    /// 내부 드랍 상태 정리 후 위치/회전/물리 힘을 적용한다.
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

    protected virtual void ApplyPresentationState()
    {
        bool equipped = NetIsEquipped;

        if (_rigidbody != null)
        {
            _rigidbody.isKinematic = equipped;
            _rigidbody.linearVelocity = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;
        }

        if (_colliders != null)
        {
            for (int i = 0; i < _colliders.Length; i++)
            {
                if (_colliders[i] == null)
                    continue;

                _colliders[i].enabled = !equipped;
            }
        }
    }
}