using Fusion;
using UnityEngine;

[RequireComponent(typeof(PlayerKCCMotor))]
[RequireComponent(typeof(PlayerLookView))]
[RequireComponent(typeof(PlayerInteraction))]
[RequireComponent(typeof(PlayerHandView))]
public class PlayerController : NetworkBehaviour, IInteractable
{
    public PlayerKCCMotor KCCMotor { get; private set; }
    public PlayerLookView LookView { get; private set; }
    public PlayerInteraction Interaction { get; private set; }
    public PlayerHandView HandView { get; private set; }

    [Header("오른손 드랍")]
    [SerializeField] private float rightHandDropForwardOffset = 0.8f;
    [SerializeField] private float rightHandDropUpOffset = 0.5f;
    [SerializeField] private float rightHandDropImpulse = 2.5f;

    [Networked] public PlayerState NetPlayerState { get; set; }
    [Networked] public PlayerRole NetPlayerRole { get; set; }
    [Networked] public Zone NetZone { get; set; }

    [Networked] public NetworkObject NetLeftHandItem { get; set; }
    [Networked] public NetworkObject NetRightHandItem { get; set; }

    [Networked] public NetworkBool NetMovementLocked { get; set; }
    [Networked] public NetworkBool NetLookLocked { get; set; }

    private int _lastInteractRequestTick = -1;

    public override void Spawned()
    {
        KCCMotor = GetComponent<PlayerKCCMotor>();
        LookView = GetComponent<PlayerLookView>();
        Interaction = GetComponent<PlayerInteraction>();
        HandView = GetComponent<PlayerHandView>();

        KCCMotor.Initialize(this);
        LookView.Initialize(this);
        Interaction.Initialize(this);
        HandView.Initialize(this);

        if (HasStateAuthority)
        {
            NetPlayerState = PlayerState.Alive;
            NetPlayerRole = PlayerRole.None;
            NetZone = Zone.ZoneA; // 임시값
            NetLeftHandItem = default;
            NetRightHandItem = default;
            NetMovementLocked = false;
            NetLookLocked = false;
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!GetInput(out PlayerNetworkInput input))
            return;

        KCCMotor.Simulate(input, NetMovementLocked, NetLookLocked);

        if (HasInputAuthority &&
            input.Buttons.IsSet(InputButtons.Interact) &&
            Runner.Tick != _lastInteractRequestTick)
        {
            _lastInteractRequestTick = Runner.Tick;

            if (Interaction != null && Interaction.TryGetCurrentTargetId(out NetworkId targetId))
            {
                RPC_RequestInteract(targetId);
            }
        }
    }

    public Transform GetCameraLightRoot()
    {
        return LookView != null ? LookView.GetCameraLightRoot() : null;
    }

    public void SetInputLock(bool movementLocked, bool lookLocked)
    {
        if (!HasStateAuthority)
            return;

        NetMovementLocked = movementLocked;
        NetLookLocked = lookLocked;
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_RequestInteract(NetworkId targetId)
    {
        if (!HasStateAuthority)
            return;

        if (NetPlayerState != PlayerState.Alive)
            return;

        if (!Runner.TryFindObject(targetId, out NetworkObject targetObject))
            return;

        if (targetObject == null)
            return;

        float maxDistance = Interaction != null ? Interaction.InteractDistance : 2.5f;
        float sqrDistance = (targetObject.transform.position - transform.position).sqrMagnitude;
        if (sqrDistance > maxDistance * maxDistance + 0.25f)
            return;

        if (!PlayerInteraction.TryFindInteractable(targetObject.transform, out _, out IInteractable interactable))
            return;

        if (!interactable.CanInteract(this))
            return;

        interactable.Interact(this);
    }

    public ItemObject GetRightHandItemObject()
    {
        return NetRightHandItem != null ? NetRightHandItem.GetComponent<ItemObject>() : null;
    }

    public bool ServerTryPickupRightHand(ItemObject item)
    {
        if (!HasStateAuthority || item == null)
            return false;

        if (NetPlayerState != PlayerState.Alive)
            return false;

        if (!item.CanInteract(this))
            return false;

        // 규칙 2 준비:
        // 오른손이 차 있으면 기존 아이템 먼저 드랍하고 새 아이템 획득
        if (NetRightHandItem != null)
        {
            ServerDropRightHandItem();
        }

        NetRightHandItem = item.Object;
        item.OnEquipped(this);
        return true;
    }

    public bool ServerDropRightHandItem()
    {
        if (!HasStateAuthority)
            return false;

        if (NetRightHandItem == null)
            return false;

        ItemObject item = NetRightHandItem.GetComponent<ItemObject>();
        NetRightHandItem = default;

        if (item == null)
            return false;

        Vector3 dropPosition =
            transform.position +
            transform.forward * rightHandDropForwardOffset +
            Vector3.up * rightHandDropUpOffset;

        item.OnDropped(dropPosition, transform.forward, rightHandDropImpulse);
        return true;
    }

    public bool ServerTryTakeRightHandFrom(PlayerController target)
    {
        if (!HasStateAuthority || target == null || target == this)
            return false;

        if (NetPlayerState != PlayerState.Alive)
            return false;

        if (target.NetPlayerState != PlayerState.Alive)
            return false;

        ItemObject targetItem = target.GetRightHandItemObject();
        if (targetItem == null)
            return false;

        // 애매한 부분이긴 한데,
        // 규칙 2와 일관성 있게 가려면 탈취자 오른손이 차 있어도 먼저 드랍 후 탈취가 자연스럽다.
        if (NetRightHandItem != null)
        {
            ServerDropRightHandItem();
        }

        target.NetRightHandItem = default;
        NetRightHandItem = targetItem.Object;
        targetItem.OnEquipped(this);

        return true;
    }

    // 기존 호출부 호환용
    public void ServerEquipRightHand(ItemObject item)
    {
        ServerTryPickupRightHand(item);
    }

    // -------------------------------------------------
    // IInteractable : 플레이어를 상호작용해서 오른손 아이템 탈취
    // -------------------------------------------------

    public bool CanInteract(PlayerController actor)
    {
        if (actor == null || actor == this)
            return false;

        if (actor.NetPlayerState != PlayerState.Alive)
            return false;

        if (NetPlayerState != PlayerState.Alive)
            return false;

        return NetRightHandItem != null;
    }

    public void Interact(PlayerController actor)
    {
        if (!HasStateAuthority)
            return;

        if (!CanInteract(actor))
            return;

        actor.ServerTryTakeRightHandFrom(this);
    }

    public string GetPromptText(PlayerController actor)
    {
        if (!CanInteract(actor))
            return string.Empty;

        return "오른손 아이템 뺏기";
    }
}