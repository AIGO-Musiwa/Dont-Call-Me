using Fusion;
using UnityEngine;

public class WalkieTalkieItem : ItemObject
{
    // ─── 컴포넌트 참조 ───────────────────────────────
    [Header("무전기 전용")]
    [SerializeField] private WalkieMeterialController materialController;
    [SerializeField] private AudioSource whiteNoiseSource;

    // ─── 네트워크 변수 ───────────────────────────────
    [Networked] public Zone NetZone { get; set; }       // 무전기가 속한 구역
    [Networked, OnChangedRender(nameof(OnWalkieStateChanged))]
    public WalkieState NetWalkieState {  get; set; }


    // ─── 초기화 ──────────────────────────────────────
    protected override void Awake()
    {
        itemType = ItemType.WalkieTalkie;
        isRoleItem = true;
        base.Awake();
    }

    public override void Spawned()
    {
        base.Spawned();

        if (HasStateAuthority)
        {
            NetWalkieState = WalkieState.Idle;
        }

        materialController?.SetWalkieState(WalkieState.Idle);

        WalkieTalkieManager.Instance?.RegisterWalkieTalkie(this);
    }

    // ────────────────────────────────────────────────
    // 서버 상태 변경
    public void ServerSetState(WalkieState state)
    {
        if (!HasStateAuthority) return;
        NetWalkieState = state;
    }

    // ────────────────────────────────────────────────
    private void OnWalkieStateChanged()
    {
        // material 업데이트
        materialController?.SetWalkieState(NetWalkieState);

        UpdateWhiteNoise();
    }

    // 화이트 노이즈 재생 여부를 현재 상태 + 근접 여부로 결정
    public void UpdateWhiteNoise()
    {
        if ( NetWalkieState != WalkieState.RX)
        {
            StopWhiteNoise();
            return;
        }

        if (CheckHearWhiteNoisePlayer())
            PlayeWhiteNoise();
        else
            StopWhiteNoise();
    }

    // 화이트 노이즈를 들어야 하는지 판단
    private bool CheckHearWhiteNoisePlayer()
    {
        if (!Runner.TryGetPlayerObject(Runner.LocalPlayer, out var localObj)) return false;
        PlayerController localPc = localObj.GetComponent<PlayerData>()?.GetPlayerController();
        if (localPc.NetZone != NetZone) return false;

        // 무전기 소지자는 항상 들림
        if (localPc.GetHeldWalkieTalkie() == this) return true;

        // 팀원은 무전기 범위 안에 있을 때만 들림
        return localPc.NetIsNearReceiver;
    }

    // 화이트 노이즈 재생
    private void PlayeWhiteNoise()
    {
        if (whiteNoiseSource != null && !whiteNoiseSource.isPlaying)
        {
            whiteNoiseSource.Play();
        }
    }

    // 화이트 노이즈 정지
    private void StopWhiteNoise()
    {
        if (whiteNoiseSource != null && whiteNoiseSource.isPlaying)
        {
            whiteNoiseSource.Stop();
        }
    }

    // ─── 오버라이드 ──────────────────────────────────

    // 드롭 처리 오버라이드
    public override void OnDropped(Vector3 worldPosition, Vector3 worldForward, float impulse)
    {
        if (HasStateAuthority)
        {
            WalkieTalkieManager.Instance?.OnWalkieTalkieDropped(NetCurrentHolder, this);
        }

        base.OnDropped(worldPosition, worldForward, impulse);
    }

    public override string GetPromptText(PlayerController actor)
    {
        if (actor != null && actor.NetRightHandItem != null)
            return "기존 아이템 내려놓고 무전기 줍기";

        return "무전기 줍기";
    }

    // ─── RPC ───────────────────────────────────────
    // PTT 상태 변경 요청
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_RequestPTT(bool isPressed)
    {
        WalkieTalkieManager.Instance?.HandlePTTRequest(Object.InputAuthority, isPressed, this);
    }

    // 무전음 dB 발행 요청
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_EmitWalkieSound(float voicedB)
    {
        // 수신 구역 무전기 위치로 발행
        Zone receiverZone = (NetZone == Zone.ZoneA) ? Zone.ZoneB : Zone.ZoneA;
        WalkieTalkieItem receiverWalkie = WalkieTalkieManager.Instance?.GetWalkieTalkieByZone(receiverZone);
        if (receiverWalkie == null) return;

        SoundEmitter.EmitToEventBus(SoundChannel.Walkie, voicedB, receiverWalkie.transform.position, 0f, receiverZone);
    }
}
