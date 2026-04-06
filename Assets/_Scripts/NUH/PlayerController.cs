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

        // 시점 기준 origin -> 대상 collider의 closest point 거리 검사
        if (!IsTargetWithinInteractDistance(targetObject, maxDistance))
            return;

        if (!PlayerInteraction.TryFindInteractable(targetObject.transform, out _, out IInteractable interactable))
            return;

        if (!interactable.CanInteract(this))
            return;

        interactable.Interact(this);
    }

    public ItemObject GetRightHandItemObject()
    {
        return TryGetItemObject(NetRightHandItem, out ItemObject item) ? item : null;
    }

    public bool ServerTryPickupRightHand(ItemObject item)
    {
        if (!HasStateAuthority || item == null)
            return false;

        if (NetPlayerState != PlayerState.Alive)
            return false;

        if (!item.CanInteract(this))
            return false;

        if (!EnsureRightHandEmpty())
            return false;

        NetRightHandItem = item.Object;
        item.OnEquipped(this);
        return true;
    }

    public bool ServerDropRightHandItem()
    {
        if (!HasStateAuthority)
            return false;

        if (!TryGetItemObject(NetRightHandItem, out ItemObject item))
            return false;

        Vector3 dropPosition = GetRightHandDropPosition();
        Vector3 dropForward = transform.forward;

        NetRightHandItem = default;
        item.OnDropped(dropPosition, dropForward, rightHandDropImpulse);
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

        if (!target.TryGetItemObject(target.NetRightHandItem, out ItemObject targetItem))
            return false;

        if (!EnsureRightHandEmpty())
            return false;

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

    private bool EnsureRightHandEmpty()
    {
        if (NetRightHandItem == null)
            return true;

        return ServerDropRightHandItem();
    }

    private bool TryGetItemObject(NetworkObject networkObject, out ItemObject item)
    {
        item = null;

        if (networkObject == null)
            return false;

        item = networkObject.GetComponent<ItemObject>();
        return item != null;
    }

    private Vector3 GetRightHandDropPosition()
    {
        return transform.position +
               transform.forward * rightHandDropForwardOffset +
               Vector3.up * rightHandDropUpOffset;
    }

    private Vector3 GetServerInteractionOrigin()
    {
        if (LookView != null && LookView.ViewOrigin != null)
            return LookView.ViewOrigin.position;

        float fallbackEyeHeight = 1.6f;

        if (KCCMotor != null)
        {
            fallbackEyeHeight = (KCCMotor.IsCrouching ? KCCMotor.CrouchHeight : KCCMotor.StandHeight) - 0.1f;
        }

        return transform.position + Vector3.up * fallbackEyeHeight;
    }

    private Vector3 GetClosestInteractionPoint(NetworkObject targetObject, Vector3 origin)
    {
        if (targetObject == null)
            return origin;

        Collider[] colliders = targetObject.GetComponentsInChildren<Collider>(true);

        Vector3 bestPoint = targetObject.transform.position;
        float bestSqrDistance = (bestPoint - origin).sqrMagnitude;
        bool foundCollider = false;

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider col = colliders[i];
            if (col == null || !col.enabled)
                continue;

            Vector3 point = col.ClosestPoint(origin);
            float sqrDistance = (point - origin).sqrMagnitude;

            if (!foundCollider || sqrDistance < bestSqrDistance)
            {
                foundCollider = true;
                bestSqrDistance = sqrDistance;
                bestPoint = point;
            }
        }

        return bestPoint;
    }

    private bool IsTargetWithinInteractDistance(NetworkObject targetObject, float maxDistance)
    {
        Vector3 origin = GetServerInteractionOrigin();
        Vector3 targetPoint = GetClosestInteractionPoint(targetObject, origin);

        float sqrDistance = (targetPoint - origin).sqrMagnitude;
        float allowedSqrDistance = maxDistance * maxDistance + 0.25f;

        return sqrDistance <= allowedSqrDistance;
    }
}