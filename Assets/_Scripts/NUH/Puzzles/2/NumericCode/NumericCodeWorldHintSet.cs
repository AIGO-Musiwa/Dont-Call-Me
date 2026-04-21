using UnityEngine;

/// <summary>
/// 2-1 숫자 입력 퍼즐의 반대편 Zone 월드 힌트 세트 루트
/// 
/// 역할
/// - 같은 answer seed를 받아 정답 데이터 재구성
/// - 시계 / 서랍 / 책 / 액자 힌트 오브젝트에 값 분배
/// </summary>
public class NumericCodeWorldHintSet : MonoBehaviour, IPuzzleSeedReceiver
{
    [Header("힌트 표시 스크립트")]
    [SerializeField] private ClockHintDisplay clockDisplay;             // 시계 힌트 표시 담당
    [SerializeField] private DrawerHintDisplay drawerDisplay;           // 서랍 "
    [SerializeField] private BookHintDisplay bookDisplay;               // 책 "
    [SerializeField] private PortraitFrameHintDisplay frameDisplay;     // 액자 "

    [Header("디버그")]
    [SerializeField] private bool enableDebugLog = true;

    private NumericCodeAnswerGenerator.NumericCodeAnswerData _answerData;   // seed 기반으로 재구성한 정답 데이터
    private bool _hasAnswerSeed;

    /// <summary>
    /// 같은 answer seed를 받아 월드 힌트 전체 상태 재구성
    /// </summary>
    public void ApplyAnswerSeed(int seed)
    {
        _answerData = NumericCodeAnswerGenerator.Generate(seed);
        _hasAnswerSeed = true;

        ApplyWorldHintState();

        Log($"월드 힌트 시드 적용 완료 | seed = {seed}");
        LogWorldHintDebug();
    }

    /// <summary>
    /// 현재 정답 데이터를 하위 월드 힌트 오브젝트들에 반영
    /// </summary>
    private void ApplyWorldHintState()
    {
        if (!_hasAnswerSeed || _answerData == null)
            return;

        if (clockDisplay != null)
            clockDisplay.SetHour(_answerData.ClockHour);

        if (drawerDisplay != null)
            drawerDisplay.SetOpenedDrawerCount(_answerData.OpenedDrawerCount);

        if (bookDisplay != null)
            bookDisplay.SetBookColors(_answerData.BookPlacement);

        if(frameDisplay != null)
            frameDisplay.SetMarkedFrameCount(_answerData.MarkedFrameCount);
    }

    /// <summary>
    /// 현재 재구성된 정답 데이터를 외부에서 읽을 수 있게 반환
    /// </summary>
    /// <returns></returns>
    public NumericCodeAnswerGenerator.NumericCodeAnswerData GetAnswerData()
    {
        return _answerData;
    }



    /// <summary>
    /// 디버그용 로그 출력.
    /// </summary>
    private void LogWorldHintDebug()
    {
        if (!enableDebugLog || _answerData == null)
            return;

        int redCount = GetBookCount(NumericCodeAnswerGenerator.NumericBookColor.Red);
        int greenCount = GetBookCount(NumericCodeAnswerGenerator.NumericBookColor.Green);
        int blueCount = GetBookCount(NumericCodeAnswerGenerator.NumericBookColor.Blue);
        int yellowCount = GetBookCount(NumericCodeAnswerGenerator.NumericBookColor.Yellow);

        Debug.Log(
            $"[NumericCodeWorldHintSet] " +
            $"Code={_answerData.GetFinalCodeString()} | " +
            $"Clock={_answerData.ClockHour} | " +
            $"Drawer={_answerData.OpenedDrawerCount} | " +
            $"Frame={_answerData.MarkedFrameCount} | " +
            $"TargetBookColor={_answerData.TargetBookColor} | " +
            $"Books(R/G/B/Y)={redCount}/{greenCount}/{blueCount}/{yellowCount}",
            this);
    }

    /// <summary>
    /// 일반 디버그 로그 출력.
    /// </summary>
    private void Log(string message)
    {
        if (!enableDebugLog)
            return;

        Debug.Log($"[NumericCodeWorldHintSet] {message}", this);
    }
}
