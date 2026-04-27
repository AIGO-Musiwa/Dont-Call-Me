using Fusion;
using UnityEngine;

/// <summary>
/// 플레이어의 네트워크 입력, 상호작용, 손 아이템, 포획 상태 등을 총괄하는 메인 컨트롤러.
/// </summary>
[RequireComponent(typeof(PlayerKCCMotor))]
[RequireComponent(typeof(PlayerLookView))]
[RequireComponent(typeof(PlayerInteraction))]
[RequireComponent(typeof(PlayerHandView))]
[RequireComponent(typeof(PlayerFlashlightView))]
public class PlayerController : NetworkBehaviour, IInteractable
{
    public PlayerKCCMotor KCCMotor { get; private set; }                 // 이동 모터 참조
    public PlayerLookView LookView { get; private set; }                 // 시야 제어 참조
    public PlayerInteraction Interaction { get; private set; }           // 상호작용 탐지 참조
    public PlayerHandView HandView { get; private set; }                 // 손 아이템 시각 표현 참조
    public PlayerFlashlightView FlashlightView { get; private set; }     // 손전등 시각 표현 참조
    public PlayerSpectatorController SpectatorController { get; private set; } // 관전 모드 제어 참조

    [Header("역할 아이템 프리팹")]
    [SerializeField] private NetworkObject flashlightRoleItemPrefab;     // 손전등 역할 아이템 프리팹
    [SerializeField] private NetworkObject walkieTalkieRoleItemPrefab;   // 무전기 역할 아이템 프리팹

    [Header("오른손 드랍")]
    [SerializeField] private float rightHandDropForwardOffset = 0.8f;    // 오른손 드랍 전방 오프셋
    [SerializeField] private float rightHandDropUpOffset = 0.5f;         // 오른손 드랍 상방 오프셋
    [SerializeField] private float rightHandDropImpulse = 2.5f;          // 오른손 드랍 임펄스

    [Header("왼손 드랍")]
    [SerializeField] private float leftHandDropForwardOffset = 0.6f;     // 왼손 드랍 전방 오프셋
    [SerializeField] private float leftHandDropSideOffset = -0.25f;      // 왼손 드랍 좌우 오프셋
    [SerializeField] private float leftHandDropUpOffset = 0.45f;         // 왼손 드랍 상방 오프셋
    [SerializeField] private float leftHandDropImpulse = 2.0f;           // 왼손 드랍 임펄스

    [Header("포획")]
    [SerializeField] private float captureTransitionSeconds = 1.0f;      // 포획 전환 연출 시간
    [SerializeField] private float traumaPenaltyCapture1 = 10f;          // 첫 포획 후유증 증가량
    [SerializeField] private float traumaPenaltyCapture2 = 20f;          // 두 번째 포획 후유증 증가량
    [SerializeField] private float traumaPenaltyCapture3Plus = 30f;      // 세 번째 이상 포획 후유증 증가량
    [SerializeField] private float traumaIncreasePerSecond = 1f;         // 포획 중 초당 후유증 증가량
    [SerializeField] private float traumaDeathThreshold = 100f;          // 후유증 사망 임계값
    [SerializeField] private float rescueBaseTimeSeconds = 100f;         // 기본 구조 제한 시간

    [Networked, OnChangedRender(nameof(OnPlayerStateChanged))]
    public PlayerState NetPlayerState { get; set; }                      // 현재 플레이어 상태
    [Networked] public PlayerRole NetPlayerRole { get; set; }            // 현재 플레이어 역할
    [Networked, OnChangedRender(nameof(OnZoneChanged))]
    public Zone NetZone { get; set; }                                    // 현재 플레이어 구역

    [Networked] public HideState NetHideState { get; set; }              // 현재 은신 상태
    [Networked] public CapturePhase NetCapturePhase { get; set; }        // 현재 포획 페이즈
    [Networked] public float NetAftereffectPercent { get; set; }         // 현재 후유증 퍼센트
    [Networked] public int NetCaptureCount { get; set; }                 // 누적 포획 횟수
    [Networked] public TickTimer NetCaptureTransitionTimer { get; set; } // 포획 전환 타이머
    [Networked] public TickTimer NetCaptureExpireTimer { get; set; }     // 포획 후 사망 타이머
    [Networked] public Vector3 NetCaptureAnchorPosition { get; set; }    // 포획 시 이동할 위치
    [Networked] public Quaternion NetCaptureAnchorRotation { get; set; } // 포획 시 이동할 회전
    [Networked] public NetworkId NetCurrentHideSpotId { get; set; }      // 현재 숨은 은신처 NetworkId

    [Networked] public NetworkObject NetLeftHandItem { get; set; }       // 현재 왼손 아이템
    [Networked] public NetworkObject NetRightHandItem { get; set; }      // 현재 오른손 아이템

    [Networked] public NetworkBool NetMovementLocked { get; set; }       // 이동 잠금 여부
    [Networked] public NetworkBool NetLookLocked { get; set; }           // 시야 잠금 여부
    [Networked] public NetworkBool NetIsCrouching { get; set; }          // 앉기 상태

    [Networked, OnChangedRender(nameof(OnNearWalkieChanged))]
    public NetworkBool NetIsNearReceiver { get; set; }                   // 수신 무전기 근처 여부

    [Networked, OnChangedRender(nameof(OnNearSenderChanged))]
    public NetworkBool NetIsNearSender { get; set; }                     // 송신 무전기 근처 여부

    private int _lastInteractRequestTick = -1;                           // 마지막 일반 상호작용 요청 tick
    private bool _prevWalkiePressed;                                     // 이전 tick 무전기 입력 상태

    private ChangeDetector stateChangeDetector;                          // PlayerState 변경 감지기
    private ItemOutlineController _lastHighlightedOutline;               // 마지막으로 강조 중인 외곽선 장치

    private HUDController _localHUD;                                     // 🛠️ 로컬 UI 상태 업데이트를 위한 캐시

    [SerializeField] private Transform debugCaptureAnchor;               // 테스트용 임시 포획 Anchor

    public override void Spawned()
    {
        KCCMotor = GetComponent<PlayerKCCMotor>();                       // 이동 모터 캐시
        LookView = GetComponent<PlayerLookView>();                       // 시야 제어 캐시
        Interaction = GetComponent<PlayerInteraction>();                 // 상호작용 센서 캐시
        HandView = GetComponent<PlayerHandView>();                       // 손 시각 표현 캐시
        FlashlightView = GetComponent<PlayerFlashlightView>();           // 손전등 시각 표현 캐시
        SpectatorController = FindFirstObjectByType<PlayerSpectatorController>(FindObjectsInactive.Include); // 관전 컨트롤러 탐색

        KCCMotor.Initialize(this);                                       // 이동 모터 초기화
        LookView.Initialize(this);                                       // 시야 제어 초기화
        Interaction.Initialize(this);                                    // 상호작용 초기화
        HandView.Initialize(this);                                       // 손 시각 표현 초기화
        FlashlightView.Initialize(this);                                 // 손전등 시각 표현 초기화
        SpectatorController.Initialize(this);                            // 관전 제어 초기화

        if (HasStateAuthority)
        {
            NetPlayerState = PlayerState.Normal;                         // 시작 상태는 Normal
            NetHideState = HideState.None;                               // 시작 은신 상태 없음
            NetCapturePhase = CapturePhase.None;                         // 시작 포획 페이즈 없음
            NetAftereffectPercent = 0f;                                  // 후유증 초기화
            NetCaptureCount = 0;                                         // 포획 횟수 초기화
            NetCaptureTransitionTimer = TickTimer.None;                  // 전환 타이머 초기화
            NetCaptureExpireTimer = TickTimer.None;                      // 사망 타이머 초기화
            NetCaptureAnchorPosition = transform.position;               // 포획 앵커 위치 초기화
            NetCaptureAnchorRotation = transform.rotation;               // 포획 앵커 회전 초기화
            NetCurrentHideSpotId = default;                              // 은신처 참조 초기화

            NetLeftHandItem = default;                                   // 왼손 아이템 초기화
            NetRightHandItem = default;                                  // 오른손 아이템 초기화

            SetInputLock(false, false);                                  // 입력 잠금 해제
        }

        if (HasInputAuthority)
        {
            HUDController hud = UnityEngine.Object.FindAnyObjectByType<HUDController>(); // HUD 탐색

            if (hud != null)
            {
                hud.LinkPlayer(this);                                    // HUD와 플레이어 연결
                _localHUD = hud;                                         // 🛠️ HUD 캐시 저장
            }
            else
            {
                Debug.LogWarning("[HUD] 씬에서 HUDController를 찾을 수 없습니다. HUD 프리팹이 배치되었는지 확인하세요.");
            }
        }

        var bodySync = GetComponent<PlayerBodySync>();                   // 원격 바디 동기화 스크립트 탐색
        if (bodySync != null)
            bodySync.Initialize(this);                                   // 있으면 초기화

        WalkieTalkieManager.Instance?.RegisterPlayer(this);              // 무전기 매니저에 플레이어 등록

        stateChangeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState); // 상태 변경 감지기 생성
    }

    public override void FixedUpdateNetwork()
    {
        if (HasStateAuthority)
        {
            ServerTickCaptureState();                                    // 포획 상태 서버 업데이트

            foreach (var change in stateChangeDetector.DetectChanges(this))
            {
                if (change == nameof(NetPlayerState))
                    GameSessionManager.Instance?.EvaluateEndCondition(); // 상태 변경 시 게임 종료 조건 평가
            }
        }

        if (!GetInput(out PlayerNetworkInput input))
            return;                                                      // 입력 없으면 종료

        KCCMotor.Simulate(input, NetMovementLocked, NetLookLocked);      // 이동/시야 시뮬레이션

        if (HasInputAuthority && SpectatorController != null && IsSpectatorState())
            SpectatorController.TickSpectatorInput(input);               // 관전 상태 입력 처리

        bool interactPressedThisTick = input.Buttons.IsSet(InputButtons.InteractPressed); // 이번 tick 눌림 순간 입력
        bool interactHeldThisTick = input.Buttons.IsSet(InputButtons.InteractHeld);       // 이번 tick 유지 입력

        // 일반 클릭 상호작용 처리
        if (HasInputAuthority &&
            interactPressedThisTick &&
            Runner.Tick != _lastInteractRequestTick)
        {
            _lastInteractRequestTick = Runner.Tick;                      // 중복 클릭 방지용 tick 기록

            if (NetHideState != HideState.None)
            {
                RPC_RequestExitHide();                                   // 숨은 상태면 상호작용 대신 은신 해제
            }
            else if (Interaction != null && Interaction.TryGetCurrentTargetInfo(out NetworkId targetId, out int interactableId))
            {
                RPC_RequestInteract(targetId, interactableId);           // 일반 클릭 상호작용 요청
            }
        }

        // Hold 상호작용 처리
        if (HasInputAuthority &&
            interactHeldThisTick &&
            Interaction != null &&
            Interaction.TryGetCurrentTargetInfo(out NetworkId holdTargetId, out int holdInteractableId))
        {
            RPC_RequestHoldInteract(holdTargetId, holdInteractableId, Runner.DeltaTime); // 유지 중이면 매 tick Hold 요청
        }

        // 무전기 PTT 누르기 시작
        if (HasInputAuthority &&
            input.Buttons.IsSet(InputButtons.Walkie) &&
            !_prevWalkiePressed)
        {
            _prevWalkiePressed = true;
            if (GetHeldWalkieTalkie() != null)
                RPC_RequestPTT(true);
        }
        // 무전기 PTT 떼기
        else if (HasInputAuthority &&
                 !input.Buttons.IsSet(InputButtons.Walkie) &&
                 _prevWalkiePressed)
        {
            _prevWalkiePressed = false;
            if (GetHeldWalkieTalkie() != null)
                RPC_RequestPTT(false);
        }
    }

    /// <summary>
    /// 로컬 시각 효과 및 프레임 기반 센싱 처리
    /// </summary>
    private void Update()
    {
        if (!HasInputAuthority || NetPlayerState != PlayerState.Normal)
        {
            ClearLastHighlight();                                        // 조준 강조 해제
            if (_localHUD != null) _localHUD.UpdateInteractionGauge(0f, 10f); // 🛠️ 강제 HUD 게이지 초기화
            return;
        }

        UpdateItemHighlight();                                           // 조준 대상 외곽선 갱신
        UpdateInteractionHUD();                                          // 🛠️ 조준 대상 UI 게이지 갱신
    }

    // 🛠️ [신규 부품] 조준 중인 오브젝트의 진행도를 HUD에 실시간으로 그린다
    private void UpdateInteractionHUD()
    {
        if (_localHUD == null) return;

        if (Interaction != null && Interaction.TryGetCurrentTargetInfo(out NetworkId targetId, out int interactableId))
        {
            if (Runner.TryFindObject(targetId, out NetworkObject targetObj))
            {
                // 현재 바라보고 있는 대상이 라디오라면
                if (targetObj.TryGetComponent(out Radio radio))
                {
                    // 수리 진행도를 HUD에 전달 (최대 수리 시간 10초 기준)
                    _localHUD.UpdateInteractionGauge(radio.RepairProgress, 10f);
                    return;
                }
            }
        }

        // 라디오를 보고 있지 않거나 유효하지 않으면 게이지 전원 차단
        _localHUD.UpdateInteractionGauge(0f, 10f);
    }

    /// <summary>
    /// 조준선(Raycast)에 닿은 아이템 또는 자식 퍼즐 부품의 외곽선을 실시간으로 제어한다.
    /// </summary>
    private void UpdateItemHighlight()
    {
        if (Interaction != null && Interaction.TryGetCurrentTargetInfo(out NetworkId targetId, out int interactableId))
        {
            if (Runner.TryFindObject(targetId, out NetworkObject obj))
            {
                ItemOutlineController outline = null;                    // 이번 프레임 목표 외곽선 참조

                if (interactableId >= 0)
                {
                    IInteractable childInteractable = FindChildInteractable(obj.transform, interactableId); // 자식 부품 탐색

                    if (childInteractable is MonoBehaviour mb)
                        outline = mb.GetComponent<ItemOutlineController>(); // 자식 외곽선 모듈 찾기
                }

                if (outline == null)
                    outline = obj.GetComponent<ItemOutlineController>(); // 자식에 없으면 루트 본체에서 찾기

                if (outline != null)
                {
                    if (_lastHighlightedOutline != outline)
                    {
                        _lastHighlightedOutline?.SetOutline(false);      // 이전 외곽선 끄기
                        _lastHighlightedOutline = outline;               // 새 외곽선 저장
                        _lastHighlightedOutline.SetOutline(true);        // 새 외곽선 켜기
                    }
                    return;
                }
            }
        }

        ClearLastHighlight();                                            // 조준 대상이 없으면 외곽선 해제
    }

    /// <summary>
    /// 마지막으로 활성화된 외곽선 센서를 초기화한다.
    /// </summary>
    private void ClearLastHighlight()
    {
        if (_lastHighlightedOutline != null)
        {
            _lastHighlightedOutline.SetOutline(false);                   // 외곽선 끄기
            _lastHighlightedOutline = null;                              // 참조 초기화
        }
    }

    public Transform GetCameraLightRoot()
    {
        return LookView != null ? LookView.GetCameraLightRoot() : null;  // 카메라 기준 손전등 루트 반환
    }

    public void SetInputLock(bool movementLocked, bool lookLocked)
    {
        if (!HasStateAuthority)
            return;

        NetMovementLocked = movementLocked;                              // 이동 잠금 설정
        NetLookLocked = lookLocked;                                      // 시야 잠금 설정
    }

    public bool CanUseGameplayInput()
    {
        return NetPlayerState == PlayerState.Normal && NetHideState == HideState.None; // 일반 상태 + 비은신일 때만 게임 입력 허용
    }

    public bool IsSpectatorState()
    {
        return NetPlayerState == PlayerState.Dead || NetPlayerState == PlayerState.Escaped; // 관전 상태인지 반환
    }

    /// <summary>
    /// 관전 대상이 될 수 있는 상태인지 반환한다.
    /// 현재 기준으로 Normal과 Captured를 관전 대상으로 허용한다.
    /// </summary>
    public bool CanBeSpectated()
    {
        return NetPlayerState == PlayerState.Normal || NetPlayerState == PlayerState.Captured; // 관전 허용 상태
    }

    public bool IsCaptureActive()
    {
        return NetPlayerState == PlayerState.Captured && NetCapturePhase == CapturePhase.Active; // 현재 포획 활성 상태인지
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

        float maxDistance = Interaction != null ? Interaction.InteractDistance : 2f; // 최대 상호작용 거리 계산

        if (!IsTargetWithinInteractDistance(targetObject, maxDistance))
            return;

        IInteractable interactable = null;                               // 최종 상호작용 대상 참조

        if (interactableId >= 0)
            interactable = FindChildInteractable(targetObject.transform, interactableId); // 자식 상호작용 우선 탐색

        if (interactable == null)
        {
            if (!PlayerInteraction.TryFindInteractable(targetObject.transform, out _, out IInteractable rootInteractable, out _))
                return;

            interactable = rootInteractable;                              // 루트 상호작용 fallback
        }

        if (interactable == null)
            return;

        if (!interactable.CanInteract(this))
            return;

        interactable.Interact(this);                                      // 일반 클릭 상호작용 실행
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

        hideSpot.RequestExit(this);                                       // 현재 은신처에 퇴장 요청
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

        IChildPuzzleInteractable[] children = root.GetComponentsInChildren<IChildPuzzleInteractable>(true); // 자식 퍼즐 상호작용 목록 수집

        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].InteractableId != interactableId)
                continue;

            return children[i] as IInteractable;                          // 일치하는 자식 상호작용 반환
        }

        return null;
    }

    public ItemObject GetLeftHandItemObject()
    {
        return TryGetItemObject(NetLeftHandItem, out ItemObject item) ? item : null; // 왼손 ItemObject 반환
    }

    public ItemObject GetRightHandItemObject()
    {
        return TryGetItemObject(NetRightHandItem, out ItemObject item) ? item : null; // 오른손 ItemObject 반환
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

        NetLeftHandItem = item.Object;                                    // 왼손에 아이템 장착
        item.OnEquipped(this);                                            // 아이템 장착 처리
        return true;
    }

    public bool ServerGrantRoleItemForCurrentRole()
    {
        if (!HasStateAuthority)
            return false;

        if (NetLeftHandItem != null)
            return false;                                                 // 이미 왼손에 역할 아이템이 있으면 중복 지급 안 함

        NetworkObject prefabToSpawn = null;                               // 지급할 역할 아이템 프리팹 참조

        if (NetPlayerRole == PlayerRole.Flashlight)
        {
            if (flashlightRoleItemPrefab == null)
            {
                Debug.LogWarning($"[PlayerController] flashlightRoleItemPrefab이 비어 있습니다. name={name}");
                return false;
            }
            prefabToSpawn = flashlightRoleItemPrefab;                     // 손전등 지급
        }
        else if (NetPlayerRole == PlayerRole.WalkieTalkie)
        {
            if (walkieTalkieRoleItemPrefab == null)
            {
                Debug.LogWarning($"[PlayerController] WalkieTalkieRoleItemPrefab이 비어 있습니다. name={name}");
                return false;
            }
            prefabToSpawn = walkieTalkieRoleItemPrefab;                   // 무전기 지급
        }
        else
        {
            return false;
        }

        NetworkObject spawnedItem = Runner.Spawn(
            prefabToSpawn,
            transform.position,
            transform.rotation,
            Object.InputAuthority);                                       // 역할 아이템 스폰

        if (spawnedItem == null)
        {
            Debug.LogWarning($"[PlayerController] 역할 아이템 Spawn 실패. name={name}");
            return false;
        }

        if (spawnedItem.TryGetComponent(out WalkieTalkieItem walkieItem))
            walkieItem.NetZone = NetZone;                                 // 무전기면 현재 구역 배정

        ItemObject item = spawnedItem.GetComponent<ItemObject>();         // ItemObject 추출
        if (item == null)
        {
            Debug.LogWarning($"[PlayerController] Spawn된 역할 아이템에 ItemObject가 없습니다. name={spawnedItem.name}");
            Runner.Despawn(spawnedItem);
            return false;
        }

        if (!ServerEquipLeftHand(item))
        {
            Runner.Despawn(spawnedItem);                                  // 장착 실패 시 스폰 취소
            return false;
        }

        return true;
    }

    #region 무전기 관련 함수

    private void OnPlayerStateChanged()
    {
        GetComponent<PlayerVoiceController>()?.OnNetPlayerStateChanged();
    }

    private void OnZoneChanged()
    {
        if (!HasInputAuthority)
            return;

        VoiceManager.Instance?.SwitchToGameMode(NetZone);                 // 현재 구역 기준 음성 채널 전환
    }

    private void OnNearWalkieChanged()
    {
        if (!HasInputAuthority)
            return;

        VoiceManager.Instance?.SetTeammateGroup(NetIsNearReceiver);       // 수신 그룹 상태 갱신

        WalkieTalkieItem receiverWalkie = WalkieTalkieManager.Instance?.GetWalkieTalkieByZone(NetZone); // 현재 구역 무전기 찾기
        receiverWalkie?.UpdateWhiteNoise();                               // 화이트 노이즈 상태 갱신
    }

    private void OnNearSenderChanged()
    {
        if (!HasInputAuthority)
            return;

        bool isPTTActive = WalkieTalkieManager.Instance != null &&
                           WalkieTalkieManager.Instance.GetActiveSender() != PlayerRef.None; // 현재 송신 중인지 확인

        if (!isPTTActive)
            return;

        VoiceManager.Instance?.SetTeammateSenderGroup(NetIsNearSender);   // 송신 그룹 상태 갱신
    }

    public WalkieTalkieItem GetHeldWalkieTalkie()
    {
        if (NetLeftHandItem != null &&
            NetLeftHandItem.TryGetComponent(out WalkieTalkieItem leftWalkie))
            return leftWalkie;                                            // 왼손 무전기 반환

        if (NetRightHandItem != null &&
            NetRightHandItem.TryGetComponent(out WalkieTalkieItem rightWalkie))
            return rightWalkie;                                           // 오른손 무전기 반환

        return null;                                                      // 들고 있는 무전기 없음
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_RequestPTT(bool isPressed)
    {
        WalkieTalkieItem walkie = GetHeldWalkieTalkie();
        if (walkie == null) return;

        WalkieTalkieManager.Instance?.HandlePTTRequest(Object.InputAuthority, isPressed, walkie);
    }

    #endregion

    #region 소리 이벤트 RPC

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_EmitNatural(float voicedB, Vector3 sourcePosition, Zone sourceZone)
    {
        float penalty = SoundEmitter.CalculateObstaclePenalty(sourcePosition, sourceZone); // 장애물 페널티 계산
        SoundEmitter.EmitToEventBus(SoundChannel.Natural, voicedB, sourcePosition, penalty, sourceZone); // 일반 음성 이벤트 발행
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_EmitWalkie(float voicedB, Vector3 sourcePosition, Zone receiverZone)
    {
        SoundEmitter.EmitToEventBus(SoundChannel.Walkie, voicedB, sourcePosition, 0f, receiverZone); // 무전기 음성 이벤트 발행
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

        NetRightHandItem = item.Object;                                   // 오른손에 아이템 장착
        item.OnEquipped(this);                                            // 아이템 장착 처리
        return true;
    }

    public bool ServerClearRightHandItemWithoutDrop()
    {
        if (!HasStateAuthority)
            return false;

        if (NetRightHandItem == null)
            return false;

        NetRightHandItem = default;                                       // 드랍 없이 오른손 참조만 해제
        return true;
    }

    public bool ServerDropRightHandItem()
    {
        if (!HasStateAuthority)
            return false;

        if (!TryGetItemObject(NetRightHandItem, out ItemObject item))
            return false;

        Vector3 dropPosition = GetRightHandDropPosition();                // 드랍 위치 계산
        Vector3 dropForward = transform.forward;                          // 드랍 전방 방향 계산

        NetRightHandItem = default;                                       // 오른손 참조 해제
        item.OnDropped(dropPosition, dropForward, rightHandDropImpulse);  // 아이템 드랍 처리
        return true;
    }

    public bool ServerDropLeftHandItem()
    {
        if (!HasStateAuthority)
            return false;

        if (!TryGetItemObject(NetLeftHandItem, out ItemObject item))
            return false;

        Vector3 dropPosition = GetLeftHandDropPosition();                 // 드랍 위치 계산
        Vector3 dropForward = transform.forward;                          // 드랍 전방 방향 계산

        NetLeftHandItem = default;                                        // 왼손 참조 해제
        item.OnDropped(dropPosition, dropForward, leftHandDropImpulse);   // 아이템 드랍 처리
        return true;
    }

    public void ServerForceDropAllHeldItems()
    {
        if (!HasStateAuthority)
            return;

        ServerDropLeftHandItem();                                         // 왼손 아이템 드랍
        ServerDropRightHandItem();                                        // 오른손 아이템 드랍
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

        bool isMyProfessionalGear = false;                                // 뺏어올 아이템이 내 직업 장비인지 판별용

        if (targetItem.NetIsRoleItem)
        {
            if (NetPlayerRole == PlayerRole.WalkieTalkie && targetItem.NetItemType == ItemType.WalkieTalkie)
                isMyProfessionalGear = true;
            else if (NetPlayerRole == PlayerRole.Flashlight && targetItem.NetItemType == ItemType.Flashlight)
                isMyProfessionalGear = true;
        }

        if (isMyProfessionalGear && NetLeftHandItem == null)
        {
            target.NetRightHandItem = default;                            // 상대 오른손 아이템 제거

            if (targetItem is WalkieTalkieItem walkieLeft)
                WalkieTalkieManager.Instance?.OnWalkieTalkieDropped(target.Object.InputAuthority, walkieLeft);

            NetLeftHandItem = targetItem.Object;                          // 내 왼손에 탈취 장착
            targetItem.OnEquipped(this);                                  // 장착 처리
            return true;
        }

        if (!EnsureRightHandEmpty())
            return false;

        target.NetRightHandItem = default;                                // 상대 오른손 아이템 제거
        NetRightHandItem = targetItem.Object;                             // 내 오른손에 탈취 장착
        targetItem.OnEquipped(this);                                      // 장착 처리

        return true;
    }

    public void ServerEquipRightHand(ItemObject item)
    {
        ServerTryPickupRightHand(item);                                   // 오른손 장착 시도 위임
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
            ServerTryRescueBy(actor);                                     // 구출 시도
            return;
        }

        if (CanBeStolenFromBy(actor))
            actor.ServerTryTakeRightHandFrom(this);                       // 오른손 아이템 탈취
    }

    public string GetPromptText(PlayerController actor)
    {
        if (CanBeRescuedBy(actor))
            return "구출하기";                                            // 구출 프롬프트

        if (CanBeStolenFromBy(actor))
            return "오른손 아이템 뺏기";                                  // 탈취 프롬프트

        return string.Empty;
    }

    public bool ServerEnterCaptured(Vector3 captureAnchorPosition, Quaternion captureAnchorRotation)
    {
        if (!HasStateAuthority)
            return false;

        if (NetPlayerState != PlayerState.Normal)
            return false;

        NetPlayerState = PlayerState.Captured;                            // 포획 상태 진입
        NetCapturePhase = CapturePhase.Transition;                        // 전환 페이즈 시작
        NetHideState = HideState.None;                                    // 은신 해제
        NetCurrentHideSpotId = default;                                   // 은신처 참조 제거
        NetCaptureAnchorPosition = captureAnchorPosition;                 // 포획 이동 위치 저장
        NetCaptureAnchorRotation = captureAnchorRotation;                 // 포획 이동 회전 저장
        NetCaptureTransitionTimer = TickTimer.CreateFromSeconds(Runner, captureTransitionSeconds); // 전환 타이머 시작
        NetCaptureExpireTimer = TickTimer.None;                           // 사망 타이머 초기화

        SetInputLock(true, false);                                        // 이동 잠금, 시야는 허용

        ServerForceDropAllHeldItems();                                    // 들고 있던 아이템 강제 드랍
        ApplyImmediateTraumaOnCapture();                                  // 즉시 후유증 증가

        GameEventLogger.Instance?.LogCaptured(GetNickname(), GetSlotIndex()); // 포획 로그 기록

        if (NetAftereffectPercent >= traumaDeathThreshold)
        {
            ServerEnterDead();                                            // 임계치 초과면 즉시 사망
            return true;
        }

        GameSessionManager.Instance?.CheckZoneAllCaptured(NetZone);       // 같은 구역 전원 포획 여부 확인

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
                ServerBeginCapturedActive();                              // 전환 시간 끝나면 Active 페이즈 시작
            return;
        }

        if (NetCapturePhase != CapturePhase.Active)
            return;

        NetAftereffectPercent += Runner.DeltaTime * traumaIncreasePerSecond; // 후유증 누적 증가

        if (NetAftereffectPercent >= traumaDeathThreshold)
        {
            ServerEnterDead();                                            // 후유증 누적으로 사망
            return;
        }

        if (NetCaptureExpireTimer.Expired(Runner))
            ServerEnterDead();                                            // 제한 시간 만료 시 사망
    }

    public void ServerBeginCapturedActive()
    {
        if (!HasStateAuthority)
            return;

        if (NetPlayerState != PlayerState.Captured)
            return;

        NetCapturePhase = CapturePhase.Active;                            // 포획 활성 페이즈 전환
        NetCaptureTransitionTimer = TickTimer.None;                       // 전환 타이머 종료

        MovePlayerToWorldPose(NetCaptureAnchorPosition, NetCaptureAnchorRotation); // 구조 구역으로 이동

        float remainSeconds = Mathf.Max(0f, rescueBaseTimeSeconds - NetAftereffectPercent); // 남은 구조 가능 시간 계산
        NetCaptureExpireTimer = TickTimer.CreateFromSeconds(Runner, remainSeconds); // 사망 타이머 시작
    }

    public bool ServerExitCapturedToNormal()
    {
        if (!HasStateAuthority)
            return false;

        if (NetPlayerState != PlayerState.Captured)
            return false;

        NetPlayerState = PlayerState.Normal;                              // Normal 상태 복귀
        NetCapturePhase = CapturePhase.None;                              // 포획 페이즈 종료
        NetCaptureTransitionTimer = TickTimer.None;                       // 전환 타이머 종료
        NetCaptureExpireTimer = TickTimer.None;                           // 사망 타이머 종료
        SetInputLock(false, false);                                       // 입력 잠금 해제

        GameEventLogger.Instance?.LogRescued(GetNickname(), GetSlotIndex()); // 구출 로그 기록

        return true;
    }

    /// <summary>
    /// 현재 플레이어가 다른 플레이어에게 구출될 수 있는지 검사
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
    /// </summary>
    private bool CanBeStolenFromBy(PlayerController actor)
    {
        if (actor == null || actor == this)
            return false;

        if (!actor.CanUseGameplayInput())
            return false;

        if (!CanUseGameplayInput())
            return false;

        return NetRightHandItem != null;                                  // 오른손에 아이템이 있어야 탈취 가능
    }

    /// <summary>
    /// 서버에서 실제 구출을 실행
    /// </summary>
    public bool ServerTryRescueBy(PlayerController actor)
    {
        if (!HasStateAuthority)
            return false;

        if (!CanBeRescuedBy(actor))
            return false;

        return ServerExitCapturedToNormal();                              // 구출 가능하면 Normal 복귀
    }

    public void ServerEnterDead()
    {
        if (!HasStateAuthority)
            return;

        NetPlayerState = PlayerState.Dead;                                // 사망 상태 진입
        SaveFinalPlayerState(PlayerState.Dead);                           // 최종 상태 저장

        GameEventLogger.Instance?.LogDead(GetNickname(), GetSlotIndex()); // 사망 로그 기록

        NetHideState = HideState.None;                                    // 은신 해제
        NetCapturePhase = CapturePhase.None;                              // 포획 페이즈 종료
        NetCurrentHideSpotId = default;                                   // 은신처 참조 제거
        NetCaptureTransitionTimer = TickTimer.None;                       // 전환 타이머 종료
        NetCaptureExpireTimer = TickTimer.None;                           // 사망 타이머 종료
        NetMovementLocked = true;                                         // 이동 잠금
        NetLookLocked = true;                                             // 시야 잠금
    }

    public void ServerEnterEscaped()
    {
        if (!HasStateAuthority)
            return;

        NetPlayerState = PlayerState.Escaped;                             // 탈출 상태 진입
        SaveFinalPlayerState(PlayerState.Escaped);                        // 최종 상태 저장

        GameEventLogger.Instance?.LogEscaped(GetNickname(), GetSlotIndex()); // 탈출 로그 기록
        GameSessionManager.Instance?.CheckZoneEscaped(NetZone);           // 같은 구역 캡처 상태 플레이어 처리

        NetHideState = HideState.None;                                    // 은신 해제
        NetCapturePhase = CapturePhase.None;                              // 포획 페이즈 종료
        NetCurrentHideSpotId = default;                                   // 은신처 참조 제거
        NetCaptureTransitionTimer = TickTimer.None;                       // 전환 타이머 종료
        NetCaptureExpireTimer = TickTimer.None;                           // 사망 타이머 종료
        NetMovementLocked = true;                                         // 이동 잠금
        NetLookLocked = true;                                             // 시야 잠금
    }

    public bool ServerTryPickupLeftHand(ItemObject item)
    {
        if (!HasStateAuthority || item == null)
            return false;

        if (!CanUseGameplayInput())
            return false;

        if (!item.CanInteract(this))
            return false;

        if (NetLeftHandItem != null)
            return false;                                                 // 왼손은 자의 교체 불가

        NetLeftHandItem = item.Object;                                    // 왼손에 아이템 장착
        item.OnEquipped(this);                                            // 아이템 장착 처리
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

        NetHideState = hideState;                                         // 은신 상태 설정
        NetCurrentHideSpotId = hideSpotObject != null ? hideSpotObject.Id : default; // 현재 은신처 참조 저장
        NetMovementLocked = true;                                         // 은신 중 이동 잠금
        NetLookLocked = false;                                            // 시야는 허용

        MovePlayerToWorldPose(enterPosition, enterRotation);              // 은신 위치로 이동
        return true;
    }

    public bool ServerExitHide(Vector3 exitPosition, Quaternion exitRotation)
    {
        if (!HasStateAuthority)
            return false;

        if (NetHideState == HideState.None)
            return false;

        NetHideState = HideState.None;                                    // 은신 상태 해제
        NetCurrentHideSpotId = default;                                   // 은신처 참조 제거

        if (NetPlayerState == PlayerState.Normal)
        {
            NetMovementLocked = false;                                    // Normal이면 이동 잠금 해제
            NetLookLocked = false;                                        // 시야 잠금 해제
        }

        MovePlayerToWorldPose(exitPosition, exitRotation);                // 퇴장 위치로 이동
        return true;
    }

    private bool TryGetCurrentHideSpot(out HideSpotInteractable hideSpot)
    {
        hideSpot = null;                                                  // out 초기화

        if (NetCurrentHideSpotId == default)
            return false;

        if (!Runner.TryFindObject(NetCurrentHideSpotId, out NetworkObject hideSpotObject))
            return false;

        if (hideSpotObject == null)
            return false;

        hideSpot = hideSpotObject.GetComponent<HideSpotInteractable>();   // 은신처 컴포넌트 추출
        return hideSpot != null;
    }

    private bool EnsureRightHandEmpty()
    {
        if (NetRightHandItem == null)
            return true;                                                  // 이미 비어 있으면 성공

        return ServerDropRightHandItem();                                 // 아니면 먼저 드랍
    }

    private bool TryGetItemObject(NetworkObject networkObject, out ItemObject item)
    {
        item = null;                                                      // out 초기화

        if (networkObject == null)
            return false;

        item = networkObject.GetComponent<ItemObject>();                  // ItemObject 추출
        return item != null;
    }

    private Vector3 GetRightHandDropPosition()
    {
        return transform.position +
               transform.forward * rightHandDropForwardOffset +
               Vector3.up * rightHandDropUpOffset;                        // 오른손 드랍 위치 계산
    }

    private Vector3 GetLeftHandDropPosition()
    {
        return transform.position +
               transform.forward * leftHandDropForwardOffset +
               transform.right * leftHandDropSideOffset +
               Vector3.up * leftHandDropUpOffset;                         // 왼손 드랍 위치 계산
    }

    private void ApplyImmediateTraumaOnCapture()
    {
        NetCaptureCount += 1;                                             // 포획 횟수 증가
        NetAftereffectPercent += GetBaseTraumaPenalty(NetCaptureCount);   // 즉시 후유증 적용
    }

    private float GetBaseTraumaPenalty(int captureCount)
    {
        if (captureCount <= 1)
            return traumaPenaltyCapture1;                                 // 1회차 포획 후유증

        if (captureCount == 2)
            return traumaPenaltyCapture2;                                 // 2회차 포획 후유증

        return traumaPenaltyCapture3Plus;                                 // 3회 이상 포획 후유증
    }

    private string GetNickname()
    {
        var data = Runner.GetPlayerObject(Object.InputAuthority)?.GetComponent<PlayerData>(); // PlayerData 탐색
        return data != null ? data.Nickname.ToString() : "Unknown";       // 닉네임 반환
    }

    private int GetSlotIndex()
    {
        var data = Runner.GetPlayerObject(Object.InputAuthority)?.GetComponent<PlayerData>(); // PlayerData 탐색
        return data != null ? data.SlotIndex : -1;                        // 슬롯 번호 반환
    }

    private void SaveFinalPlayerState(PlayerState state)
    {
        var data = Runner.GetPlayerObject(Object.InputAuthority)?.GetComponent<PlayerData>(); // PlayerData 탐색
        if (data != null)
            data.FinalPlayerState = state;                                // 최종 상태 저장
    }

    private Vector3 GetServerInteractionOrigin()
    {
        if (LookView != null && LookView.ViewOrigin != null)
            return LookView.ViewOrigin.position;                          // 카메라 기준 상호작용 시작점 반환

        float fallbackEyeHeight = 1.6f;                                   // 기본 눈높이

        if (KCCMotor != null)
            fallbackEyeHeight = (KCCMotor.IsCrouching ? KCCMotor.CrouchHeight : KCCMotor.StandHeight) - 0.1f; // 앉기/서기 기준 눈높이 보정

        return transform.position + Vector3.up * fallbackEyeHeight;       // fallback 상호작용 시작점 반환
    }

    private Vector3 GetClosestInteractionPoint(NetworkObject targetObject, Vector3 origin)
    {
        if (targetObject == null)
            return origin;

        Collider[] colliders = targetObject.GetComponentsInChildren<Collider>(true); // 대상 콜라이더 전부 수집

        Vector3 bestPoint = targetObject.transform.position;              // 초기 최적점
        float bestSqrDistance = (bestPoint - origin).sqrMagnitude;        // 초기 거리
        bool foundCollider = false;                                       // 유효 콜라이더 탐색 여부

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider col = colliders[i];                                  // 현재 콜라이더 참조
            if (col == null || !col.enabled)
                continue;

            Vector3 point = col.ClosestPoint(origin);                     // 현재 콜라이더 최단점 계산
            float sqrDistance = (point - origin).sqrMagnitude;             // 현재 거리 계산

            if (!foundCollider || sqrDistance < bestSqrDistance)
            {
                foundCollider = true;                                     // 유효 콜라이더 찾음
                bestSqrDistance = sqrDistance;                            // 최적 거리 갱신
                bestPoint = point;                                        // 최적점 갱신
            }
        }

        return bestPoint;
    }

    private bool IsTargetWithinInteractDistance(NetworkObject targetObject, float maxDistance)
    {
        Vector3 origin = GetServerInteractionOrigin();                    // 서버 기준 상호작용 시작점 계산
        Vector3 targetPoint = GetClosestInteractionPoint(targetObject, origin); // 대상 최적 상호작용 지점 계산

        float sqrDistance = (targetPoint - origin).sqrMagnitude;          // 실제 거리 제곱
        float allowedSqrDistance = maxDistance * maxDistance + 0.25f;     // 허용 거리 제곱 + 여유값

        return sqrDistance <= allowedSqrDistance;                         // 허용 거리 내 여부 반환
    }

    private void MovePlayerToWorldPose(Vector3 worldPosition, Quaternion worldRotation)
    {
        if (KCCMotor != null)
        {
            KCCMotor.WarpToPose(worldPosition, worldRotation);            // KCC 기준 워프 처리
            return;
        }

        transform.SetPositionAndRotation(worldPosition, worldRotation);   // 일반 Transform 워프 처리

        Rigidbody body = GetComponent<Rigidbody>();                       // Rigidbody 탐색
        if (body != null)
        {
            body.position = worldPosition;                                // 물리 위치 동기화
            body.rotation = worldRotation;                                // 물리 회전 동기화
            body.linearVelocity = Vector3.zero;                           // 속도 제거
            body.angularVelocity = Vector3.zero;                          // 각속도 제거
        }
    }

    /// <summary>
    /// 플레이 모드에서 인스펙터 컨텍스트 메뉴로 강제 포획 테스트를 실행한다.
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

        Vector3 targetPosition = debugCaptureAnchor != null ? debugCaptureAnchor.position : transform.position; // 포획 위치 계산
        Quaternion targetRotation = debugCaptureAnchor != null ? debugCaptureAnchor.rotation : transform.rotation; // 포획 회전 계산

        ServerEnterCaptured(targetPosition, targetRotation);              // 강제 포획 실행
    }

    /// <summary>
    /// 플레이 모드에서 인스펙터 컨텍스트 메뉴로 강제 구출 테스트를 실행한다.
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

        ServerExitCapturedToNormal();                                     // 강제 구출 실행
    }

    /// <summary>
    /// 지속성 상호작용(Hold)을 서버에 요청한다.
    /// 
    /// 규칙:
    /// - 대상이 IHoldInteractable일 때만 실제 OnHoldInteract를 호출한다.
    /// - 일반 퍼즐 버튼 / 아이템 / 판별기 등은 무시된다.
    /// </summary>
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_RequestHoldInteract(NetworkId targetId, int interactableId, float holdDeltaTime)
    {
        if (!HasStateAuthority || !CanUseGameplayInput())
            return;

        if (!Runner.TryFindObject(targetId, out NetworkObject targetObject) || targetObject == null)
            return;

        float maxDistance = Interaction != null ? Interaction.InteractDistance : 2f; // 최대 상호작용 거리 계산
        if (!IsTargetWithinInteractDistance(targetObject, maxDistance))
            return;

        IInteractable interactable = null;                                // 최종 상호작용 대상 참조

        if (interactableId >= 0)
            interactable = FindChildInteractable(targetObject.transform, interactableId); // 자식 상호작용 우선 탐색

        if (interactable == null)
        {
            if (PlayerInteraction.TryFindInteractable(targetObject.transform, out _, out IInteractable rootInteractable, out _))
                interactable = rootInteractable;                          // 루트 상호작용 fallback
        }

        if (interactable == null || !interactable.CanInteract(this))
            return;

        if (interactable is IHoldInteractable holdInteractable)
            holdInteractable.OnHoldInteract(this, holdDeltaTime);         // Hold 대상이면 수리 등 지속 상호작용 실행
    }

    // 🛠️ [수신 단자 개조] 클라이언트에서 모아둔 성공 횟수(successCount)를 받음
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_ProcessMinigameSuccess(int successCount)
    {
        if (NetPlayerState != PlayerState.Captured || successCount <= 0)
            return;

        // 🛠️ 1% * 성공 횟수만큼 방출량을 계산
        float reductionValue = 1.0f * successCount;

        NetAftereffectPercent = Mathf.Max(0f, NetAftereffectPercent - reductionValue);

        if (NetCaptureExpireTimer.IsRunning)
        {
            float currentRemaining = NetCaptureExpireTimer.RemainingTime(Runner).GetValueOrDefault(0);
            // 🛠️ 삭감된 만큼 생존 시간도 비례해서 연장!
            NetCaptureExpireTimer = TickTimer.CreateFromSeconds(Runner, currentRemaining + reductionValue);
        }

        Debug.Log($"<color=cyan>[미니게임 결산]</color> 1사이클 완료! {successCount}회 성공하여 후유증 {reductionValue}% 삭감. 현재: {NetAftereffectPercent}%");
    }

    #region 게임 종료 이벤트 확인용 RPC

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void Rpc_DebugSetState(PlayerState state)
    {
        if (state == PlayerState.Dead)
            ServerEnterDead();                                            // 강제 사망
        else if (state == PlayerState.Escaped)
            ServerEnterEscaped();                                         // 강제 탈출
        else
            NetPlayerState = state;                                       // 그 외 상태는 직접 설정
    }

    #endregion
}