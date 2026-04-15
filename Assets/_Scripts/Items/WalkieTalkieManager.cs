using Fusion;
using System.Collections.Generic;
using UnityEngine;

public class WalkieTalkieManager : NetworkBehaviour
{
    public static WalkieTalkieManager Instance { get; private set; }

    [Header("무전 설정")]
    [Tooltip("팀원 목소리가 무전기에 들어갈는 거리(m)")]
    [SerializeField] private float walkiePickupRange = 3f;

    // ──── 네트워크 변수 ───────────────────────────
    [Networked, OnChangedRender(nameof(OnActiveSenderChanged))]
    private PlayerRef ActiveSender { get; set; }        // 송신권을 가진 플레이어
    [Networked] private PlayerRef PendingSender { get; set; }       // 송신 대기자
    [Networked] private Zone ActiveSenderZone {  get; set; }        // 송신자 구역

    // ──── 캐시 ────────────────────────────────────
    private readonly List<WalkieTalkieItem> allWalkies = new();
    private readonly List<PlayerController> cachedPlayers = new();

    public void RegisterWalkieTalkie(WalkieTalkieItem walkie)
    {
        if (!allWalkies.Contains(walkie))
        {
            allWalkies.Add(walkie);
        }
    }

    // 플레이어 등록
    public void RegisterPlayer(PlayerController pc)
    {
        if (!cachedPlayers.Contains(pc))
        {
            cachedPlayers.Add(pc);
        }
    }


    // ──── 초기화 ──────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public override void Spawned()
    {
        if (!HasStateAuthority) return;

        ActiveSender = PlayerRef.None;
        PendingSender = PlayerRef.None;
        ActiveSenderZone = Zone.ZoneA;
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;
        if (ActiveSender == PlayerRef.None) return;

        // 송신자 / 수신자 구역 무전기
        WalkieTalkieItem senderWalkie = GetWalkieTalkieByZone(ActiveSenderZone);
        Zone receiverZone = ActiveSenderZone == Zone.ZoneA ? Zone.ZoneB : Zone.ZoneA;
        WalkieTalkieItem receiverWalkie = GetWalkieTalkieByZone(receiverZone);

        foreach (var pc in cachedPlayers)
        {
            if (pc == null) continue;
            if (pc.Object.InputAuthority == ActiveSender) continue;
            if (pc.GetHeldWalkieTalkie() != null) continue;         // 무전기 소지자 스킵

            if (pc.NetZone != ActiveSenderZone)
            {
                // 수신자 구역 팀원 - 수신 무전기와의 거리 체크
                if (receiverWalkie == null) continue;
                float sqrDist = (pc.transform.position - receiverWalkie.transform.position).sqrMagnitude;
                bool isNear = sqrDist <= walkiePickupRange * walkiePickupRange;

                if (pc.NetIsNearReceiver != isNear)
                {
                    pc.NetIsNearReceiver = isNear;
                }
            }
            else
            {
                // 송신자 구역 팀원 - 송신 무전기와의 거리 체크
                if (senderWalkie == null) continue;
                float sqrDist = (pc.transform.position - senderWalkie.transform.position).sqrMagnitude;
                bool isNear = sqrDist <= walkiePickupRange * walkiePickupRange;

                if (pc.NetIsNearSender != isNear)
                {
                    pc.NetIsNearSender = isNear;
                }
            }
        }
        
    }

    // ──── PTT 처리 ───────────────────────────────
    public void HandlePTTRequest(PlayerRef caller, bool isPressed, WalkieTalkieItem walkie)
    {
        if (!HasStateAuthority) return;

        if (isPressed) AcquirePTT(caller, walkie);
        else ReleasePTT(caller, walkie);
    }

    // PTT 등록
    private void AcquirePTT(PlayerRef caller, WalkieTalkieItem walkie)
    {
        if (ActiveSender != PlayerRef.None)
        {
            PendingSender = caller;
            walkie.ServerSetState(WalkieState.RX);
            return;
        }

        ActiveSender = caller;
        ActiveSenderZone = walkie.NetZone;
        PendingSender = PlayerRef.None;
        walkie.ServerSetState(WalkieState.TX);

        // 수신자 구역 무전기 RX 설정 → 화이트 노이즈 재생
        Zone recvZone = ActiveSenderZone == Zone.ZoneA ? Zone.ZoneB : Zone.ZoneA;
        WalkieTalkieItem recvWalkie = GetWalkieTalkieByZone(recvZone);
        if (recvWalkie != null)
            recvWalkie.ServerSetState(WalkieState.RX);
    }

    // PTT 해제
    private void ReleasePTT(PlayerRef caller, WalkieTalkieItem walkie)
    {
        if (ActiveSender == caller)
        {
            walkie.ServerSetState(WalkieState.Idle);

            // 수신자 구역 무전기도 Idle로
            Zone recvZone = ActiveSenderZone == Zone.ZoneA ? Zone.ZoneB : Zone.ZoneA;
            WalkieTalkieItem recvWalkie = GetWalkieTalkieByZone(recvZone);
            if (recvWalkie != null)
                recvWalkie.ServerSetState(WalkieState.Idle);

            if (PendingSender != PlayerRef.None)
            {
                ActiveSender = PendingSender;
                PendingSender = PlayerRef.None;

                // 새 송신자 구역의 무전기를 TX로 전환
                if (Runner.TryGetPlayerObject(ActiveSender, out var newSenderObj))
                {
                    PlayerController newSenderPc = newSenderObj.GetComponent<PlayerData>()?.GetPlayerController();

                    if (newSenderPc != null)
                    {
                        ActiveSenderZone = newSenderPc.NetZone;
                        WalkieTalkieItem newWalkie = GetWalkieTalkieByZone(ActiveSenderZone);
                        if (newWalkie != null)
                        {
                            newWalkie.ServerSetState(WalkieState.TX);
                        }

                        Zone newRecvZone = ActiveSenderZone == Zone.ZoneA ? Zone.ZoneB : Zone.ZoneA;
                        WalkieTalkieItem newRecvWalkie = GetWalkieTalkieByZone(newRecvZone);
                        if (newRecvWalkie != null)
                            newRecvWalkie.ServerSetState(WalkieState.RX);
                    }
                }
            }
            else
            {
                ActiveSender = PlayerRef.None;
            }
        }
        else if (PendingSender == caller)
        {
            PendingSender = PlayerRef.None;
            if (ActiveSender == PlayerRef.None)
            {
                walkie.ServerSetState(WalkieState.Idle);
            }
        }
    }

    // ──── 무전기 드롭 처리 ────────────────────────

    public void OnWalkieTalkieDropped(PlayerRef holder, WalkieTalkieItem walkie)
    {
        if (!HasStateAuthority) return;
        ReleasePTT(holder, walkie);
    }


    // ──── Voice Group 전환 ───────────────────────

    // 각 클라이언트는 자신의 역할에 맞게 Voice Group 전환
    private void OnActiveSenderChanged()
    {
        if (VoiceManager.Instance == null) return;

        if (ActiveSender != PlayerRef.None)
        {
            HandlePTTStarted();
        }
        else
        {
            HandlePTTEnded();
        }
    }

    private void HandlePTTStarted()
    {
        if (!Runner.TryGetPlayerObject(Runner.LocalPlayer, out var localObj)) return;
        PlayerController localPc = localObj.GetComponent<PlayerData>()?.GetPlayerController();

        if (ActiveSender == Runner.LocalPlayer)
        {
            VoiceManager.Instance?.SetPTT(true);
            MicrophonedBMeasurer.Instance?.SetPTTActive(true);
        }
        else
        {
            // 수신자 구역 무전기 소지자
            if (localPc.GetHeldWalkieTalkie() != null)
            {
                VoiceManager.Instance.SetRemotePTT(true, ActiveSenderZone);
            }
        }
    }

    private void HandlePTTEnded()
    {
        if (!Runner.TryGetPlayerObject(Runner.LocalPlayer, out var localObj)) return;
        PlayerController localPc = localObj.GetComponent<PlayerData>()?.GetPlayerController();


        if (ActiveSenderZone == localPc.NetZone)
        {
            if (localPc.GetHeldWalkieTalkie() != null)
            {
                VoiceManager.Instance?.SetPTT(false);
                MicrophonedBMeasurer.Instance.SetPTTActive(false);
            }
        }
        else
        {
            // 수신자 구역 무전기 소지자
            if (localPc.GetHeldWalkieTalkie() != null)
            {
                VoiceManager.Instance.SetRemotePTT(false, ActiveSenderZone);
            }
        }
    }

    // 특정 구역의 무전기 찾는 함수
    public WalkieTalkieItem GetWalkieTalkieByZone(Zone targetZone)
    {
        // 리스트를 순회하며 현재 해당 구역(targetZone)에 있는 무전기를 찾아서 반환
        foreach (var walkie in allWalkies)
        {
            if (walkie.NetZone == targetZone)
                return walkie;
        }
        return null;
    }
}
