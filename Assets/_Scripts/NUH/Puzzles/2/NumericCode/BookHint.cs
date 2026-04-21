using UnityEngine;

/// <summary>
/// 숫자 입력 퍼즐의 책 힌트 프리팹 루트 스크립트.
/// 
/// 역할
/// - answer seed를 직접 받아 정답 데이터를 재구성한다.
/// - 책 20권의 색 배치 결과를 BookHintDisplay에 전달한다.
/// </summary>
public class BookHint : MonoBehaviour, IPuzzleSeedReceiver
{
    [Header("참조")]
    [SerializeField] private BookHintDisplay bookDisplay; // 실제 책 색 표시 담당

    [Header("디버그")]
    [SerializeField] private bool enableDebugLog = true;

    private NumericCodeAnswerGenerator.NumericCodeAnswerData _answerData; // seed 기반 정답 데이터
    private bool _hasAnswerSeed;                                          // 시드 적용 완료 여부

    /// <summary>
    /// answer seed를 받아 책 힌트를 재구성한다.
    /// </summary>
    public void ApplyAnswerSeed(int seed)
    {
        _answerData = NumericCodeAnswerGenerator.Generate(seed);
        _hasAnswerSeed = true;

        ApplyBookState();

        Log($"책 힌트 시드 적용 완료 | seed = {seed} | targetColor = {_answerData.TargetBookColor}");
    }

    /// <summary>
    /// 현재 정답 데이터의 책 색 배치를 display에 반영한다.
    /// </summary>
    private void ApplyBookState()
    {
        if (!_hasAnswerSeed || _answerData == null)
            return;

        if (bookDisplay == null)
            return;

        bookDisplay.SetBookColors(_answerData.BookPlacement);
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

        Debug.Log($"[NumericBookHint] {message}", this);
    }
}