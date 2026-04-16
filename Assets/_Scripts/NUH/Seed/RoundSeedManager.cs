using Fusion;
using UnityEngine;

/// <summary>
/// 이번 판의 기준 시드를 생성/보관하는 매니저
/// 같은 판의 모든 랜덤 계산은 이 시드를 기준으로 한다
/// </summary>
public class RoundSeedManager : NetworkBehaviour
{
    public static RoundSeedManager Instance { get; private set; }

    [Header("디버그")]
    [SerializeField] private bool enableDebugLog = true;   // 시드 로그 출력 여부
    [SerializeField] private bool useFixedSeed = false;    // 고정 시드 사용 여부
    [SerializeField] private int fixedSeed = 12345;        // 디버그용 고정 시드

    [Networked] public int NetRoundSeed { get; private set; } // 현재 라운드 시드

    public bool HasValidSeed => NetRoundSeed != 0;
    public int CurrentSeed => NetRoundSeed;

    public override void Spawned()
    {
        Instance = this;
        Log($"Spawned 호출 | IsServer={Runner.IsServer} | HasStateAuthority={HasStateAuthority} | CurrentSeed={NetRoundSeed}");
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>
    /// 서버에서 이번 판 시드를 보장한다
    /// 이미 시드가 있으면 그대로 유지
    /// </summary>
    public void EnsureRoundSeed()
    {
        if (!HasStateAuthority)
        {
            Log("HasStateAuthority가 없어 시드 생성 안 함");
            return;
        }

        if (NetRoundSeed != 0)
        {
            Log($"기존 시드 유지 : {NetRoundSeed}");
            return;
        }

        NetRoundSeed = useFixedSeed ? fixedSeed : CreateRuntimeSeed();
        Log($"Round Seed Initialized : {NetRoundSeed}");
    }

    public void ForceSetSeed(int seed)
    {
        if (!HasStateAuthority)
            return;

        NetRoundSeed = seed == 0 ? 1 : seed;
        Log($"Round Seed Forced : {NetRoundSeed}");
    }

    private int CreateRuntimeSeed()
    {
        int seed = System.Environment.TickCount;
        if (seed == 0)
            seed = 1;

        return seed;
    }

    private void Log(string message)
    {
        if (!enableDebugLog)
            return;

        Debug.Log($"[RoundSeedManager] {message}", this);
    }
}