using UnityEngine;

/// <summary>
/// 숫자 입력 퍼즐의 서랍 힌트 프리팹 루트 스크립트.
/// 
/// 역할
/// - answer seed를 직접 받아 정답 데이터를 재구성한다.
/// - 열린 서랍 개수만 꺼내서 DrawerHintDisplay에 전달한다.
/// - 실제 어떤 서랍이 열릴지는 DrawerHintDisplay가
///   자기 drawers.Count 기준으로 seed 랜덤 선택한다.
/// </summary>
public class DrawerHint : MonoBehaviour, IPuzzleSeedReceiver
{
    [Header("참조")]
    [SerializeField] private DrawerHintDisplay drawerDisplay; // 실제 서랍 표시 담당

    [Header("디버그")]
    [SerializeField] private bool enableDebugLog = true;

    private NumericCodeAnswerGenerator.NumericCodeAnswerData _answerData; // seed 기반 정답 데이터
    private bool _hasAnswerSeed;                                          // 시드 적용 완료 여부
    private int _appliedSeed;                                             // 적용된 seed

    /// <summary>
    /// answer seed를 받아 서랍 힌트를 재구성한다.
    /// </summary>
    public void ApplyAnswerSeed(int seed)
    {
        _appliedSeed = seed;
        _answerData = NumericCodeAnswerGenerator.Generate(seed);
        _hasAnswerSeed = true;

        ApplyDrawerState();

        Log($"서랍 힌트 시드 적용 완료 | seed = {seed} | openedCount = {_answerData.OpenedDrawerCount}");
    }

    /// <summary>
    /// 현재 정답 데이터의 열린 서랍 개수를 display에 반영한다.
    /// 실제 어떤 서랍이 열릴지는 display에서 처리한다.
    /// </summary>
    private void ApplyDrawerState()
    {
        if (!_hasAnswerSeed || _answerData == null)
            return;

        if (drawerDisplay == null)
            return;

        drawerDisplay.SetOpenedDrawerCount(_answerData.OpenedDrawerCount, _appliedSeed);
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

        Debug.Log($"[NumericDrawerHint] {message}", this);
    }
}