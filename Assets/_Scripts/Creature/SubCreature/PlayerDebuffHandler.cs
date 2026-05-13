using Fusion;
using UnityEngine;

public class PlayerDebuffHandler : NetworkBehaviour
{
    [Networked] public float NetSpeedMultiplier { get; set; } = 1f; // 이속 배율
    [Networked] public bool NetIsSlowed { get; set; } = false; // 이속 감소 활성황 여부

    // ── 설정값 ────────────────────────────────────────────
    [Header("이속 감소 설정")]
    [Tooltip("서브 크리처 SlowAndCall 기믹 적용 시 이속 배율 (0 ~ 1)")]
    [SerializeField, Range(0.1f, 1f)] private float slowMultiplier = 0.6f;

    [Tooltip("디버프 지속 시간 (초)")]
    [SerializeField] private float debuffDuration = 3f;

    // ── 내부 상태 ─────────────────────────────────────────
    private float slowTimer = 0f;               // 디버프 자동 해제 타이머
    private NetworkId slowSourceId = default;   // 현재 디버프 건 개체의 NetworkId (중첩 방지)

    #region Network LifeCycle
    
    public override void Spawned()
    {
        if (HasStateAuthority)
        {
            NetSpeedMultiplier = 1f;
            NetIsSlowed = false;
        }
    }

    public override void FixedUpdateNetwork()
    {
        // 호스트만 타이머 감소 처리
        if (!HasStateAuthority) return;
        if (!NetIsSlowed) return;

        slowTimer -= Runner.DeltaTime;
        if (slowTimer <= 0f)
            RemoveSlowDebuff();
    }

    #endregion

    #region 외부 API

    // 이속 감소 디버프 적용
    public void ApplySlowDebuff(NetworkId sourceId)
    {
        if (!HasStateAuthority) return;

        // 같은 소스면 타이머 갱신만
        if (slowSourceId == sourceId)
        {
            slowTimer = debuffDuration;
            return;
        }

        // 다른 소스면 덮어쓰기
        slowSourceId = sourceId;
        slowTimer = debuffDuration;

        if (!NetIsSlowed)
        {
            NetSpeedMultiplier = slowMultiplier;
            NetIsSlowed = true;
        }
    }

    // 이속 감소 디버프 즉시 해제
    public void RemoveSlowDebuffBySource(NetworkId sourceId)
    {
        if (!HasStateAuthority) return;

        //자신이 건 디버프만 해제
        if (slowSourceId != sourceId) return;

        RemoveSlowDebuff();
    }

    #endregion

    #region 내부 해제 처리
    private void RemoveSlowDebuff()
    {
        NetSpeedMultiplier = 1f;
        NetIsSlowed = false;
        slowTimer = 0f;
        slowSourceId = default;
    }

    #endregion
}
