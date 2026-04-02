using Fusion;
using UnityEngine;

// Photon Voice 2 네임스페이스
// 프로젝트에 Photon Voice 2 SDK가 임포트되어 있어야 함
using Photon.Voice.Unity;
using Photon.Voice.Fusion;

/// <summary>
/// 무전기 아이템
/// ItemObject 상속
///
/// 동작 방식:
///   우클릭 홀드(Walkie 버튼) 동안만 PTT(Push-To-Talk) 송신 활성화
///   Photon Voice 2 + Fusion Voice Bridge 연동
///   무전기 역할이 아닌 플레이어가 들어도 동일하게 동작
/// </summary>
public class WalkieTalkieItem : ItemObject
{
    // ─────────────────────────────────────────
    // 네트워크 동기화 변수
    // ─────────────────────────────────────────

    /// <summary>
    /// 현재 송신 중 여부 — 다른 클라이언트에서 UI/애니메이션 표시용
    /// </summary>
    [Networked] public bool IsTransmitting { get; private set; }

    // ─────────────────────────────────────────
    // Inspector 설정값
    // ─────────────────────────────────────────
    [Header("Photon Voice")]
    [SerializeField] private Recorder voiceRecorder;   // 마이크 입력 담당
    [SerializeField] private Speaker  voiceSpeaker;    // 수신 음성 출력 담당

    [Header("시각 피드백")]
    [SerializeField] private GameObject transmitIndicator;  // 송신 중 표시 오브젝트 (LED 등)

    // ─────────────────────────────────────────
    // 로컬 변수
    // ─────────────────────────────────────────
    private PlayerController _currentHolder;
    private bool             _wasTransmitting;  // 이전 틱 송신 상태 (변경 감지용)

    // ─────────────────────────────────────────
    // Fusion 생명주기
    // ─────────────────────────────────────────
    public override void Spawned()
    {
        base.Spawned();

        // 초기 송신 OFF
        SetTransmitState(false);

        if (transmitIndicator != null)
            transmitIndicator.SetActive(false);
    }

    // ─────────────────────────────────────────
    // Fusion 틱 업데이트
    // ─────────────────────────────────────────
    public override void FixedUpdateNetwork()
    {
        // 현재 들고 있는 플레이어의 입력만 처리
        if (_currentHolder == null) return;
        if (!HasInputAuthority)     return;

        if (!GetInput(out PlayerNetworkInput input)) return;

        bool wantsTransmit = input.Buttons.IsSet(InputButtons.Walkie);

        // 상태가 변경됐을 때만 RPC 전송 (매 틱 전송 방지)
        if (wantsTransmit != _wasTransmitting)
        {
            if (wantsTransmit) StartTransmit();
            else               StopTransmit();

            _wasTransmitting = wantsTransmit;
        }
    }

    // ─────────────────────────────────────────
    // PTT 송신 제어
    // ─────────────────────────────────────────

    /// <summary>
    /// 우클릭 press — 마이크 송신 시작
    /// </summary>
    private void StartTransmit()
    {
        RPC_SetTransmitting(true);
    }

    /// <summary>
    /// 우클릭 release — 마이크 송신 종료
    /// </summary>
    private void StopTransmit()
    {
        RPC_SetTransmitting(false);
    }

    /// <summary>
    /// 송신 상태 동기화 RPC
    /// StateAuthority가 IsTransmitting 갱신 → 모든 클라이언트에 전파
    /// </summary>
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_SetTransmitting(bool transmitting)
    {
        IsTransmitting = transmitting;
        SetTransmitState(transmitting);
    }

    /// <summary>
    /// Photon Voice Recorder의 실제 송신 상태 변경
    /// </summary>
    private void SetTransmitState(bool transmitting)
    {
        if (voiceRecorder != null)
            voiceRecorder.TransmitEnabled = transmitting;

        // 시각 피드백
        if (transmitIndicator != null)
            transmitIndicator.SetActive(transmitting);
    }

    // ─────────────────────────────────────────
    // [Networked] 변경 감지 — 원격 클라이언트 UI 처리
    // ─────────────────────────────────────────
    public override void Render()
    {
        // IsTransmitting 변경 시 원격 클라이언트에서도 indicator 갱신
        if (transmitIndicator != null)
            transmitIndicator.SetActive(IsTransmitting);
    }

    // ─────────────────────────────────────────
    // 장착 / 드랍 오버라이드
    // ─────────────────────────────────────────
    public override void OnEquipped(PlayerController actor, HandSlot slot)
    {
        base.OnEquipped(actor, slot);
        _currentHolder   = actor;
        _wasTransmitting = false;

        // Recorder는 들고 있는 플레이어가 로컬일 때만 활성화
        if (voiceRecorder != null)
            voiceRecorder.enabled = HasInputAuthority;
    }

    public override void OnDropped()
    {
        base.OnDropped();

        // 드랍 시 송신 강제 종료
        if (IsTransmitting)
            StopTransmit();

        _currentHolder   = null;
        _wasTransmitting = false;

        if (voiceRecorder != null)
            voiceRecorder.enabled = false;
    }

    // ─────────────────────────────────────────
    // IInteractable 오버라이드
    // ─────────────────────────────────────────
    public override string GetPromptText() => "무전기 집기";
}
