using Fusion;
using UnityEngine;

[RequireComponent(typeof(PlayerKCCMotor))]
[RequireComponent(typeof(PlayerLookView))]
[RequireComponent(typeof(PlayerInteraction))]
[RequireComponent(typeof(PlayerHandView))]
[RequireComponent(typeof(PlayerFlashlightView))]
public class PlayerController : NetworkBehaviour, IInteractable
{
    public PlayerKCCMotor KCCMotor { get; private set; }
    public PlayerLookView LookView { get; private set; }
    public PlayerInteraction Interaction { get; private set; }
    public PlayerHandView HandView { get; private set; }
    public PlayerFlashlightView FlashlightView { get; private set; }
    public PlayerSpectatorController SpectatorController { get; private set; }

    [Header("역할 아이템 프리팹")]
    [SerializeField] private NetworkObject flashlightRoleItemPrefab;

    [Header("오른손 드랍")]
    [SerializeField] private float rightHandDropForwardOffset = 0.8f;
    [SerializeField] private float rightHandDropUpOffset = 0.5f;
    [SerializeField] private float rightHandDropImpulse = 2.5f;

    [Header("왼손 드랍")]
    [SerializeField] private float leftHandDropForwardOffset = 0.6f;
    [SerializeField] private float leftHandDropSideOffset = -0.25f;
    [SerializeField] private float leftHandDropUpOffset = 0.45f;
    [SerializeField] private float leftHandDropImpulse = 2.0f;

    [Header("포획")]
    [SerializeField] private float captureTransitionSeconds = 1.0f;
    [SerializeField] private float traumaPenaltyCapture1 = 10f;
    [SerializeField] private float traumaPenaltyCapture2 = 20f;
    [SerializeField] private float traumaPenaltyCapture3Plus = 30f;
    [SerializeField] private float traumaIncreasePerSecond = 1f;
    [SerializeField] private float traumaDeathThreshold = 100f;
    [SerializeField] private float rescueBaseTimeSeconds = 100f;

    [Networked] public PlayerState NetPlayerState { get; set; }
    [Networked] public PlayerRole NetPlayerRole { get; set; }
    [Networked] public Zone NetZone { get; set; }

    [Networked] public HideState NetHideState { get; set; }
    [Networked] public CapturePhase NetCapturePhase { get; set; }
    [Networked] public float NetAftereffectPercent { get; set; }
    [Networked] public int NetCaptureCount { get; set; }
    [Networked] public TickTimer NetCaptureTransitionTimer { get; set; }
    [Networked] public TickTimer NetCaptureExpireTimer { get; set; }
    [Networked] public Vector3 NetCaptureAnchorPosition { get; set; }
    [Networked] public Quaternion NetCaptureAnchorRotation { get; set; }
    [Networked] public NetworkId NetCurrentHideSpotId { get; set; }

    [Networked] public NetworkObject NetLeftHandItem { get; set; }
    [Networked] public NetworkObject NetRightHandItem { get; set; }

    [Networked] public NetworkBool NetMovementLocked { get; set; }
    [Networked] public NetworkBool NetLookLocked { get; set; }

    private int _lastInteractRequestTick = -1;

    /// <summary>
    /// 네트워크 스폰 시 핵심 컴포넌트를 초기화하고 기본 상태를 세팅한다.
    /// </summary>
    public override void Spawned()
    {
        KCCMotor = GetComponent<PlayerKCCMotor>();
        LookView = GetComponent<PlayerLookView>();
        Interaction = GetComponent<PlayerInteraction>();
        HandView = GetComponent<PlayerHandView>();
        FlashlightView = GetComponent<PlayerFlashlightView>();
        SpectatorController = GetComponent<PlayerSpectatorController>();

        KCCMotor.Initialize(this);
        LookView.Initialize(this);
        Interaction.Initialize(this);
        HandView.Initialize(this);
        FlashlightView.Initialize(this);

        if (SpectatorController != null)
            SpectatorController.Initialize(this);

        if (HasStateAuthority)
        {
            NetPlayerState = PlayerState.Normal;
            NetHideState = HideState.None;
            NetCapturePhase = CapturePhase.None;
            NetAftereffectPercent = 0f;
            NetCaptureCount = 0;
            NetCaptureTransitionTimer = TickTimer.None;
            NetCaptureExpireTimer = TickTimer.None;
            NetCaptureAnchorPosition = transform.position;
            NetCaptureAnchorRotation = transform.rotation;
            NetCurrentHideSpotId = default;

            NetLeftHandItem = default;
            NetRightHandItem = default;

            NetMovementLocked = false;
            NetLookLocked = false;
        }

        var bodySync = GetComponent<PlayerBodySync>();
        if (bodySync != null)
            bodySync.Initialize(this);
    }

    /// <summary>
    /// 입력을 소비하고, 서버 권한에서는 포획/은신 같은 지속 상태를 틱 단위로 갱신한다.
    /// </summary>
    public override void FixedUpdateNetwork()
    {
        if (HasStateAuthority)
        {
            ServerTickCaptureState();
        }

        if (!GetInput(out PlayerNetworkInput input))
            return;

        KCCMotor.Simulate(input, NetMovementLocked, NetLookLocked);

        if (HasInputAuthority && SpectatorController != null && IsSpectatorState())
        {
            SpectatorController.TickSpectatorInput(input);
        }

        if (HasInputAuthority &&
            input.Buttons.IsSet(InputButtons.Interact) &&
            Runner.Tick != _lastInteractRequestTick)
        {
            _lastInteractRequestTick = Runner.Tick;

            if (NetHideState != HideState.None)
            {
                RPC_RequestExitHide();
                return;
            }

            if (Interaction != null && Interaction.TryGetCurrentTargetId(out NetworkId targetId))
            {
                RPC_RequestInteract(targetId);
            }
        }
    }

    /// <summary>
    /// 카메라 루트에 붙은 손전등 라이트 루트를 외부에서 가져온다.
    /// </summary>
    public Transform GetCameraLightRoot()
    {
        return LookView != null ? LookView.GetCameraLightRoot() : null;
    }

    /// <summary>
    /// 외부 상태에 의해 이동 / 시야 잠금을 직접 설정한다.
    /// </summary>
    public void SetInputLock(bool movementLocked, bool lookLocked)
    {
        if (!HasStateAuthority)
            return;

        NetMovementLocked = movementLocked;
        NetLookLocked = lookLocked;
    }

    /// <summary>
    /// 현재 플레이어가 일반 게임플레이 입력을 사용할 수 있는지 반환한다.
    /// </summary>
    public bool CanUseGameplayInput()
    {
        return NetPlayerState == PlayerState.Normal && NetHideState == HideState.None;
    }

    /// <summary>
    /// 현재 플레이어가 관전 모드 대상인지 반환한다.
    /// </summary>
    public bool IsSpectatorState()
    {
        return NetPlayerState == PlayerState.Dead || NetPlayerState == PlayerState.Escaped;
    }

    /// <summary>
    /// 현재 플레이어가 Captured의 실제 플레이 단계인지 반환한다.
    /// </summary>
    public bool IsCaptureActive()
    {
        return NetPlayerState == PlayerState.Captured && NetCapturePhase == CapturePhase.Active;
    }

    /// <summary>
    /// 클라이언트가 선택한 상호작용 대상을 서버에 요청하고, 서버가 최종 성립 여부를 판정한다.
    /// </summary>
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_RequestInteract(NetworkId targetId)
    {
        if (!HasStateAuthority)
            return;

        if (!CanUseGameplayInput())
            return;

        if (!Runner.TryFindObject(targetId, out NetworkObject targetObject))
            return;

        if (targetObject == null)
            return;

        float maxDistance = Interaction != null ? Interaction.InteractDistance : 2f;

        if (!IsTargetWithinInteractDistance(targetObject, maxDistance))
            return;

        if (!PlayerInteraction.TryFindInteractable(targetObject.transform, out _, out IInteractable interactable))
            return;

        if (!interactable.CanInteract(this))
            return;

        interactable.Interact(this);
    }

    /// <summary>
    /// 은신 중 좌클릭 입력이 들어오면 현재 숨은 은신처를 다시 찾아,
    /// 입장과 동일하게 HideSpotInteractable 쪽 퇴장 경로를 타도록 요청한다.
    /// </summary>
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_RequestExitHide()
    {
        if (!HasStateAuthority)
            return;

        if (NetHideState == HideState.None)
            return;

        if (!TryGetCurrentHideSpot(out HideSpotInteractable hideSpot))
        {
            Debug.LogWarning("[PlayerController] 현재 숨은 은신처를 찾지 못해 퇴장 요청을 처리할 수 없습니다.", this);
            return;
        }

        hideSpot.RequestExit(this);
    }

    public ItemObject GetLeftHandItemObject()
    {
        return TryGetItemObject(NetLeftHandItem, out ItemObject item) ? item : null;
    }

    public ItemObject GetRightHandItemObject()
    {
        return TryGetItemObject(NetRightHandItem, out ItemObject item) ? item : null;
    }

    public bool ServerEquipLeftHand(ItemObject item)
    {
        if (!HasStateAuthority || item == null)
            return false;

        if (NetPlayerState != PlayerState.Normal)
            return false;

        if (NetHideState != HideState.None)
            return false;

        if (NetLeftHandItem != null)
            return false;

        NetLeftHandItem = item.Object;
        item.OnEquipped(this);
        return true;
    }

    public bool ServerGrantRoleItemForCurrentRole()
    {
        if (!HasStateAuthority)
            return false;

        if (NetPlayerRole != PlayerRole.Flashlight)
            return false;

        if (NetLeftHandItem != null)
            return false;

        if (flashlightRoleItemPrefab == null)
        {
            Debug.LogWarning($"[PlayerController] flashlightRoleItemPrefab이 비어 있습니다. name={name}");
            return false;
        }

        NetworkObject spawnedItem = Runner.Spawn(
            flashlightRoleItemPrefab,
            transform.position,
            transform.rotation,
            Object.InputAuthority
        );

        if (spawnedItem == null)
        {
            Debug.LogWarning($"[PlayerController] 역할 손전등 Spawn 실패. name={name}");
            return false;
        }

        ItemObject item = spawnedItem.GetComponent<ItemObject>();
        if (item == null)
        {
            Debug.LogWarning($"[PlayerController] Spawn된 역할 손전등에 ItemObject가 없습니다. name={spawnedItem.name}");
            Runner.Despawn(spawnedItem);
            return false;
        }

        if (!ServerEquipLeftHand(item))
        {
            Runner.Despawn(spawnedItem);
            return false;
        }

        return true;
    }

    public bool ServerTryPickupRightHand(ItemObject item)
    {
        if (!HasStateAuthority || item == null)
            return false;

        if (!CanUseGameplayInput())
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

    public bool ServerDropLeftHandItem()
    {
        if (!HasStateAuthority)
            return false;

        if (!TryGetItemObject(NetLeftHandItem, out ItemObject item))
            return false;

        Vector3 dropPosition = GetLeftHandDropPosition();
        Vector3 dropForward = transform.forward;

        NetLeftHandItem = default;
        item.OnDropped(dropPosition, dropForward, leftHandDropImpulse);
        return true;
    }

    public void ServerForceDropAllHeldItems()
    {
        if (!HasStateAuthority)
            return;

        ServerDropLeftHandItem();
        ServerDropRightHandItem();
    }

    public bool ServerTryTakeRightHandFrom(PlayerController target)
    {
        if (!HasStateAuthority || target == null || target == this)
            return false;

        if (!CanUseGameplayInput())
            return false;

        if (!target.CanUseGameplayInput())
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

    public void ServerEquipRightHand(ItemObject item)
    {
        ServerTryPickupRightHand(item);
    }

    public bool CanInteract(PlayerController actor)
    {
        if (actor == null || actor == this)
            return false;

        if (!actor.CanUseGameplayInput())
            return false;

        if (!CanUseGameplayInput())
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

    //public bool ServerEnterCaptured()
    //{
    //    return ServerEnterCaptured(transform.position, transform.rotation);
    //}

    public bool ServerEnterCaptured(Vector3 captureAnchorPosition, Quaternion captureAnchorRotation)
    {
        if (!HasStateAuthority)
            return false;

        if (NetPlayerState != PlayerState.Normal)
            return false;

        NetPlayerState = PlayerState.Captured;
        NetCapturePhase = CapturePhase.Transition;
        NetHideState = HideState.None;
        NetCurrentHideSpotId = default;
        NetCaptureAnchorPosition = captureAnchorPosition;
        NetCaptureAnchorRotation = captureAnchorRotation;
        NetCaptureTransitionTimer = TickTimer.CreateFromSeconds(Runner, captureTransitionSeconds);
        NetCaptureExpireTimer = TickTimer.None;

        NetMovementLocked = true;
        NetLookLocked = false;

        ServerForceDropAllHeldItems();
        ApplyImmediateTraumaOnCapture();

        if (NetAftereffectPercent >= traumaDeathThreshold)
        {
            ServerEnterDead();
            return true;
        }

        // [Capture Presentation] 잡힌 플레이어 전용 괴물 포획 연출 시작 지점.
        // [Capture Presentation] alive 플레이어 전원 검정 페이드 인/아웃 시작 지점.
        // [Capture Presentation] spectator(Dead / Escaped)는 연출 제외.
        // [Capture Transition] 페이드 중 구제 위치로 이동하는 연출을 여기에 연결.

        return true;
    }

    public void ServerTickCaptureState()
    {
        if (!HasStateAuthority)
            return;

        if (NetPlayerState != PlayerState.Captured)
            return;

        if (NetCapturePhase == CapturePhase.Transition)
        {
            if (NetCaptureTransitionTimer.Expired(Runner))
            {
                ServerBeginCapturedActive();
            }
            return;
        }

        if (NetCapturePhase != CapturePhase.Active)
            return;

        NetAftereffectPercent += Runner.DeltaTime * traumaIncreasePerSecond;

        if (NetAftereffectPercent >= traumaDeathThreshold)
        {
            ServerEnterDead();
            return;
        }

        if (NetCaptureExpireTimer.Expired(Runner))
        {
            ServerEnterDead();
        }
    }

    public void ServerBeginCapturedActive()
    {
        if (!HasStateAuthority)
            return;

        if (NetPlayerState != PlayerState.Captured)
            return;

        NetCapturePhase = CapturePhase.Active;
        NetCaptureTransitionTimer = TickTimer.None;

        MovePlayerToWorldPose(NetCaptureAnchorPosition, NetCaptureAnchorRotation);

        float remainSeconds = Mathf.Max(0f, rescueBaseTimeSeconds - NetAftereffectPercent);
        NetCaptureExpireTimer = TickTimer.CreateFromSeconds(Runner, remainSeconds);

        // [Capture Transition] 검정 페이드아웃 종료 후 Active 단계 진입.
        // [Capture Active] 누워있는 상태로 시야를 움직이며 비밀번호 힌트 확인.
        // [Future Minigame] 포획 미니게임과 결과 처리 연결 지점.
    }

    public bool ServerExitCapturedToNormal()
    {
        if (!HasStateAuthority)
            return false;

        if (NetPlayerState != PlayerState.Captured)
            return false;

        NetPlayerState = PlayerState.Normal;
        NetCapturePhase = CapturePhase.None;
        NetCaptureTransitionTimer = TickTimer.None;
        NetCaptureExpireTimer = TickTimer.None;
        NetMovementLocked = false;
        NetLookLocked = false;

        return true;
    }

    public void ServerEnterDead()
    {
        if (!HasStateAuthority)
            return;

        NetPlayerState = PlayerState.Dead;
        NetHideState = HideState.None;
        NetCapturePhase = CapturePhase.None;
        NetCurrentHideSpotId = default;
        NetCaptureTransitionTimer = TickTimer.None;
        NetCaptureExpireTimer = TickTimer.None;
        NetMovementLocked = true;
        NetLookLocked = true;

        // [Death Presentation] 사망 UI 출력 및 확인 후 관전 모드 진입 지점.
    }

    public void ServerEnterEscaped()
    {
        if (!HasStateAuthority)
            return;

        NetPlayerState = PlayerState.Escaped;
        NetHideState = HideState.None;
        NetCapturePhase = CapturePhase.None;
        NetCurrentHideSpotId = default;
        NetCaptureTransitionTimer = TickTimer.None;
        NetCaptureExpireTimer = TickTimer.None;
        NetMovementLocked = true;
        NetLookLocked = true;
    }

    public bool ServerEnterHide(HideState hideState, NetworkObject hideSpotObject, Vector3 enterPosition, Quaternion enterRotation)
    {
        if (!HasStateAuthority)
            return false;

        if (NetPlayerState != PlayerState.Normal)
            return false;

        if (NetHideState != HideState.None)
            return false;

        NetHideState = hideState;
        NetCurrentHideSpotId = hideSpotObject != null ? hideSpotObject.Id : default;
        NetMovementLocked = true;
        NetLookLocked = false;

        MovePlayerToWorldPose(enterPosition, enterRotation);
        return true;
    }

    /// <summary>
    /// 은신처 내부 로직이 전달한 퇴장 위치를 사용해 hide 상태를 해제한다.
    /// 은신처 점유 상태 해제는 HideSpotInteractable이 담당하고,
    /// 플레이어 컨트롤러는 플레이어 상태와 이동 처리만 담당한다.
    /// </summary>
    public bool ServerExitHide(Vector3 exitPosition, Quaternion exitRotation)
    {
        if (!HasStateAuthority)
            return false;

        if (NetHideState == HideState.None)
            return false;

        NetHideState = HideState.None;
        NetCurrentHideSpotId = default;

        if (NetPlayerState == PlayerState.Normal)
        {
            NetMovementLocked = false;
            NetLookLocked = false;
        }

        MovePlayerToWorldPose(exitPosition, exitRotation);
        return true;
    }

    /// <summary>
    /// 현재 숨고 있는 은신처를 NetworkId 기준으로 다시 찾아온다.
    /// 숨은 상태에서 퇴장 요청 시 HideSpotInteractable 쪽 로직을 다시 타기 위해 사용한다.
    /// </summary>
    private bool TryGetCurrentHideSpot(out HideSpotInteractable hideSpot)
    {
        hideSpot = null;

        if (NetCurrentHideSpotId == default)
            return false;

        if (!Runner.TryFindObject(NetCurrentHideSpotId, out NetworkObject hideSpotObject))
            return false;

        if (hideSpotObject == null)
            return false;

        hideSpot = hideSpotObject.GetComponent<HideSpotInteractable>();
        return hideSpot != null;
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

    private Vector3 GetLeftHandDropPosition()
    {
        return transform.position +
               transform.forward * leftHandDropForwardOffset +
               transform.right * leftHandDropSideOffset +
               Vector3.up * leftHandDropUpOffset;
    }

    private void ApplyImmediateTraumaOnCapture()
    {
        NetCaptureCount += 1;
        NetAftereffectPercent += GetBaseTraumaPenalty(NetCaptureCount);
    }

    private float GetBaseTraumaPenalty(int captureCount)
    {
        if (captureCount <= 1)
            return traumaPenaltyCapture1;

        if (captureCount == 2)
            return traumaPenaltyCapture2;

        return traumaPenaltyCapture3Plus;
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

    /// <summary>
    /// 네트워크 플레이어를 지정한 월드 위치/회전으로 즉시 이동시킨다.
    /// 이동은 반드시 PlayerKCCMotor를 통해 SimpleKCC 기준으로 처리한다.
    /// </summary>
    private void MovePlayerToWorldPose(Vector3 worldPosition, Quaternion worldRotation)
    {
        if (KCCMotor != null)
        {
            KCCMotor.WarpToPose(worldPosition, worldRotation);
            return;
        }

        transform.SetPositionAndRotation(worldPosition, worldRotation);

        Rigidbody body = GetComponent<Rigidbody>();
        if (body != null)
        {
            body.position = worldPosition;
            body.rotation = worldRotation;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }
    }
}
