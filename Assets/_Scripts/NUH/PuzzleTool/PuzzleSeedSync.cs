using Fusion;
using UnityEngine;

/// <summary>
/// 퍼즐/힌트 루트에 붙는 시드 동기화용 NetworkBehaviour.
/// 
/// 역할
/// - 서버가 설정한 정답 시드를 Networked 값으로 동기화한다.
/// - IPuzzleSeedReceiver를 구현한 퍼즐/힌트 컴포넌트에 같은 seed를 적용한다.
/// 
/// 핵심 변경점
/// - Spawned / OnChangedRender 시점에 즉시 ApplyAnswerSeed를 호출하지 않는다.
/// - seed 적용을 예약해두고 Render()에서 한 번 늦게 적용한다.
/// - 다른 NetworkBehaviour / View / 자식 컴포넌트 초기화가 끝난 뒤 적용되도록 안정성을 높인다.
/// </summary>
public class PuzzleSeedSync : NetworkBehaviour
{
    [Header("디버그")]
    [SerializeField] private bool enableDebugLog = true; // 디버그 로그 출력 여부

    [Networked, OnChangedRender(nameof(OnAnswerSeedChanged))]
    private int NetAnswerSeed { get; set; } // 네트워크 동기화되는 정답 시드

    private int _lastAppliedSeed = int.MinValue; // 마지막으로 적용한 시드
    private bool _pendingApply;                  // Render에서 seed 적용을 예약했는지 여부

    public override void Spawned()
    {
        // Spawned 시점에는 다른 NetworkBehaviour / 자식 컴포넌트 초기화 순서가 완전히 보장되지 않을 수 있다.
        // 따라서 즉시 적용하지 않고 Render에서 적용하도록 예약한다.
        RequestApplySeed();

        Log($"Spawned | currentSeed={NetAnswerSeed} | seed 적용 예약");
    }

    public override void Render()
    {
        if (!_pendingApply)
            return;

        _pendingApply = false;

        TryApplySeed(NetAnswerSeed);
    }

    /// <summary>
    /// 서버에서 정답 시드를 설정한다.
    /// </summary>
    public void ServerSetAnswerSeed(int seed)
    {
        if (!HasStateAuthority)
            return;

        if (seed == 0)
            seed = 1;

        NetAnswerSeed = seed;

        // 서버에서도 즉시 적용하지 않고 Render에서 적용되도록 예약한다.
        RequestApplySeed();

        Log($"정답 시드 설정 | seed={NetAnswerSeed} | 적용 예약");
    }

    /// <summary>
    /// 렌더 측 OnChanged 콜백.
    /// 클라이언트가 새 시드를 받았을 때 즉시 적용하지 않고 예약한다.
    /// </summary>
    private void OnAnswerSeedChanged()
    {
        RequestApplySeed();

        Log($"정답 시드 변경 감지 | seed={NetAnswerSeed} | 적용 예약");
    }

    /// <summary>
    /// 현재 NetAnswerSeed를 Render에서 적용하도록 예약한다.
    /// </summary>
    private void RequestApplySeed()
    {
        if (NetAnswerSeed == 0)
            return;

        if (_lastAppliedSeed == NetAnswerSeed)
            return;

        _pendingApply = true;
    }

    /// <summary>
    /// 아직 적용하지 않은 seed를 모든 IPuzzleSeedReceiver 수신기에 적용한다.
    /// </summary>
    private void TryApplySeed(int seed)
    {
        if (seed == 0)
            return;

        if (_lastAppliedSeed == seed)
            return;

        _lastAppliedSeed = seed;

        MonoBehaviour[] behaviours = GetComponentsInChildren<MonoBehaviour>(true);
        int receiverCount = 0;

        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour == null)
                continue;

            if (behaviour is IPuzzleSeedReceiver receiver)
            {
                receiver.ApplyAnswerSeed(seed);
                receiverCount++;

                Log($"수신기 seed 적용 | receiver={behaviour.GetType().Name} | object={behaviour.name} | seed={seed}");
            }
        }

        Log($"정답 시드 적용 완료 | seed={seed} | receiverCount={receiverCount}");
    }

    private void Log(string message)
    {
        if (!enableDebugLog)
            return;

        Debug.Log($"[PuzzleSeedSync] {message}", this);
    }
}