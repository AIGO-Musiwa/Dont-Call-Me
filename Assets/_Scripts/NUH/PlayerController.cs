using Fusion;
using UnityEngine;

[RequireComponent(typeof(PlayerKCCMotor))]
[RequireComponent(typeof(PlayerLookView))]
[RequireComponent(typeof(PlayerInteraction))]
[RequireComponent (typeof(PlayerHandView))]
public class PlayerController : NetworkBehaviour
{
    public PlayerKCCMotor KCCMotor { get; private set; }
    public PlayerLookView LookView { get; private set; }
    public PlayerInteraction Interaction { get; private set; }
    public PlayerHandView HandView { get; private set; }

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

        // 상호작용 요청은 로컬 플레이어가 1틱에 1번만 보낸다
        if(HasInputAuthority && input.Buttons.IsSet(InputButtons.Interact) && Runner.Tick != _lastInteractRequestTick)
        {
            _lastInteractRequestTick = Runner.Tick;

            if(Interaction != null && Interaction.TryGetCurrentTargetId(out NetworkId targetId))
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

        // 1차 검증
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

    public void ServerEquipRightHand(ItemObject item)
    {
        if (!HasStateAuthority || item == null)
            return;

        if (NetRightHandItem != null)
            return;

        NetRightHandItem = item.Object;
        item.OnEquipped(this);
    }
}