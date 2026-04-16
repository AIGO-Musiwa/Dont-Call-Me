using Fusion;
using UnityEngine;


/// <summary>
/// 퍼즐/힌트 루트에 붙는 시드 동기화용 NetworkBehaviour
/// 서버가 설정한 정답 시드를 모든 클라이언트에서 동일하게 적용
/// </summary>
public class PuzzleSeedSync : NetworkBehaviour
{
    [Header("디버그")]
    [SerializeField] private bool enableDebugLog = true;        // 디버그 로그 출력 여부

    [Networked, OnChangedRender(nameof(OnAnswerSeedChanged))]
    private int NetAnswerSeed { get; set; }                     // 네트워크 동기화되는 정답 시드

    private int _lastAplliedSeed = int.MinValue;                // 마지막으로 적용한 시드

    public override void Spawned()
    {
        TryApplySeed(NetAnswerSeed);
    }

    /// <summary>
    /// 서버에서 정답 시드를 설정
    /// </summary>
    public void ServerSetAnswerSeed(int seed)
    {
        if (!HasStateAuthority)
            return;

        if (seed == 0)
            seed = 1;

        NetAnswerSeed = seed;
        TryApplySeed(NetAnswerSeed);
        Log($"정답 시드 설정 : {NetAnswerSeed}");
    }

    /// <summary>
    /// 렌더 측 OnChanged 콜백
    /// 클라이언트가 새 시드를 받았을 때 적용0
    /// </summary>
    private void OnAnswerSeedChanged()
    {
        TryApplySeed(NetAnswerSeed);
    }


    /// <summary>
    /// 아직 적용 안 된 시드를 모든 수신기에 적용
    /// </summary>
    private void TryApplySeed(int seed)
    {
        if (seed == 0)
            return;

        if (_lastAplliedSeed == seed)
            return;

        _lastAplliedSeed = seed;

        MonoBehaviour[] behaviours = GetComponentsInChildren<MonoBehaviour>(true);
        for(int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IPuzzleSeedReceiver receiver)
            {
                receiver.ApplyAnswerSeed(seed);
            }
        }

        Log($"정답 시드 적용 완료 : {seed}");
    }


    private void Log(string m)
    {
        if (!enableDebugLog)
            return;

        Debug.Log($"[PuzzleSeedSync] {m}", this);
    }
}
