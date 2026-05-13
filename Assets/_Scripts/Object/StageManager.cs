using Fusion;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 게임 전체 Stage 진행 승인 및 전역 맵 변화 관리 매니저.
/// 
/// 역할
/// - PuzzleProgressManager의 Stage1 완료 보고를 받는다.
/// - 해당 Zone의 Stage2 해금을 승인한다.
/// - 해당 Zone의 Stage3 진입 문을 연다.
/// - Zone별 Stage3 완료 보고를 받아 3막(Act3) 진입 여부를 판단한다.
/// - 3막(Act3) 발동, 셔터/조명/사이렌 같은 전역 맵 변화를 담당한다.
/// - Zone별 Stage2 진행도를 읽어서 키카드 보상 판정을 제공한다.
/// - 맵에 고정 배치된 구제구역 퍼즐에 라운드 seed 기반 정답 seed를 주입한다.
/// </summary>
public class StageManager : NetworkBehaviour
{
    public static StageManager Instance { get; private set; } // 전역 접근용 싱글톤

    [Header("퍼즐 관리 매니저 참조")]
    [SerializeField] private PuzzleProgressManager puzzleProgressManager; // 퍼즐 진행도 집계 매니저 참조

    [Header("Stage3 진입 문")]
    [SerializeField] private GameObject[] zoneAStage3Door; // ZoneA 3단계 진입 문
    [SerializeField] private GameObject[] zoneBStage3Door; // ZoneB 3단계 진입 문

    [Header("3막 연출 소리 설정")]
    [SerializeField] private AudioSource sirenAudioSource; // 전역 사이렌 AudioSource
    [SerializeField] private AudioClip sirenClip;          // 3막 진입 시 재생할 사이렌 클립
    [SerializeField] private AudioClip shutterCloseClip;   // 셔터 닫힘 시 재생할 클립

    [Header("Zone A 계단 차단 (셔터)")]
    [SerializeField] private GameObject zoneA_StairA_Top;    // ZoneA A계단 상단 차단벽 (3F-2F)
    [SerializeField] private GameObject zoneA_StairA_Bottom; // ZoneA A계단 하단 차단벽 (2F-1F)
    [SerializeField] private GameObject zoneA_StairB_Top;    // ZoneA B계단 상단 차단벽 (3F-2F)
    [SerializeField] private GameObject zoneA_StairB_Bottom; // ZoneA B계단 하단 차단벽 (2F-1F)

    [Header("Zone B 계단 차단 (셔터)")]
    [SerializeField] private GameObject zoneB_StairA_Top;    // ZoneB A계단 상단 차단벽 (3F-2F)
    [SerializeField] private GameObject zoneB_StairA_Bottom; // ZoneB A계단 하단 차단벽 (2F-1F)
    [SerializeField] private GameObject zoneB_StairB_Top;    // ZoneB B계단 상단 차단벽 (3F-2F)
    [SerializeField] private GameObject zoneB_StairB_Bottom; // ZoneB B계단 하단 차단벽 (2F-1F)

    [Header("구제구역 퍼즐")]
    [SerializeField] private RescueZonePuzzle zoneARescueZonePuzzle; // ZoneA 고정 구제구역 퍼즐
    [SerializeField] private RescueZonePuzzle zoneBRescueZonePuzzle; // ZoneB 고정 구제구역 퍼즐

    [Header("관전 대기실 (Dead Room)")]
    [SerializeField] private Transform deadRespawnPoint;

    [Header("디버그")]
    [SerializeField] private bool enableDebugLog = true; // 디버그 로그 출력 여부

    [Networked, OnChangedRender(nameof(OnAct3StateChanged))]
    public NetworkBool IsAct3Active { get; set; } // 3막 진행 여부 네트워크 동기화 값

    [Networked] private NetworkBool NetZoneAPattern1 { get; set; } // ZoneA 셔터 패턴
    [Networked] private NetworkBool NetZoneBPattern1 { get; set; } // ZoneB 셔터 패턴

    [Networked, OnChangedRender(nameof(OnStage1CompletedChangedRender))]
    private NetworkBool NetZoneAStage1Completed { get; set; } // ZoneA Stage1 완료 승인 여부

    [Networked, OnChangedRender(nameof(OnStage1CompletedChangedRender))]
    private NetworkBool NetZoneBStage1Completed { get; set; } // ZoneB Stage1 완료 승인 여부

    [Networked] private NetworkBool NetZoneAStage3Completed { get; set; } // ZoneA Stage3 완료 여부
    [Networked] private NetworkBool NetZoneBStage3Completed { get; set; } // ZoneB Stage3 완료 여부

    [Networked] public NetworkBool IsEscapeButtonExposed { get; private set; } // 기존 호환용 전역 탈출 버튼 노출 값
    [Networked] public NetworkBool IsZoneAEscapeButtonExposed { get; private set; } // ZoneA 탈출 버튼 노출 여부
    [Networked] public NetworkBool IsZoneBEscapeButtonExposed { get; private set; } // ZoneB 탈출 버튼 노출 여부
    [Networked] public NetworkBool IsZoneAEscapePressed { get; set; } // ZoneA 탈출 버튼 입력 여부
    [Networked] public NetworkBool IsZoneBEscapePressed { get; set; } // ZoneB 탈출 버튼 입력 여부
    [Networked] private TickTimer EscapeInputTimer { get; set; } // 동시 입력 제한 타이머

    [Networked] private NetworkBool NetRescueZonePuzzlesSeedApplied { get; set; } // 구제구역 퍼즐 seed 주입 완료 여부

    private readonly HashSet<NetworkId> _teleportedPlayers = new(); // 이미 DeadRoom으로 보낸 플레이어 ID
    private readonly Dictionary<NetworkId, TickTimer> _deadTeleportTimers = new(); // 사망/탈출 후 지연 텔레포트 대기 목록
    private float _findRespawnTimer; // Dead_Respawn 재탐색 타이머

    private void Update()
    {
        if (!HasStateAuthority)
            return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        if (keyboard.f11Key.wasPressedThisFrame)
            DebugCheatSkipAllToEscapeButton();
    }

    public override void Spawned()
    {
        if (Instance == null)
            Instance = this;

        if (HasStateAuthority)
        {
            IsAct3Active = false;

            NetZoneAStage1Completed = false;
            NetZoneBStage1Completed = false;
            NetZoneAStage3Completed = false;
            NetZoneBStage3Completed = false;

            IsEscapeButtonExposed = false;
            IsZoneAEscapeButtonExposed = false;
            IsZoneBEscapeButtonExposed = false;
            IsZoneAEscapePressed = false;
            IsZoneBEscapePressed = false;
            EscapeInputTimer = TickTimer.None;

            NetZoneAPattern1 = false;
            NetZoneBPattern1 = false;

            NetRescueZonePuzzlesSeedApplied = false;

            SetAllStairBlocksActive(false);
            SetStage3DoorOpen(Zone.ZoneA, false);
            SetStage3DoorOpen(Zone.ZoneB, false);

            ServerTryInitializeRescueZonePuzzlesFromRoundSeed();
        }

        ApplyStage1CompletedStateToLocalObjects();
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority)
            return;

        ServerTryInitializeRescueZonePuzzlesFromRoundSeed();

        if (EscapeInputTimer.IsRunning)
        {
            if (IsZoneAEscapePressed && IsZoneBEscapePressed)
            {
                EscapeInputTimer = TickTimer.None;
                TriggerAct3();
            }
            else if (EscapeInputTimer.Expired(Runner))
            {
                IsZoneAEscapePressed = false;
                IsZoneBEscapePressed = false;
                EscapeInputTimer = TickTimer.None;
                Log("탈출 버튼 동시 입력 시간 초과, 입력을 초기화합니다.");
            }
        }

        ProcessPendingTeleports();
    }

    #region 구제구역 퍼즐

    /// <summary>
    /// RoundSeedManager에서 현재 라운드 seed를 확보한 뒤,
    /// ZoneA / ZoneB 구제구역 퍼즐에 seed를 주입한다.
    /// RoundSeedManager의 Spawn 순서가 StageManager보다 늦을 수 있으므로
    /// Spawned와 FixedUpdateNetwork에서 안전하게 재시도한다.
    /// </summary>
    private void ServerTryInitializeRescueZonePuzzlesFromRoundSeed()
    {
        if (!HasStateAuthority)
            return;

        if (NetRescueZonePuzzlesSeedApplied)
            return;

        RoundSeedManager seedManager = RoundSeedManager.Instance;
        if (seedManager == null)
        {
            LogWarning("RoundSeedManager.Instance가 아직 없습니다. 구제구역 seed 초기화를 대기합니다.");
            return;
        }

        seedManager.EnsureRoundSeed();

        if (!seedManager.HasValidSeed)
        {
            LogWarning("RoundSeedManager에 아직 유효한 seed가 없습니다. 구제구역 seed 초기화를 대기합니다.");
            return;
        }

        int roundSeed = seedManager.CurrentSeed;
        ServerInitializeRescueZonePuzzles(roundSeed);
    }

    /// <summary>
    /// 라운드 seed 기반으로 ZoneA / ZoneB 구제구역 퍼즐에 base seed를 주입한다.
    /// 구제구역 퍼즐은 맵 고정 배치 오브젝트이므로 PuzzleSpawnManager가 아니라 StageManager가 직접 관리한다.
    /// </summary>
    private void ServerInitializeRescueZonePuzzles(int roundSeed)
    {
        if (!HasStateAuthority)
            return;

        if (NetRescueZonePuzzlesSeedApplied)
            return;

        int zoneASeed = BuildRescueZoneBaseSeed(roundSeed, Zone.ZoneA);
        int zoneBSeed = BuildRescueZoneBaseSeed(roundSeed, Zone.ZoneB);

        if (zoneARescueZonePuzzle != null)
            zoneARescueZonePuzzle.ServerApplyBaseAnswerSeed(zoneASeed);
        else
            LogWarning("ZoneA RescueZonePuzzle 참조가 비어 있습니다.");

        if (zoneBRescueZonePuzzle != null)
            zoneBRescueZonePuzzle.ServerApplyBaseAnswerSeed(zoneBSeed);
        else
            LogWarning("ZoneB RescueZonePuzzle 참조가 비어 있습니다.");

        NetRescueZonePuzzlesSeedApplied = true;

        Log($"구제구역 퍼즐 seed 적용 완료 | RoundSeed={roundSeed} | ZoneASeed={zoneASeed} | ZoneBSeed={zoneBSeed}");
    }

    /// <summary>
    /// 라운드 seed와 Zone을 섞어서 구제구역 퍼즐 전용 base seed를 만든다.
    /// ZoneA / ZoneB가 같은 라운드 안에서도 서로 다른 정답을 갖도록 Zone 값을 포함한다.
    /// </summary>
    private int BuildRescueZoneBaseSeed(int roundSeed, Zone zone)
    {
        unchecked
        {
            int seed = roundSeed;
            seed = seed * 31 + 91027; // 구제구역 퍼즐 전용 salt
            seed = seed * 31 + (int)zone;

            if (seed == 0)
                seed = 1;

            return seed;
        }
    }

    #endregion

    #region 죽은 플레이어 강제 전송

    /// <summary>
    /// PlayerController에서 사망/탈출 이벤트 발생 시 DeadRoom으로 이동시키기 위해 호출한다.
    /// </summary>
    public void RequestTeleportToDeadRoom(PlayerController player)
    {
        if (!HasStateAuthority)
            return;

        if (player == null || !player.Object.IsValid)
            return;

        NetworkId playerId = player.Object.Id;

        if (!_teleportedPlayers.Contains(playerId) && !_deadTeleportTimers.ContainsKey(playerId))
        {
            _deadTeleportTimers[playerId] = TickTimer.CreateFromSeconds(Runner, 1.0f);
            Log($"[{player.gameObject.name}] 사망/탈출 이벤트 수신. 1초 후 DeadRoom으로 이동합니다.");
        }
    }

    private void ProcessPendingTeleports()
    {
        if (_deadTeleportTimers.Count == 0)
            return;

        if (deadRespawnPoint == null)
        {
            _findRespawnTimer += Runner.DeltaTime;

            if (_findRespawnTimer > 1.0f)
            {
                _findRespawnTimer = 0f;

                GameObject respawnObject = GameObject.Find("Dead_Respawn");
                if (respawnObject != null)
                    deadRespawnPoint = respawnObject.transform;
            }

            if (deadRespawnPoint == null)
                return;
        }

        List<NetworkId> readyToTeleport = new();

        foreach (KeyValuePair<NetworkId, TickTimer> pair in _deadTeleportTimers)
        {
            if (pair.Value.Expired(Runner))
                readyToTeleport.Add(pair.Key);
        }

        foreach (NetworkId playerId in readyToTeleport)
        {
            _deadTeleportTimers.Remove(playerId);
            _teleportedPlayers.Add(playerId);

            if (!Runner.TryFindObject(playerId, out NetworkObject playerObj))
                continue;

            PlayerController player = playerObj.GetComponent<PlayerController>();
            if (player == null)
                continue;

            if (player.KCCMotor != null)
                player.KCCMotor.WarpToPose(deadRespawnPoint.position, deadRespawnPoint.rotation);
            else
                player.transform.SetPositionAndRotation(deadRespawnPoint.position, deadRespawnPoint.rotation);

            Log($"[{player.gameObject.name}] DeadRoom으로 강제 이동 완료.");
        }
    }

    #endregion

    #region Stage1 완료 -> Stage2 해금 / Stage3 문 개방

    /// <summary>
    /// 특정 Zone의 Stage1 퍼즐이 전부 해결되었음을 PuzzleProgressManager가 보고할 때 호출한다.
    /// </summary>
    public void ReportZoneStage1Completed(Zone zone)
    {
        if (!HasStateAuthority)
            return;

        if (zone == Zone.ZoneA)
        {
            if (NetZoneAStage1Completed)
                return;

            NetZoneAStage1Completed = true;
            ApplyStage1CompletedStateToLocalObjects();

            Log("ZoneA Stage1 완료 승인 | ZoneA Stage2 화면 ON | ZoneA Stage3 문 OPEN");
            return;
        }

        if (NetZoneBStage1Completed)
            return;

        NetZoneBStage1Completed = true;
        ApplyStage1CompletedStateToLocalObjects();

        Log("ZoneB Stage1 완료 승인 | ZoneB Stage2 화면 ON | ZoneB Stage3 문 OPEN");
    }

    /// <summary>
    /// 특정 Zone의 Stage3 진입 문을 열거나 닫는다.
    /// </summary>
    private void SetStage3DoorOpen(Zone zone, bool isOpen)
    {
        GameObject[] targetDoor = zone == Zone.ZoneA ? zoneAStage3Door : zoneBStage3Door;
        if (targetDoor == null)
            return;

        foreach (GameObject door in targetDoor)
        {
            if (door != null)
                door.SetActive(!isOpen);
        }
    }

    /// <summary>
    /// Stage1 완료 Networked 값이 바뀌었을 때 모든 클라이언트에서 호출된다.
    /// </summary>
    private void OnStage1CompletedChangedRender()
    {
        ApplyStage1CompletedStateToLocalObjects();
    }

    /// <summary>
    /// Networked Stage1 완료 상태를 현재 클라이언트의 로컬 오브젝트에 반영한다.
    /// </summary>
    private void ApplyStage1CompletedStateToLocalObjects()
    {
        RefreshAllStage2ScreenGates();

        SetStage3DoorOpen(Zone.ZoneA, NetZoneAStage1Completed);
        SetStage3DoorOpen(Zone.ZoneB, NetZoneBStage1Completed);
    }

    /// <summary>
    /// 씬에 존재하는 모든 Stage2ScreenGate에게 현재 StageManager 상태를 다시 반영하게 한다.
    /// </summary>
    private void RefreshAllStage2ScreenGates()
    {
        Stage2ScreenGate[] gates = FindObjectsByType<Stage2ScreenGate>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < gates.Length; i++)
        {
            if (gates[i] == null)
                continue;

            gates[i].RefreshScreenState();
        }
    }

    #endregion

    #region Stage2 진행도 / 키카드 보상 판정

    /// <summary>
    /// 특정 Zone의 Stage1 완료 승인 여부를 반환한다.
    /// </summary>
    public bool IsZoneStage1Completed(Zone zone)
    {
        return zone == Zone.ZoneA
            ? NetZoneAStage1Completed
            : NetZoneBStage1Completed;
    }

    /// <summary>
    /// 특정 Zone의 Stage2 solved 개수를 반환한다.
    /// </summary>
    public int GetZoneSolvedStage2Count(Zone zone)
    {
        if (puzzleProgressManager == null)
            return 0;

        return puzzleProgressManager.GetSolvedStage2Count(zone);
    }

    /// <summary>
    /// 특정 Zone의 Stage2 전체 개수를 반환한다.
    /// </summary>
    public int GetZoneTotalStage2Count(Zone zone)
    {
        if (puzzleProgressManager == null)
            return 0;

        return puzzleProgressManager.GetTotalStage2Count(zone);
    }

    /// <summary>
    /// 특정 Zone의 Stage2 퍼즐이 전부 해결되었는지 반환한다.
    /// </summary>
    public bool IsZoneStage2FullySolved(Zone zone)
    {
        if (puzzleProgressManager == null)
            return false;

        return puzzleProgressManager.IsZoneStage2FullySolved(zone);
    }

    /// <summary>
    /// 특정 Zone에서 마스터 키카드를 줘야 하는지 반환한다.
    /// </summary>
    public bool ShouldSpawnMasterKeycardForZone(Zone zone)
    {
        return IsZoneStage2FullySolved(zone);
    }

    #endregion

    #region Stage3 완료 보고 / Act3 진입

    /// <summary>
    /// 특정 Zone의 Stage3 최종 퍼즐이 해결되었음을 PuzzleProgressManager가 보고할 때 호출한다.
    /// </summary>
    public void ReportZoneStage3Completed(Zone zone)
    {
        if (!HasStateAuthority)
            return;

        if (zone == Zone.ZoneA)
        {
            if (NetZoneAStage3Completed)
                return;

            NetZoneAStage3Completed = true;
            IsZoneAEscapeButtonExposed = true;

            Log("ZoneA Stage3 완료 보고 수신");
            return;
        }

        if (NetZoneBStage3Completed)
            return;

        NetZoneBStage3Completed = true;
        IsZoneBEscapeButtonExposed = true;

        Log("ZoneB Stage3 완료 보고 수신");
    }

    #endregion

    #region 3막 이벤트 로직

    /// <summary>
    /// 해당 Zone의 탈출 버튼 입력을 처리한다.
    /// </summary>
    public void TryPressEscapeButton(Zone zone)
    {
        if (!HasStateAuthority)
            return;

        if (IsAct3Active)
            return;

        if (zone == Zone.ZoneA)
            IsZoneAEscapePressed = true;

        if (zone == Zone.ZoneB)
            IsZoneBEscapePressed = true;

        if (!EscapeInputTimer.IsRunning)
        {
            EscapeInputTimer = TickTimer.CreateFromSeconds(Runner, 3.0f);
            Log($"{zone} 탈출 버튼 입력. 3.0초 대기 시작");
        }
    }

    /// <summary>
    /// 3막(Act3)을 발동한다.
    /// </summary>
    public void TriggerAct3()
    {
        if (!HasStateAuthority || IsAct3Active)
            return;

        IsAct3Active = true;

        IsEscapeButtonExposed = false;
        IsZoneAEscapeButtonExposed = false;
        IsZoneBEscapeButtonExposed = false;
        IsZoneAEscapePressed = false;
        IsZoneBEscapePressed = false;
        EscapeInputTimer = TickTimer.None;

        CreatureAI[] allCreature = FindObjectsByType<CreatureAI>(FindObjectsSortMode.None);
        foreach (CreatureAI creature in allCreature)
            creature.ApplyAct3Multipliers(true);

        FinalCodePuzzle[] finalCodePuzzles = FindObjectsByType<FinalCodePuzzle>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        foreach (FinalCodePuzzle puzzle in finalCodePuzzles)
            puzzle.TrySpawnRewardKeycard();

        NetZoneAPattern1 = Random.value > 0.5f;
        NetZoneBPattern1 = Random.value > 0.5f;

        ZoneLightingManager.GetManager(Zone.ZoneA)?.TriggerAct3Event(true);
        ZoneLightingManager.GetManager(Zone.ZoneB)?.TriggerAct3Event(true);

        RPC_PlayAct3Effects();

        GameSessionManager.Instance.notifyEscapeUnlocked();

        Log("3막(Act3) 진입 완료 | 크리처 강화 | 계단 차단 | 조명/사이렌 발동");
    }

    /// <summary>
    /// IsAct3Active가 true로 변할 때 모든 클라이언트에서 호출된다.
    /// </summary>
    private void OnAct3StateChanged()
    {
        if (!IsAct3Active)
            return;

        ApplySyncedPatternToZone(Zone.ZoneA, NetZoneAPattern1);
        ApplySyncedPatternToZone(Zone.ZoneB, NetZoneBPattern1);
    }

    /// <summary>
    /// 특정 Zone에 동기화된 계단 차단 패턴을 적용한다.
    /// </summary>
    private void ApplySyncedPatternToZone(Zone zone, bool isPattern1)
    {
        if (zone == Zone.ZoneA)
        {
            if (zoneA_StairA_Top != null) zoneA_StairA_Top.SetActive(isPattern1);
            if (zoneA_StairB_Bottom != null) zoneA_StairB_Bottom.SetActive(isPattern1);

            if (zoneA_StairA_Bottom != null) zoneA_StairA_Bottom.SetActive(!isPattern1);
            if (zoneA_StairB_Top != null) zoneA_StairB_Top.SetActive(!isPattern1);
        }
        else
        {
            if (zoneB_StairA_Top != null) zoneB_StairA_Top.SetActive(isPattern1);
            if (zoneB_StairB_Bottom != null) zoneB_StairB_Bottom.SetActive(isPattern1);

            if (zoneB_StairA_Bottom != null) zoneB_StairA_Bottom.SetActive(!isPattern1);
            if (zoneB_StairB_Top != null) zoneB_StairB_Top.SetActive(!isPattern1);
        }

        Log($"[{zone}] 3막 계단 차단 완료");
    }

    /// <summary>
    /// 모든 계단 차단벽을 일괄 활성/비활성 처리한다.
    /// </summary>
    private void SetAllStairBlocksActive(bool active)
    {
        if (zoneA_StairA_Top != null) zoneA_StairA_Top.SetActive(active);
        if (zoneA_StairA_Bottom != null) zoneA_StairA_Bottom.SetActive(active);
        if (zoneA_StairB_Top != null) zoneA_StairB_Top.SetActive(active);
        if (zoneA_StairB_Bottom != null) zoneA_StairB_Bottom.SetActive(active);

        if (zoneB_StairA_Top != null) zoneB_StairA_Top.SetActive(active);
        if (zoneB_StairA_Bottom != null) zoneB_StairA_Bottom.SetActive(active);
        if (zoneB_StairB_Top != null) zoneB_StairB_Top.SetActive(active);
        if (zoneB_StairB_Bottom != null) zoneB_StairB_Bottom.SetActive(active);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayAct3Effects()
    {
        if (sirenAudioSource == null)
            return;

        if (shutterCloseClip != null)
            sirenAudioSource.PlayOneShot(shutterCloseClip);

        if (sirenClip != null)
        {
            sirenAudioSource.clip = sirenClip;
            sirenAudioSource.loop = true;
            sirenAudioSource.Play();
        }
    }

    #endregion

    #region 디버그 및 테스트

    [ContextMenu("Debug/1단계 완료 강제 승인 (2단계 해금 + 3단계 문 개방)")]
    private void DebugForceUnlockStage2AndOpenStage3Doors()
    {
        if (!Application.isPlaying)
        {
            LogWarning("플레이 모드에서만 실행 가능합니다.");
            return;
        }

        if (!HasStateAuthority)
        {
            LogWarning("상태 권한이 있는 서버(호스트)에서만 실행 가능합니다.");
            return;
        }

        ReportZoneStage1Completed(Zone.ZoneA);
        ReportZoneStage1Completed(Zone.ZoneB);

        Log("디버그 | 양쪽 Zone Stage2 해금 + Stage3 문 개방 강제 적용");
    }

    [ContextMenu("Debug/치트: 1&2&3 단계 즉시 패스 (F11)")]
    private void DebugCheatSkipAllToEscapeButton()
    {
        if (!HasStateAuthority)
            return;

        ReportZoneStage1Completed(Zone.ZoneA);
        ReportZoneStage1Completed(Zone.ZoneB);

        ReportZoneStage3Completed(Zone.ZoneA);
        ReportZoneStage3Completed(Zone.ZoneB);

        PuzzleInteractableBase[] allPuzzles = FindObjectsByType<PuzzleInteractableBase>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (PuzzleInteractableBase puzzle in allPuzzles)
        {
            if (puzzle == null)
                continue;

            if (puzzle is FinalCodePuzzle finalCodePuzzle)
                finalCodePuzzle.HandleSolved();
            else
                puzzle.DebugForceSolve();
        }

        Log("<color=magenta><b>[CHEAT] F11 입력!</b></color> 3단계까지 모두 패스했습니다. 탈출 버튼에 불이 들어옵니다.");
    }

    [ContextMenu("Debug/3단계 완료 강제 승인 (탈출 버튼 노출)")]
    private void DebugForceExposeEscapeButton()
    {
        if (!Application.isPlaying)
        {
            LogWarning("플레이 모드에서만 실행 가능합니다.");
            return;
        }

        if (!HasStateAuthority)
        {
            LogWarning("상태 권한이 있는 서버(호스트)에서만 실행 가능합니다.");
            return;
        }

        ReportZoneStage3Completed(Zone.ZoneA);
        ReportZoneStage3Completed(Zone.ZoneB);

        Log("디버그 | 양쪽 Zone Stage3 완료 강제 승인 및 탈출 버튼 노출");
    }

    [ContextMenu("Debug/3막(Act 3) 강제 진입")]
    private void DebugForceTriggerAct3()
    {
        if (!Application.isPlaying)
        {
            LogWarning("플레이 모드에서만 실행 가능합니다.");
            return;
        }

        if (!HasStateAuthority)
        {
            LogWarning("상태 권한이 있는 서버(호스트)에서만 실행 가능합니다.");
            return;
        }

        TriggerAct3();

        Log("디버그 | 3막 강제 발동");
    }

    #endregion

    /// <summary>
    /// 일반 디버그 로그 출력.
    /// </summary>
    private void Log(string message)
    {
        if (!enableDebugLog)
            return;

        Debug.Log($"[StageManager] {message}", this);
    }

    /// <summary>
    /// 경고 디버그 로그 출력.
    /// </summary>
    private void LogWarning(string message)
    {
        if (!enableDebugLog)
            return;

        Debug.LogWarning($"[StageManager] {message}", this);
    }
}