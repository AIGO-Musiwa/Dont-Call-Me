using Fusion;
using UnityEngine;

public class StageManager : NetworkBehaviour
{
    public static StageManager Instance { get; private set; }

    [Header("퍼즐 관리 매니저 참조")]
    public PuzzleProgressManager puzzleProgressManager;

    [Header("3막 연출 소리 설정")]
    public AudioSource sirenAudioSource;
    public AudioClip sirenClip;

    [Header("Zone A 계단 차단 (셔터)")]
    public GameObject zoneA_StairA_Top;    //3F-2F
    public GameObject zoneA_StairA_Bottom; //2F-1F
    public GameObject zoneA_StairB_Top;    //3F-2F
    public GameObject zoneA_StairB_Bottom; //2F-1F

    [Header("Zone B 계단 차단 (셔터)")]
    public GameObject zoneB_StairA_Top;    //3F-2F
    public GameObject zoneB_StairA_Bottom; //2F-1F
    public GameObject zoneB_StairB_Top;    //3F-2F
    public GameObject zoneB_StairB_Bottom; //2F-1F

    //3막 진행 여부를 모든 클라이언트가 알 수 있도록 선언
    [Networked] public NetworkBool IsAct3Active { get; set; }

    public override void Spawned()
    {
        //싱글톤
        if (Instance == null) Instance = this;

        //호스트 서버에서만 초기 셋팅 진행
        if (HasStateAuthority)
        {
            IsAct3Active = false;

            //게임 시작 시, 모든 셔터 비활성화
            SetAllStairBlocksActive(false);
        }
    }

    #region 메인 루프 및 퍼즐 진행도 감시
    public override void FixedUpdateNetwork()
    {
        //호스트 서버에서 매 프레임 감시 로직 실행
        if (!HasStateAuthority) return;

        //매 프레임 진행 상황 확인 후 승인
        if (puzzleProgressManager != null && puzzleProgressManager.IsRegistered)
        {
            //A동, B동 1단계 퍼즐이 모두 풀리면 2단계 퍼즐 해금
            if (puzzleProgressManager.AreZoneAStage1PuzzlesSolved()) puzzleProgressManager.HandleZoneStage2Unlocked(Zone.ZoneA);
            if (puzzleProgressManager.AreZoneBStage1PuzzlesSolved()) puzzleProgressManager.HandleZoneStage2Unlocked(Zone.ZoneB);
        }
    }

    public void ReportZoneStage1Completed(Zone zone)
    {
        if (!HasStateAuthority) return;

        if (puzzleProgressManager != null)
        {
            //2단계 퍼즐 해금 승인
            puzzleProgressManager.HandleZoneStage2Unlocked(zone);
            Debug.Log($"[StageManager] {zone}의 Stage 2 해금을 승인했습니다.");
        }
    }

    #endregion

    #region 3막 이벤트 로직
    public void TriggerAct3()
    {
        //권한 확인 및 중복 실행 방지
        if (!HasStateAuthority || IsAct3Active) return;

        IsAct3Active = true;

        //맵 내 모든 크리쳐 능력치 강화
        CreatureAI[] allCreature = FindObjectsByType<CreatureAI>(FindObjectsSortMode.None);
        foreach (CreatureAI creature in allCreature) creature.ApplyAct3Multipliers(true);

        //각 구역별 랜덤 계단 차단 패턴 적용
        ApplyRandomPatternToZone(Zone.ZoneA);
        ApplyRandomPatternToZone(Zone.ZoneB);

        //각 구역 조명 매니저를 통한 3막 붉은 조명 연출 발동
        ZoneLightingManager.GetManager(Zone.ZoneA)?.TriggerAct3Event(true);
        ZoneLightingManager.GetManager(Zone.ZoneB)?.TriggerAct3Event(true);

        //전역 사이렌 소리 실행 (RPC)
        RPC_PlayAct3Effects();
        Debug.Log("[StageManager] 3막(Act 3) 진입: 크리처 강화, 계단 차단, 조명 및 사이렌 발동 완료");
    }

    //3층에서 1층 이동 시 무조건 2층을 횡단하도록 엇갈림 패턴 무작위 적용
    private void ApplyRandomPatternToZone(Zone zone)
    {
        //50%확률로 패턴 결정
        bool isPattern1 = Random.value > 0.5f;

        if (zone == Zone.ZoneA)
        {
            //패턴 1: A상단, B하단 차단 / 패턴 2: A하단, B상단 차단
            if (zoneA_StairA_Top != null) zoneA_StairA_Top.SetActive(isPattern1);
            if (zoneA_StairB_Bottom != null) zoneA_StairB_Bottom.SetActive(isPattern1);

            if (zoneA_StairA_Bottom != null) zoneA_StairA_Bottom.SetActive(!isPattern1);
            if (zoneA_StairB_Top != null) zoneA_StairB_Top.SetActive(!isPattern1);

            Debug.Log($"[StageManager] Zone A 3막 계단 차단 패턴 {(isPattern1 ? "1" : "2")} 적용");
        }

        else if (zone == Zone.ZoneB)
        {
            //패턴 1: A상단, B하단 차단 / 패턴 2: A하단, B상단 차단
            if (zoneB_StairA_Top != null) zoneB_StairA_Top.SetActive(isPattern1);
            if (zoneB_StairB_Bottom != null) zoneB_StairB_Bottom.SetActive(isPattern1);

            if (zoneB_StairA_Bottom != null) zoneB_StairA_Bottom.SetActive(!isPattern1);
            if (zoneB_StairB_Top != null) zoneB_StairB_Top.SetActive(!isPattern1);

            Debug.Log($"[StageManager] Zone B 3막 계단 차단 패턴 {(isPattern1 ? "1" : "2")} 적용");
        }
    }

    //모든 계단 차단벽 일괄 제어 함수
    private void SetAllStairBlocksActive(bool active)
    {
        //Zone A
        if (zoneA_StairA_Top != null) zoneA_StairA_Top.SetActive(active);
        if (zoneA_StairA_Bottom != null) zoneA_StairA_Bottom.SetActive(active);
        if (zoneA_StairB_Top != null) zoneA_StairB_Top.SetActive(active);
        if (zoneA_StairB_Bottom != null) zoneA_StairB_Bottom.SetActive(active);

        //Zone B
        if (zoneB_StairA_Top != null) zoneB_StairA_Top.SetActive(active);
        if (zoneB_StairA_Bottom != null) zoneB_StairA_Bottom.SetActive(active);
        if (zoneB_StairB_Top != null) zoneB_StairB_Top.SetActive(active);
        if (zoneB_StairB_Bottom != null) zoneB_StairB_Bottom.SetActive(active);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayAct3Effects()
    {
        //사이렌 오이도 동기화 재생
        if (sirenAudioSource != null && sirenClip != null)
        {
            sirenAudioSource.clip = sirenClip;
            sirenAudioSource.loop = true;
            sirenAudioSource.Play();
        }
    }
    #endregion

    #region 디버그 및 테스트
    //인스펙터의 StageManager 컴포넌트를 우클릭하여 실행
    [ContextMenu("Debug/1단계 완료 강제 승인 (2단계 해금)")]
    private void DebugForceUnlockStage2()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[StageManager] 플레이 모드에서만 실행 가능합니다.");
            return;
        }

        if (!HasStateAuthority)
        {
            Debug.LogWarning("[StageManager] 상태 권한이 있는 서버(호스트)에서만 실행 가능합니다.");
            return;
        }

        if (puzzleProgressManager != null)
        {
            //타 개발자 코드의 테스트용 함수 호출하여 양쪽 구역 화면 모두 켬
            puzzleProgressManager.HandleAllZonesStage2Unlocked();
            Debug.Log("[StageManager] 디버그: 양쪽 구역의 2단계 해금을 강제로 승인했습니다.");
        }
        else
        {
            Debug.LogWarning("[StageManager] 디버그: PuzzleProgressManager가 연결되어 있지 않습니다.");
        }
    }

    //인스펙터의 StageManager 컴포넌트를 우클릭하여 실행
    [ContextMenu("Debug/3막(Act 3) 강제 진입")]
    private void DebugForceTriggerAct3()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[StageManager] 플레이 모드에서만 실행 가능합니다.");
            return;
        }

        if (!HasStateAuthority)
        {
            Debug.LogWarning("[StageManager] 상태 권한이 있는 서버(호스트)에서만 실행 가능합니다.");
            return;
        }

        //조건 무시하고 즉시 3막 발동
        TriggerAct3();
        Debug.Log("[StageManager] 디버그: 3막을 강제로 발동시켰습니다.");
    }
    #endregion
}