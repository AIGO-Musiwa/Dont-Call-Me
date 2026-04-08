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

        switch (NetWalkieState)
        {
            case WalkieState.RX:
                if (IsLocalPlayerInSameZone())
                    PlayeWhiteNoise();
                break;
            case WalkieState.TX:
            case WalkieState.Idle:
                StopWhiteNoise();
                break;
        }
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

    private bool IsLocalPlayerInSameZone()
    {
        if (!Runner.TryGetPlayerObject(Runner.LocalPlayer, out var localObj)) return false;
        if (!localObj.TryGetComponent(out PlayerController localPc)) return false;
        return localPc.NetZone == NetZone;
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
}
