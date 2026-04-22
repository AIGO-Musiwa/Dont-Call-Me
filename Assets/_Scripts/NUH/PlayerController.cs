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
    [SerializeField] private NetworkObject walkieTalkieRoleItemPrefab;

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
    [Networked, OnChangedRender(nameof(OnZoneChanged))]
    public Zone NetZone { get; set; }

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

    //앉기 관련 네트워크 변수 추가
    [Networked] public NetworkBool NetIsCrouching { get; set; }

    // 수신자 팀원이 수신 무전기 근처에 있는지 여부
    [Networked, OnChangedRender(nameof(OnNearWalkieChanged))]
    public NetworkBool NetIsNearReceiver { get; set; }

    // 송신자 팀원이 송신 무전기 근처에 있는지 여부
    [Networked, OnChangedRender(nameof(OnNearSenderChanged))]
    public NetworkBool NetIsNearSender { get; set; }

    private int _lastInteractRequestTick = -1;
    private bool _prevWalkiePressed;

    // 플레이어 State 변화 감지
    private ChangeDetector stateChangeDetector;

    // [추가 필드] 현재 조준 중인 아이템의 외곽선 제어 장치 저장
    private ItemOutlineController _lastHighlightedOutline;

    // 테스트용 임시 포획 Anchor
    [SerializeField] private Transform debugCaptureAnchor;

    public override void Spawned()
    {
        KCCMotor = GetComponent<PlayerKCCMotor>();
        LookView = GetComponent<PlayerLookView>();
        Interaction = GetComponent<PlayerInteraction>();
        HandView = GetComponent<PlayerHandView>();
        FlashlightView = GetComponent<PlayerFlashlightView>();
        SpectatorController = FindFirstObjectByType<PlayerSpectatorController>(FindObjectsInactive.Include);

        KCCMotor.Initialize(this);
        LookView.Initialize(this);
        Interaction.Initialize(this);
        HandView.Initialize(this);
        FlashlightView.Initialize(this);
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

            SetInputLock(false, false);
            //NetMovementLocked = false;
            //NetLookLocked = false;
        }
        // 내 로컬 기체인 경우에만 HUD를 찾아 연결한다.
        if (HasInputAuthority)
        {
            // [수정] UnityEngine.Object를 명시하여 이름 충돌을 방지한다.
            HUDController hud = UnityEngine.Object.FindAnyObjectByType<HUDController>();

            if (hud != null)
            {
                hud.LinkPlayer(this);
            }
            else
            {
                Debug.LogWarning("[HUD] 씬에서 HUDController를 찾을 수 없습니다. HUD 프리팹이 배치되었는지 확인하세요.");
            }
        }

        var bodySync = GetComponent<PlayerBodySync>();
        if (bodySync != null)
            bodySync.Initialize(this);

        WalkieTalkieManager.Instance?.RegisterPlayer(this);

        stateChangeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
    }

    public override void FixedUpdateNetwork()
    {
        if (HasStateAuthority)
        {
            ServerTickCaptureState();

            // PlayerState 변경 감지 -> 게임 종료 조건 판정
            foreach (var change in stateChangeDetector.DetectChanges(this))
            {
                if (change == nameof(NetPlayerState))
                {
                    GameSessionManager.Instance?.EvaluateEndCondition();
                }
            }
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

            if (Interaction != null && Interaction.TryGetCurrentTargetInfo(out NetworkId targetId, out int interactableId))
            {
                RPC_RequestInteract(targetId, interactableId);
            }
        }

        // 무전기 PTT 누르기 시작
        if (HasInputAuthority &&
            input.Buttons.IsSet(InputButtons.Walkie) &&
            !_prevWalkiePressed)
        {
            _prevWalkiePressed = true;
            GetHeldWalkieTalkie()?.RPC_RequestPTT(true);
        }

        // 무전기 PTT 떼기
        else if (HasInputAuthority &&
            !input.Buttons.IsSet(InputButtons.Walkie) &&
            _prevWalkiePressed)
        {
            _prevWalkiePressed = false;
            GetHeldWalkieTalkie()?.RPC_RequestPTT(false);
        }
    }

    /// <summary>
    /// 로컬 시각 효과 및 프레임 기반 센싱 처리
    /// </summary>
    private void Update()
    {
        // 내 기체가 아니거나, 정상 생존 상태가 아니면 센서 가동 중지
        if (!HasInputAuthority || NetPlayerState != PlayerState.Normal)
        {
            ClearLastHighlight();
            return;
        }

        // 아이템 조준 감지 및 외곽선 갱신
        UpdateItemHighlight();
    }

    /// <summary>
    /// 조준선(Raycast)에 닿은 아이템 또는 자식 퍼즐 부품의 외곽선을 실시간으로 제어한다.
    /// </summary>
    private void UpdateItemHighlight()
    {
        // 🛠️ 타겟의 NetworkId(본체)와 interactableId(자식 부품 번호)를 둘 다 가져옴!
        if (Interaction != null && Interaction.TryGetCurrentTargetInfo(out NetworkId targetId, out int interactableId))
        {
            if (Runner.TryFindObject(targetId, out NetworkObject obj))
            {
                ItemOutlineController outline = null;

                // 1. 자식 부품(퍼즐 버튼 등)을 조준 중인지 정밀 확인
                if (interactableId >= 0)
                {
                    // 조준 중인 특정 자식 부품을 찾음
                    IInteractable childInteractable = FindChildInteractable(obj.transform, interactableId);

                    if (childInteractable is MonoBehaviour mb)
                    {
                        // 그 자식 부품에 붙어있는 외곽선 모듈을 추출!
                        outline = mb.GetComponent<ItemOutlineController>();
                    }
                }

                // 2. 자식 부품에 모듈이 없거나, 일반 아이템(루트)인 경우 본체에서 추출
                if (outline == null)
                {
                    outline = obj.GetComponent<ItemOutlineController>();
                }

                // 모듈을 성공적으로 찾았다면 전원 공급
                if (outline != null)
                {
                    // 이전에 보던 것과 다른 새로운 타겟인 경우
                    if (_lastHighlightedOutline != outline)
                    {
                        _lastHighlightedOutline?.SetOutline(false); // 이전 외곽선 전원 차단
                        _lastHighlightedOutline = outline;
                        _lastHighlightedOutline.SetOutline(true);   // 새 외곽선 전원 인가
                    }
                    return; // 센서 가동 유지 중이므로 여기서 종료
                }
            }
        }

        // 아무것도 조준하지 않거나 모듈이 없으면 잔류 전원 초기화
        ClearLastHighlight();
    }

    /// <summary>
    /// 마지막으로 활성화된 외곽선 센서를 초기화한다.
    /// </summary>
    private void ClearLastHighlight()
    {
        if (_lastHighlightedOutline != null)
        {
            _lastHighlightedOutline.SetOutline(false);
            _lastHighlightedOutline = null;
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

    public bool CanUseGameplayInput()
    {
        return NetPlayerState == PlayerState.Normal && NetHideState == HideState.None;
    }

    public bool IsSpectatorState()
    {
        return NetPlayerState == PlayerState.Dead || NetPlayerState == PlayerState.Escaped;
    }

    /// <summary>
    /// 관전 대상이 될 수 있는 상태인지 반환한다.
    /// 현재 기준으로 Normal과 Captured를 관전 대상으로 허용한다.
    /// </summary>
    public bool CanBeSpectated()
    {
        return NetPlayerState == PlayerState.Normal || NetPlayerState == PlayerState.Captured;
    }

    public bool IsCaptureActive()
    {
        return NetPlayerState == PlayerState.Captured && NetCapturePhase == CapturePhase.Active;
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_RequestInteract(NetworkId targetId, int interactableId)
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

        IInteractable interactable = null;

        // 1. 자식 상호작용 ID가 있으면 해당 자식 우선 탐색
        if (interactableId >= 0)
        {
            interactable = FindChildInteractable(targetObject.transform, interactableId);
        }

        // 2. 못 찾았으면 루트 자체 interactable fallback
        if (interactable == null)
        {
            if (!PlayerInteraction.TryFindInteractable(targetObject.transform, out _, out IInteractable rootInteractable, out _))
                return;

            interactable = rootInteractable;
        }

        if (interactable == null)
            return;

        if (!interactable.CanInteract(this))
            return;

        interactable.Interact(this);
    }

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

    /// <summary>
    /// 루트 NetworkObject 아래에서 interactableId가 일치하는 자식 IInteractable을 찾는다.
    /// </summary>
    private IInteractable FindChildInteractable(Transform root, int interactableId)
    {
        if (root == null)
            return null;

        if (interactableId < 0)
            return null;

        IChildPuzzleInteractable[] children = root.GetComponentsInChildren<IChildPuzzleInteractable>(true);

        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].InteractableId != interactableId)
                continue;

            return children[i] as IInteractable;
        }

        return null;
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

        // 이미 왼손에 역할 아이템이 있으면 중복 지급 안 함
        if (NetLeftHandItem != null)
            return false;

        NetworkObject prefabToSpawn = null;

        if (NetPlayerRole == PlayerRole.Flashlight)
        {
            if (flashlightRoleItemPrefab == null)
            {
                Debug.LogWarning($"[PlayerController] flashlightRoleItemPrefab이 비어 있습니다. name={name}");
                return false;
            }
            prefabToSpawn = flashlightRoleItemPrefab;
        }
        else if (NetPlayerRole == PlayerRole.WalkieTalkie)
        {
            if (walkieTalkieRoleItemPrefab == null)
            {
                Debug.LogWarning($"[PlayerController] WalkieTalkieRoleItemPrefab이 비어 있습니다. name={name}");
                return false;
            }
            prefabToSpawn = walkieTalkieRoleItemPrefab;
        }
        else
        {
            return false;
        }

        NetworkObject spawnedItem = Runner.Spawn(
            prefabToSpawn,
            transform.position,
            transform.rotation,
            Object.InputAuthority
        );

        if (spawnedItem == null)
        {
            Debug.LogWarning($"[PlayerController] 역할 손전등 Spawn 실패. name={name}");
            return false;
        }

        // 무전기면 NetZone 배정
        if (spawnedItem.TryGetComponent(out WalkieTalkieItem walkieItem))
        {
            walkieItem.NetZone = NetZone;
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

    #region 무전기 관련 함수
    private void OnZoneChanged()
    {
        if (!HasInputAuthority) return;
        VoiceManager.Instance?.SwitchToGameMode(NetZone);
    }

    private void OnNearWalkieChanged()
    {
        if (!HasInputAuthority) return;
        VoiceManager.Instance?.SetTeammateGroup(NetIsNearReceiver);

        // 범위 진입/이탈 시 수신 구역 무전기의 화이트 노이즈도 갱신
        WalkieTalkieItem receiverWalkie = WalkieTalkieManager.Instance?.GetWalkieTalkieByZone(NetZone);
        receiverWalkie?.UpdateWhiteNoise();
    }

    private void OnNearSenderChanged()
    {
        if (!HasInputAuthority) return;

        bool isPTTActive = WalkieTalkieManager.Instance != null &&
                            WalkieTalkieManager.Instance.GetActiveSender() != PlayerRef.None;

        if (!isPTTActive) return;

        VoiceManager.Instance?.SetTeammateSenderGroup(NetIsNearSender);
    }

    public WalkieTalkieItem GetHeldWalkieTalkie()
    {
        if (NetLeftHandItem != null &&
            NetLeftHandItem.TryGetComponent(out WalkieTalkieItem leftWalkie))
            return leftWalkie;

        if (NetRightHandItem != null &&
            NetRightHandItem.TryGetComponent(out WalkieTalkieItem rightWalkie))
            return rightWalkie;

        return null;
    }

    #endregion

    #region 소리 이벤트 RPC

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_EmitNatural(float voicedB, Vector3 sourcePosition, Zone sourceZone)
    {
        float penalty = SoundEmitter.CalculateObstaclePenalty(sourcePosition, sourceZone);
        SoundEmitter.EmitToEventBus(SoundChannel.Natural, voicedB, sourcePosition, penalty, sourceZone);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_EmitWalkie(float voicedB, Vector3 sourcePosition, Zone receiverZone)
    {
        SoundEmitter.EmitToEventBus(SoundChannel.Walkie, voicedB, sourcePosition, 0f, receiverZone);
    }

    #endregion
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

        // 🛠️ [정밀 판별] 뺏어올 아이템이 내 직업 전용 장비인지 스캔!
        bool isMyProfessionalGear = false;
        if (targetItem.NetIsRoleItem)
        {
            if (NetPlayerRole == PlayerRole.WalkieTalkie && targetItem.NetItemType == ItemType.WalkieTalkie)
                isMyProfessionalGear = true;
            else if (NetPlayerRole == PlayerRole.Flashlight && targetItem.NetItemType == ItemType.Flashlight)
                isMyProfessionalGear = true;
        }

        // 🛠️ 내 전용 장비이고, 내 왼손이 비어있다면 '왼손'으로 탈취!
        if (isMyProfessionalGear && NetLeftHandItem == null)
        {
            target.NetRightHandItem = default;
            NetLeftHandItem = targetItem.Object;

            // 🚨 주의: AssignInputAuthority는 시스템 에러를 유발하므로 절대 쓰지 않음!
            targetItem.OnEquipped(this);
            return true;
        }

        // 🛠️ 남의 장비이거나, 내 왼손이 차있다면 기존처럼 '오른손'으로 탈취!
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

    /// <summary>
    /// 플레이어 상호작용이 가능한지 검사
    /// 우선순위
    /// 1. 대상이 Captured + Active면 구출 가능 여부 검사
    /// 2. 오른손 아이템 탈취 가능 여부 검사
    /// </summary>
    public bool CanInteract(PlayerController actor)
    {
        if (CanBeRescuedBy(actor))
            return true;

        if (CanBeStolenFromBy(actor))
            return true;

        return false;
    }

    /// <summary>
    /// 플레이어 상호작용이 실제로 성립했을 때 서버에서 실행
    /// </summary>
    public void Interact(PlayerController actor)
    {
        if (!HasStateAuthority)
            return;

        if (CanBeRescuedBy(actor))
        {
            ServerTryRescueBy(actor);
            return;
        }

        if (CanBeStolenFromBy(actor))
        {
            actor.ServerTryTakeRightHandFrom(this);
        }
    }

    public string GetPromptText(PlayerController actor)
    {
        if (CanBeRescuedBy(actor))
            return "구출하기";

        if (CanBeStolenFromBy(actor))
            return "오른손 아이템 뺏기";

        return string.Empty;
    }

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


        SetInputLock(true, false);
        //NetMovementLocked = true;
        //NetLookLocked = false;

        ServerForceDropAllHeldItems();
        ApplyImmediateTraumaOnCapture();

        // 포획 로그 추가
        GameEventLogger.Instance?.LogCaptured(GetNickname(), GetSlotIndex());

        if (NetAftereffectPercent >= traumaDeathThreshold)
        {
            ServerEnterDead();
            return true;
        }

        // 한 구역 전원이 Captured 상태면 사망 판정
        GameSessionManager.Instance?.CheckZoneAllCaptured(NetZone);

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
        SetInputLock(false, false);
        //NetMovementLocked = false;
        //NetLookLocked = false;

        // 구출 로그 추가
        GameEventLogger.Instance?.LogRescued(GetNickname(), GetSlotIndex());

        return true;
    }


    /// <summary>
    /// 현재 플레이어가 다른 플레이어에게 구출될 수 있는지 검사
    /// 대상은 Captured + Active 상태여야 하고
    /// 구출자는 Normal 상태에서 일반 입력이 가능해야 한다.
    /// </summary>
    public bool CanBeRescuedBy(PlayerController actor)
    {
        if (actor == null || actor == this)
            return false;

        if (!actor.CanUseGameplayInput())
            return false;

        if (NetPlayerState != PlayerState.Captured)
            return false;

        if (NetCapturePhase != CapturePhase.Active)
            return false;

        return true;
    }

    /// <summary>
    /// 현재 플레이어가 다른 플레이어에게 오른손 아이템을 탈취당할 수 있는지 검사
    /// 기존 플레이어 상호작용의 탈취 조건을 별도 함수로 분리
    /// </summary>
    private bool CanBeStolenFromBy(PlayerController actor)
    {
        if (actor == null || actor == this)
            return false;

        if (!actor.CanUseGameplayInput())
            return false;

        if (!CanUseGameplayInput())
            return false;

        return NetRightHandItem != null;
    }

    /// <summary>
    /// 서버에서 실제 구출을 실행
    /// 조건이 맞으면 Captured 상태를 해제하고 Normal 상태로 복귀
    /// </summary>
    public bool ServerTryRescueBy(PlayerController actor)
    {
        if (!HasStateAuthority)
            return false;

        if (!CanBeRescuedBy(actor))
            return false;

        return ServerExitCapturedToNormal();
    }

    public void ServerEnterDead()
    {
        if (!HasStateAuthority)
            return;

        NetPlayerState = PlayerState.Dead;
        SaveFinalPlayerState(PlayerState.Dead);

        // 사망 로그 추가
        GameEventLogger.Instance?.LogDead(GetNickname(), GetSlotIndex());

        NetHideState = HideState.None;
        NetCapturePhase = CapturePhase.None;
        NetCurrentHideSpotId = default;
        NetCaptureTransitionTimer = TickTimer.None;
        NetCaptureExpireTimer = TickTimer.None;
        NetMovementLocked = true;
        NetLookLocked = true;
    }

    public void ServerEnterEscaped()
    {
        if (!HasStateAuthority)
            return;

        NetPlayerState = PlayerState.Escaped;
        SaveFinalPlayerState(PlayerState.Escaped);

        // 탈출 로그 추가
        GameEventLogger.Instance?.LogEscaped(GetNickname(), GetSlotIndex());

        NetHideState = HideState.None;
        NetCapturePhase = CapturePhase.None;
        NetCurrentHideSpotId = default;
        NetCaptureTransitionTimer = TickTimer.None;
        NetCaptureExpireTimer = TickTimer.None;
        NetMovementLocked = true;
        NetLookLocked = true;
    }

    //왼손 아이템 줍기
    public bool ServerTryPickupLeftHand(ItemObject item)
    {
        if (!HasStateAuthority || item == null)
            return false;

        if (!CanUseGameplayInput())
            return false;

        if (!item.CanInteract(this))
            return false;

        // 🛠️ [안전장치] 왼손은 자의로 아이템을 버리거나 교체할 수 없음!
        // 이미 왼손에 직업 아이템이 쥐어져 있다면 픽업 모터를 정지시킴.
        if (NetLeftHandItem != null)
            return false;

        // 손이 비어있을 때만 장착 승인
        NetLeftHandItem = item.Object;
        item.OnEquipped(this);
        return true;
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

    // PlayerData에서 닉네임을 가져옴 (로그용)
    private string GetNickname()
    {
        var data = Runner.GetPlayerObject(Object.InputAuthority)?.GetComponent<PlayerData>();
        return data != null ? data.Nickname.ToString() : "Unknown";
    }

    // PlayerData에서 SlotIndex를 가져옴 (로그용)
    private int GetSlotIndex()
    {
        var data = Runner.GetPlayerObject(Object.InputAuthority)?.GetComponent<PlayerData>();
        return data != null ? data.SlotIndex : -1;
    }

    private void SaveFinalPlayerState(PlayerState state)
    {
        var data = Runner.GetPlayerObject(Object.InputAuthority)?.GetComponent<PlayerData>();
        if (data != null) data.FinalPlayerState = state;
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

    /// <summary>
    /// 플레이 모드에서 인스펙터 컨텍스트 메뉴로 강제 포획 테스트를 실행한다.
    /// debugCaptureAnchor가 있으면 그 위치/회전을 사용하고,
    /// 없으면 현재 플레이어 위치/회전을 사용한다.
    /// </summary>
    [ContextMenu("Debug/Force Capture")]
    private void DebugForceCapture()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[PlayerController] 플레이 모드에서만 테스트할 수 있습니다.", this);
            return;
        }

        if (!HasStateAuthority)
        {
            Debug.LogWarning("[PlayerController] StateAuthority가 아닌 객체는 강제 포획 테스트를 실행할 수 없습니다.", this);
            return;
        }

        Vector3 targetPosition = debugCaptureAnchor != null ? debugCaptureAnchor.position : transform.position;
        Quaternion targetRotation = debugCaptureAnchor != null ? debugCaptureAnchor.rotation : transform.rotation;

        ServerEnterCaptured(targetPosition, targetRotation);
    }

    /// <summary>
    /// 플레이 모드에서 인스펙터 컨텍스트 메뉴로 강제 구출 테스트를 실행한다.
    /// Captured 상태일 때 Normal 상태로 즉시 복귀시킨다.
    /// </summary>
    [ContextMenu("Debug/Force Rescue")]
    private void DebugForceRescue()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[PlayerController] 플레이 모드에서만 테스트할 수 있습니다.", this);
            return;
        }

        if (!HasStateAuthority)
        {
            Debug.LogWarning("[PlayerController] StateAuthority가 아닌 객체는 강제 구출 테스트를 실행할 수 없습니다.", this);
            return;
        }

        ServerExitCapturedToNormal();
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_ProcessMinigameSuccess()
    {
        // 포획 상태가 아니라면 무시
        if (NetPlayerState != PlayerState.Captured) return;

        // 🛠️ 기획서 1-4: 후유증 3% 감소 
        NetAftereffectPercent = Mathf.Max(0f, NetAftereffectPercent - 3.0f);

        // 🛠️ [핵심 부품 추가] 
        // 깎인 만큼 '살아남을 수 있는 제한 시간(타이머)'도 실질적으로 늘려줘야 해!
        if (NetCaptureExpireTimer.IsRunning)
        {
            // 타이머의 남은 시간에 3초(3%에 해당하는 시간)를 더해줌
            float currentRemaining = NetCaptureExpireTimer.RemainingTime(Runner).GetValueOrDefault(0);
            NetCaptureExpireTimer = TickTimer.CreateFromSeconds(Runner, currentRemaining + 3.0f);
        }

        Debug.Log($"[미니게임] 서버 동기화 완료! 후유증 3% 삭감. 현재 후유증: {NetAftereffectPercent}%");
    }


    #region 게임 종료 이벤트 확인용 RPC
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void Rpc_DebugSetState(PlayerState state)
    {
        if (state == PlayerState.Dead)
            ServerEnterDead();
        else if (state == PlayerState.Escaped)
            ServerEnterEscaped();
        else
            NetPlayerState = state;
    }
    #endregion
}