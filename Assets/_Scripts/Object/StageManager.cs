using Fusion;
using System.Collections.Generic;
using UnityEngine;

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
/// </summary>
public class StageManager : NetworkBehaviour
{
    public static StageManager Instance { get; private set; } // 전역 접근용 싱글톤

    [Header("퍼즐 관리 매니저 참조")]
    [SerializeField] private PuzzleProgressManager puzzleProgressManager; // 퍼즐 진행도 집계 매니저 참조

    [Header("Stage3 진입 문")]
    [SerializeField] private GameObject zoneAStage3Door; // ZoneA 3단계 진입 문
    [SerializeField] private GameObject zoneBStage3Door; // ZoneB 3단계 진입 문

    [Header("3막 연출 소리 설정")]
    [SerializeField] private AudioSource sirenAudioSource; // 전역 사이렌 AudioSource
    [SerializeField] private AudioClip sirenClip;          // 3막 진입 시 재생할 사이렌 클립

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

    [Header("관전 대기실 (Dead Room)")]    
    [SerializeField] private Transform deadRespawnPoint;

    [Header("디버그")]
    [SerializeField] private bool enableDebugLog = true; // 디버그 로그 출력 여부

    [Networked] public NetworkBool IsAct3Active { get; set; } // 3막 진행 여부 네트워크 동기화 값
    [Networked] private NetworkBool NetZoneAStage1Completed { get; set; } // ZoneA Stage1 완료 승인 여부
    [Networked] private NetworkBool NetZoneBStage1Completed { get; set; } // ZoneB Stage1 완료 승인 여부

    //Zone별 Stage3 완료 상태
    [Networked] private NetworkBool NetZoneAStage3Completed { get; set; } // ZoneA Stage3 완료 여부
    [Networked] private NetworkBool NetZoneBStage3Completed { get; set; } // ZoneB Stage3 완료 여부

    //탈출 버튼 동시 입력 관련 네트워크 변수
    [Networked] public NetworkBool IsEscapeButtonExposed { get; private set; }
    [Networked] public NetworkBool IsZoneAEscapeButtonExposed { get; private set; } // ZoneA 탈출 버튼 노출 여부
    [Networked] public NetworkBool IsZoneBEscapeButtonExposed { get; private set; } // ZoneB 탈출 버튼 노출 여부
    [Networked] private NetworkBool IsZoneAEscapePressed { get; set; }
    [Networked] private NetworkBool IsZoneBEscapePressed { get; set; }
    [Networked] private TickTimer EscapeInputTimer { get; set; }

    //텔레포트가 이미 완료된 플레이어들을 기억하여 무한 워프를 방지하는 로컬 셋
    private HashSet<NetworkId> _teleportedPlayers = new HashSet<NetworkId>();

    //옵저버 시스템 고장 방지를 위한 3초 지연 타이머 딕셔너리
    private Dictionary<NetworkId, TickTimer> _deadTeleportTimers = new Dictionary<NetworkId, TickTimer>();
    private float _findRespawnTimer = 0f;

    public override void Spawned()
    {
        if (Instance == null)
            Instance = this; // 싱글톤 설정

        if (HasStateAuthority)
        {
            IsAct3Active = false; // 게임 시작 시 3막 비활성화
            NetZoneAStage1Completed = false; // ZoneA Stage1 완료 플래그 초기화
            NetZoneBStage1Completed = false; // ZoneB Stage1 완료 플래그 초기화
            NetZoneAStage3Completed = false; // ZoneA Stage3 완료 플래그 초기화
            NetZoneBStage3Completed = false; // ZoneB Stage3 완료 플래그 초기화

            IsZoneAEscapeButtonExposed = false; // ZoneA 버튼 비노출
            IsZoneBEscapeButtonExposed = false; // ZoneB 버튼 비노출
            IsZoneAEscapePressed = false;       // ZoneA 입력 상태 초기화
            IsZoneBEscapePressed = false;       // ZoneB 입력 상태 초기화
            EscapeInputTimer = TickTimer.None;  // 동시 입력 타이머 초기화

            SetAllStairBlocksActive(false); // 시작 시 계단 차단벽 전부 비활성화
            SetStage3DoorOpen(Zone.ZoneA, false); // ZoneA 3단계 진입 문 닫기
            SetStage3DoorOpen(Zone.ZoneB, false); // ZoneB 3단계 진입 문 닫기
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;

        //동시 입력 타이머 처리
        if (EscapeInputTimer.IsRunning)
        {
            //양쪽 모두 입력 완료 시 탈출(3막) 발동
            if (IsZoneAEscapePressed && IsZoneBEscapePressed)
            {
                EscapeInputTimer = TickTimer.None;
                TriggerAct3();
            }

            //시간 초과 시 입력 초기화
            else if (EscapeInputTimer.Expired(Runner))
            {
                IsZoneAEscapePressed = false;
                IsZoneBEscapePressed = false;
                EscapeInputTimer = TickTimer.None;
                Log("탈출 버튼 동시 입력 시간 초과, 입력을 초기화합니다.");
            }
        }

        //알림을 받은 플레이어들만 모아서 지연 텔레포트 처리
        ProcessPendingTeleports();
    }

    #region 죽은 플레이어 강제 전송
    /// <summary>
    /// PlayerController에서 사망/탈출 이벤트 발생 시 워프 대상 위치로 이동시키는 함수. 
    /// </summary>

    public void RequestTeleportToDeadRoom(PlayerController player)
    {
        if (!HasStateAuthority) return;
        if (player == null || !player.Object.IsValid) return;

        NetworkId playerId = player.Object.Id;

        //아직 워프되지 않았고, 타이머도 돌고 있지 않다면
        if (!_teleportedPlayers.Contains(playerId) && !_deadTeleportTimers.ContainsKey(playerId))
        {
            //옵저버 시스템 전환을 기다려주기 위해 타이머 시작
            _deadTeleportTimers[playerId] = TickTimer.CreateFromSeconds(Runner, 1.0f);
            Log($"[{player.gameObject.name}] 사망/탈출 이벤트 수신! 옵저버 전환을 위해 1초 후 DeadRoom으로 이동합니다.");
        }
    }

    private void ProcessPendingTeleports()
    {
        if (_deadTeleportTimers.Count == 0) return;

        if (deadRespawnPoint == null)
        {
            _findRespawnTimer += Runner.DeltaTime;
            if (_findRespawnTimer > 1.0f)
            {
                _findRespawnTimer = 0f;
                GameObject respawnObject = GameObject.Find("Dead_Respawn");
                if (respawnObject != null) deadRespawnPoint = respawnObject.transform;
            }

            //찾지 못했다면 강제 이동 보류
            if (deadRespawnPoint == null) return;
        }

        //2. 3초 타이머가 만료된 플레이어 선별
        List<NetworkId> readyToTeleport = new List<NetworkId>();
        foreach (var kvp in _deadTeleportTimers)
        {
            if (kvp.Value.Expired(Runner)) readyToTeleport.Add(kvp.Key);
        }

        //실제 텔레포트 실행
        foreach (var playerId in readyToTeleport)
        {
            _deadTeleportTimers.Remove(playerId);
            _teleportedPlayers.Add(playerId);

            if (Runner.TryFindObject(playerId, out NetworkObject playerObj))
            {
                PlayerController player = playerObj.GetComponent<PlayerController>();
                if (player != null)
                {
                    if (player.KCCMotor != null) player.KCCMotor.WarpToPose(deadRespawnPoint.position, deadRespawnPoint.rotation);
                    else player.transform.SetPositionAndRotation(deadRespawnPoint.position, deadRespawnPoint.rotation);

                    Log($"[{player.gameObject.name}] DeadRoom으로 강제 이동 완료.");
                }
            }        
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

        if (puzzleProgressManager == null)
        {
            LogWarning("PuzzleProgressManager 참조가 없어 Stage2 해금을 승인할 수 없습니다.");
            return;
        }

        if (zone == Zone.ZoneA)
        {
            if (NetZoneAStage1Completed)
                return;

            NetZoneAStage1Completed = true; // ZoneA 완료 승인 기록
            puzzleProgressManager.HandleZoneStage2Unlocked(Zone.ZoneA); // ZoneA Stage2 화면 ON 승인
            SetStage3DoorOpen(Zone.ZoneA, true); // ZoneA 3단계 진입 문 개방

            Log("ZoneA Stage1 완료 승인 | ZoneA Stage2 화면 ON | ZoneA Stage3 문 OPEN");
            return;
        }

        if (NetZoneBStage1Completed)
            return;

        NetZoneBStage1Completed = true; // ZoneB 완료 승인 기록
        puzzleProgressManager.HandleZoneStage2Unlocked(Zone.ZoneB); // ZoneB Stage2 화면 ON 승인
        SetStage3DoorOpen(Zone.ZoneB, true); // ZoneB 3단계 진입 문 개방

        Log("ZoneB Stage1 완료 승인 | ZoneB Stage2 화면 ON | ZoneB Stage3 문 OPEN");
    }

    /// <summary>
    /// 특정 Zone의 Stage3 진입 문을 열거나 닫는다.
    /// </summary>
    private void SetStage3DoorOpen(Zone zone, bool isOpen)
    {
        GameObject targetDoor = zone == Zone.ZoneA ? zoneAStage3Door : zoneBStage3Door;
        if (targetDoor == null)
            return;

        targetDoor.SetActive(!isOpen); // 막는 오브젝트 기준: 열림이면 비활성화, 닫힘이면 활성화
    }

    #endregion

    #region Stage2 진행도 / 키카드 보상 판정

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
    /// 
    /// 현재 규칙
    /// - 어느 한 Zone이라도 Stage3 완료 보고가 들어오면 Act3를 발동한다.
    /// - 이미 보고된 Zone이면 중복 처리하지 않는다.
    /// - 이미 Act3 상태면 재발동하지 않는다.
    /// </summary>
    public void ReportZoneStage3Completed(Zone zone)
    {
        if (!HasStateAuthority)
            return;

        if (zone == Zone.ZoneA)
        {
            if (NetZoneAStage3Completed)
                return;

            NetZoneAStage3Completed = true; // ZoneA Stage3 완료 기록
            IsZoneAEscapeButtonExposed = true;  // ZoneA 탈출 버튼 노출
            Log("ZoneA Stage3 완료 보고 수신");
        }
        else
        {
            if (NetZoneBStage3Completed)
                return;

            NetZoneBStage3Completed = true; // ZoneB Stage3 완료 기록
            IsZoneBEscapeButtonExposed = true;  // ZoneB 탈출 버튼 노출
            Log("ZoneB Stage3 완료 보고 수신");
        }
    }

    #endregion

    #region 3막 이벤트 로직

    /// <summary>
    /// 3막(Act3)을 발동한다.
    /// </summary>
    
    public void TryPressEscapeButton(Zone zone)
    {
        if (!HasStateAuthority) return;
        if (IsAct3Active) return;

        if (zone == Zone.ZoneA) IsZoneAEscapePressed = true;
        if (zone == Zone.ZoneB) IsZoneBEscapePressed = true;

        //타이머가 돌고 있지 않으면 0.5초 타이머 시간 (동시 입력 판정)
        if (!EscapeInputTimer.IsRunning)
        {
            EscapeInputTimer = TickTimer.CreateFromSeconds(Runner, 0.5f);
            Log($"{zone} 탈출 버튼 입력! 0.5초 대기 시작");
        }
    }

    public void TriggerAct3()
    {
        if (!HasStateAuthority || IsAct3Active)
            return;

        IsAct3Active = true;

        // 버튼 재입력 방지
        IsZoneAEscapeButtonExposed = false;
        IsZoneBEscapeButtonExposed = false;
        IsZoneAEscapePressed = false;
        IsZoneBEscapePressed = false;
        EscapeInputTimer = TickTimer.None;

        CreatureAI[] allCreature = FindObjectsByType<CreatureAI>(FindObjectsSortMode.None);
        foreach (CreatureAI creature in allCreature)
            creature.ApplyAct3Multipliers(true);

        ApplyRandomPatternToZone(Zone.ZoneA);
        ApplyRandomPatternToZone(Zone.ZoneB);

        ZoneLightingManager.GetManager(Zone.ZoneA)?.TriggerAct3Event(true);
        ZoneLightingManager.GetManager(Zone.ZoneB)?.TriggerAct3Event(true);

        RPC_PlayAct3Effects();

        GameSessionManager.Instance.notifyEscapeUnlocked();

        Log("3막(Act3) 진입 완료 | 크리처 강화 | 계단 차단 | 조명/사이렌 발동");
    }

    /// <summary>
    /// 특정 Zone에 랜덤 계단 차단 패턴을 적용한다.
    /// </summary>
    private void ApplyRandomPatternToZone(Zone zone)
    {
        bool isPattern1 = Random.value > 0.5f;

        if (zone == Zone.ZoneA)
        {
            if (zoneA_StairA_Top != null) zoneA_StairA_Top.SetActive(isPattern1);
            if (zoneA_StairB_Bottom != null) zoneA_StairB_Bottom.SetActive(isPattern1);

            if (zoneA_StairA_Bottom != null) zoneA_StairA_Bottom.SetActive(!isPattern1);
            if (zoneA_StairB_Top != null) zoneA_StairB_Top.SetActive(!isPattern1);

            Log($"ZoneA 3막 계단 차단 패턴 {(isPattern1 ? "1" : "2")} 적용");
            return;
        }

        if (zoneB_StairA_Top != null) zoneB_StairA_Top.SetActive(isPattern1);
        if (zoneB_StairB_Bottom != null) zoneB_StairB_Bottom.SetActive(isPattern1);

        if (zoneB_StairA_Bottom != null) zoneB_StairA_Bottom.SetActive(!isPattern1);
        if (zoneB_StairB_Top != null) zoneB_StairB_Top.SetActive(!isPattern1);

        Log($"ZoneB 3막 계단 차단 패턴 {(isPattern1 ? "1" : "2")} 적용");
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
        if (sirenAudioSource == null || sirenClip == null)
            return;

        sirenAudioSource.clip = sirenClip;
        sirenAudioSource.loop = true;
        sirenAudioSource.Play();
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

        NetZoneAStage3Completed = true;
        NetZoneBStage3Completed = true;

        //아직 3막이 아니면 탈출 버튼 강제 노출 적용
        if (!IsAct3Active) IsEscapeButtonExposed = true;        

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