using Fusion;
using UnityEngine;

/// <summary>
/// 사망 게이지 관리
/// Restrained 상태 진입 후부터 게이지 증가 시작
/// 반복 포획 횟수에 따라 증가 속도 배율 증가
/// 게이지 100% 도달 시 CaptureSystem.OnDeath() 호출
/// </summary>
public class DeathGauge : NetworkBehaviour
{
    // ─────────────────────────────────────────
    // 네트워크 동기화 변수
    // ─────────────────────────────────────────

    /// <summary>
    /// 현재 게이지 값 0~100
    /// 클라이언트 HUD에서 실시간 표시
    /// </summary>
    [Networked, OnChangedRender(nameof(OnGaugeChanged))]
    public float Gauge { get; private set; }

    [Networked] public bool IsTicking { get; private set; }

    // ─────────────────────────────────────────
    // Inspector 설정값
    // ─────────────────────────────────────────
    [Header("게이지 설정")]
    [SerializeField] private float baseTickRate       = 5f;   // 초당 기본 증가량 (100 기준)
    [SerializeField] private float captureMultiplier  = 0.5f; // 포획 횟수당 추가 배율

    // ─────────────────────────────────────────
    // 로컬 변수
    // ─────────────────────────────────────────
    private float            _currentTickRate;
    private CaptureSystem    _captureSystem;

    // HUD 연동 이벤트
    public event System.Action<float> OnGaugeUpdated;  // 로컬 HUD 업데이트용

    // ─────────────────────────────────────────
    // Fusion 생명주기
    // ─────────────────────────────────────────
    public override void Spawned()
    {
        _captureSystem = GetComponent<CaptureSystem>();
        Gauge          = 0f;
        IsTicking      = false;
    }

    // ─────────────────────────────────────────
    // 틱 업데이트 (Host에서만 게이지 증가)
    // ─────────────────────────────────────────
    public override void FixedUpdateNetwork()
    {
        if (!Runner.IsServer) return;
        if (!IsTicking)       return;

        Gauge += _currentTickRate * Runner.DeltaTime;

        if (Gauge >= 100f)
        {
            Gauge = 100f;
            OnDeath();
        }
    }

    // ─────────────────────────────────────────
    // 외부 제어
    // ─────────────────────────────────────────

    /// <summary>
    /// CaptureSystem.EnterRestrained()에서 호출
    /// captureCount에 따라 증가 속도 결정
    ///
    /// tickRate 계산:
    ///   captureCount = 0 (첫 포획) → baseTickRate × 1.0
    ///   captureCount = 1            → baseTickRate × 1.5
    ///   captureCount = 2            → baseTickRate × 2.0
    /// </summary>
    public void StartTick(int captureCount)
    {
        if (!Runner.IsServer) return;

        _currentTickRate = baseTickRate * (1f + captureCount * captureMultiplier);
        IsTicking        = true;

        Debug.Log($"[DeathGauge] 게이지 시작 — 포획 횟수: {captureCount}, 틱레이트: {_currentTickRate}/s");
    }

    /// <summary>
    /// 구출 시 CaptureSystem.OnRescued()에서 호출
    /// </summary>
    public void StopTick()
    {
        if (!Runner.IsServer) return;
        IsTicking = false;
    }

    /// <summary>
    /// 구출 시 게이지 초기화
    /// </summary>
    public void ResetGauge()
    {
        if (!Runner.IsServer) return;
        Gauge     = 0f;
        IsTicking = false;
    }

    // ─────────────────────────────────────────
    // 사망 처리
    // ─────────────────────────────────────────
    private void OnDeath()
    {
        IsTicking = false;
        _captureSystem?.OnDeath();
    }

    // ─────────────────────────────────────────
    // [Networked] 변경 감지 — 로컬 HUD 업데이트
    // ─────────────────────────────────────────
    private void OnGaugeChanged()
    {
        OnGaugeUpdated?.Invoke(Gauge);
    }

    // ─────────────────────────────────────────
    // 유틸리티
    // ─────────────────────────────────────────
    public float GetGaugeNormalized() => Gauge / 100f;  // 0~1 정규화값 (UI Slider 등에 사용)
}
