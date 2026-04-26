using UnityEngine;

/// <summary>
/// 숫자 입력 퍼즐의 시계 힌트 프리팹 루트 스크립트.
/// 
/// 역할
/// - answer seed를 직접 받아 정답 데이터를 재구성한다.
/// - 시계 값만 꺼내서 ClockHintDisplay에 전달한다.
/// </summary>
public class ClockHint : MonoBehaviour, IPuzzleSeedReceiver
{
    [Header("참조")]
    [SerializeField] private ClockHintDisplay clockDisplay; // 실제 시계 표시 담당

    [Header("디버그")]
    [SerializeField] private bool enableDebugLog = true;

    private NumericCodeAnswerGenerator.NumericCodeAnswerData _answerData; // seed 기반 정답 데이터
    private bool _hasAnswerSeed;                                          // 시드 적용 완료 여부

    /// <summary>
    /// answer seed를 받아 시계 힌트를 재구성한다.
    /// </summary>
    public void ApplyAnswerSeed(int seed)
    {
        _answerData = NumericCodeAnswerGenerator.Generate(seed);
        _hasAnswerSeed = true;

        ApplyClockState();

        Log($"시계 힌트 시드 적용 완료 | seed = {seed} | hour = {_answerData.ClockHour}");
    }

    /// <summary>
    /// 현재 정답 데이터의 시계 값을 display에 반영한다.
    /// </summary>
    private void ApplyClockState()
    {
        if (!_hasAnswerSeed || _answerData == null)
            return;

        if (clockDisplay == null)
            return;

        clockDisplay.SetHour(_answerData.ClockHour);
    }

    /// <summary>
    /// 현재 재구성된 정답 데이터를 외부에서 읽을 수 있게 반환한다.
    /// </summary>
    public NumericCodeAnswerGenerator.NumericCodeAnswerData GetAnswerData()
    {
        return _answerData;
    }

    private void Log(string message)
    {
        if (!enableDebugLog)
            return;

        Debug.Log($"[NumericClockHint] {message}", this);
    }
}