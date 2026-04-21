using Fusion;
using Photon.Voice.Unity;
using System.Collections.Generic;
using UnityEngine;

public class WalkieTalkieItem : ItemObject
{
    // ─── 컴포넌트 참조 ───────────────────────────────
    [Header("무전기 전용")]
    [SerializeField] private WalkieMeterialController materialController;
    [SerializeField] private AudioSource whiteNoiseSource;

    [Header("무전 음성 감쇠 설정")]
    [SerializeField] private float walkieVoiceMinDistance = 2f;

    // ─── 네트워크 변수 ───────────────────────────────
    [Networked, OnChangedRender(nameof(OnZoneAssigned))]
    public Zone NetZone { get; set; }       // 무전기가 속한 구역
    [Networked, OnChangedRender(nameof(OnWalkieStateChanged))]
    public WalkieState NetWalkieState { get; set; }

    // 송신자 구역 플레이어 AudioSource 캐시
    private readonly List<AudioSource> senderZoneAudioSources = new();

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

        // 화이트 노이즈 AudioSource 3D 감쇠 설정
        if (whiteNoiseSource != null)
        {
            whiteNoiseSource.spatialBlend = 1f;
            whiteNoiseSource.rolloffMode = AudioRolloffMode.Logarithmic;
            whiteNoiseSource.minDistance = 1f;
            whiteNoiseSource.maxDistance = Constants.WALKIE_RANGE;
            whiteNoiseSource.dopplerLevel = 0f;
        }
    }

    private void Update()
    {
        // 🛠️ [안전 차단기 추가] 퓨전 네트워크 전원이 들어오기 전에는 볼륨 조절 모터 가동 중지!
        if (Object == null || !Object.IsValid) return;

        UpdateWalkieVoiceVolume();
    }

    // ────────────────────────────────────────────────

    private void OnZoneAssigned()
    {
        if (senderZoneAudioSources.Count > 0) return;

        Zone senderZone = NetZone == Zone.ZoneA ? Zone.ZoneB : Zone.ZoneA;
        var players = WalkieTalkieManager.Instance?.GetCachedPlayers();
        if (players == null) return;

        foreach (var pc in players)
        {
            if (pc == null) continue;
            if (pc.NetZone != senderZone) continue;
            AudioSource audioSource = pc.GetComponent<AudioSource>();
            if (audioSource != null)
                senderZoneAudioSources.Add(audioSource);
        }
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

        //PTT 종료 시 Speaker 볼륨 초기화
        if (NetWalkieState != WalkieState.RX)
        {
            foreach (var audioSource in senderZoneAudioSources)
            {
                if (audioSource != null) audioSource.volume = 1f;
            }
        }
    }

    // 화이트 노이즈 재생 여부를 현재 상태 + 근접 여부로 결정
    public void UpdateWhiteNoise()
    {
        if (NetWalkieState != WalkieState.RX)
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

    // 무전기와의 거리에 따라 소리 조절
    private void UpdateWalkieVoiceVolume()
    {
        // 무전기 수신 상태일 때만 처리
        if (NetWalkieState != WalkieState.RX) return;

        // 로컬 플레이어 가져오기
        if (!Runner.TryGetPlayerObject(Runner.LocalPlayer, out var localObj)) return;
        PlayerController localPc = localObj.GetComponent<PlayerData>()?.GetPlayerController();
        if (localPc == null) return;

        // 송신자 구역 팀원은 처리 X
        if (localPc.NetZone != NetZone) return;

        // ActiveSender Speaker 캐싱
        PlayerRef activeSender = WalkieTalkieManager.Instance?.GetActiveSender() ?? PlayerRef.None;
        if (activeSender == PlayerRef.None)
        {
            foreach (var audioSource in senderZoneAudioSources)
            {
                if (audioSource != null) audioSource.volume = 1f;
            }

            return;
        }

        if (senderZoneAudioSources.Count == 0) return;

        // 로컬 플레이어와 수신 무전기 사이 거리 계산
        float dist = Vector3.Distance(localPc.transform.position, transform.position);

        // Logarithmic 감쇠
        float volume = dist >= Constants.WALKIE_RANGE
            ? 0f
            : Mathf.Clamp01(walkieVoiceMinDistance / Mathf.Max(dist, walkieVoiceMinDistance));

        foreach (var audioSource in senderZoneAudioSources)
        {
            if (audioSource != null) audioSource.volume = volume;
        }
    }

    // ─── RPC (수리 완료: 권한 개방 및 소지자 검증 로직 추가) ────────────────

    // PTT 상태 변경 요청
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)] // 🛠️ [수리] InputAuthority -> All
    public void RPC_RequestPTT(bool isPressed, RpcInfo info = default) // 🛠️ RpcInfo 부품 추가
    {
        // 🛠️ [보안 회로] 이 스위치를 누른 사람(info.Source)이 실제 소지자(NetCurrentHolder)인지 검사!
        if (info.Source != NetCurrentHolder) return;

        WalkieTalkieManager.Instance?.HandlePTTRequest(info.Source, isPressed, this);
    }

    // 무전음 dB 발행 요청
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)] // 🛠️ [수리] InputAuthority -> All
    public void RPC_EmitWalkieSound(float voicedB, RpcInfo info = default) // 🛠️ RpcInfo 부품 추가
    {
        // 🛠️ [보안 회로] 실제 소지자만 소리를 낼 수 있음
        if (info.Source != NetCurrentHolder) return;

        // 수신 구역 무전기 위치로 발행
        Zone receiverZone = (NetZone == Zone.ZoneA) ? Zone.ZoneB : Zone.ZoneA;
        WalkieTalkieItem receiverWalkie = WalkieTalkieManager.Instance?.GetWalkieTalkieByZone(receiverZone);
        if (receiverWalkie == null) return;

        SoundEmitter.EmitToEventBus(SoundChannel.Walkie, voicedB, receiverWalkie.transform.position, 0f, receiverZone);
    }
}