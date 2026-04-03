using Fusion;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class ItemObject : NetworkBehaviour, IInteractable
{
    [Header("아이템 정보")]
    [SerializeField] protected ItemType itemType = ItemType.None;
    [SerializeField] protected bool isRoleItem = false;

    ItemType NetItemType { get; set; }
    NetworkBool NetIsRoleItem { get; set; }
    NetworkBool NetIsEquipped { get; set; }
    PlayerRef NetCurrentHolder { get; set; }

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

        //이미 누가 들고있는 아이템이면 상호작용 불가 -> 나중에 뺏는걸로 변경해야함
        if (NetIsEquipped)
            return false;

        //오른손이 비어있어야 집을 수 있다.
        if (actor.NetRightHandItem != null)
            return false;

        return true;
    }

    public virtual void Interact(PlayerController actor)
    {
        if (!HasStateAuthority)
            return;

        if (!CanInteract(actor))
            return;

        actor.ServerEquipRightHand(this);
    }

    public virtual string GetPromptText(PlayerController actor)
    {
        return "줍기";
    }

    public virtual void OnEquipped(PlayerController holder)
    {
        if (!HasStateAuthority || holder == null)
            return;

        NetIsEquipped = true;
        NetCurrentHolder = holder.Object.InputAuthority;
    }

    public virtual void OnDropped()
    {
        if (!HasStateAuthority)
            return;

        NetIsEquipped = false;
        NetCurrentHolder = PlayerRef.None;
    }

    protected virtual void ApplyPresentationState()
    {
        bool equipped = NetIsEquipped;

        if(_rigidbody != null)
        {
            _rigidbody.isKinematic = equipped;
            _rigidbody.linearVelocity = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;
        }

        if(_colliders != null)
        {
            for (int i = 0; i < _colliders.Length; i++)
            {
                if (_colliders[i] == null) continue;
                _colliders[i].enabled = !equipped;
            }
        }
    }
}
