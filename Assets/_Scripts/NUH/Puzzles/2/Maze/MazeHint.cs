using UnityEngine;

/// <summary>
/// 5x5 미로 퍼즐의 힌트 프리팹 루트 로직.
/// 
/// 역할
/// - answer seed를 받아 동일한 미로 데이터를 재구성한다.
/// - 힌트 화면에 미로 벽 정보와 목표 위치를 전달한다.
/// </summary>
public class MazeHint : MonoBehaviour, IPuzzleSeedReceiver
{
    [Header("참조")]
    [SerializeField] private MazeHintView hintView; // 힌트 화면 표시 담당 뷰

    [Header("디버그")]
    [SerializeField] private bool enableDebugLog = true; // 디버그 로그 출력 여부

    private MazeAnswerGenerator.MazeAnswerData _answerData; // seed 기반으로 재구성한 미로 데이터
    private bool _hasAnswerSeed;                            // answer seed 적용 완료 여부

    /// <summary>
    /// answer seed를 받아 미로 힌트 데이터를 재구성한다.
    /// </summary>
    public void ApplyAnswerSeed(int seed)
    {
        _answerData = MazeAnswerGenerator.Generate(seed); // seed 기반 미로 데이터 생성
        _hasAnswerSeed = true;                            // 시드 적용 완료 표시

        ApplyHintState();                                 // 힌트 화면에 데이터 반영

        Log($"answer seed 적용 완료 | goal={_answerData.GoalCell}");
    }

    /// <summary>
    /// 현재 미로 데이터를 힌트 화면에 반영한다.
    /// 벽 정보와 목표 위치만 표시한다.
    /// </summary>
    private void ApplyHintState()
    {
        if (!_hasAnswerSeed || _answerData == null)
            return; // 시드 미적용 상태면 종료

        if (hintView == null)
            return; // 뷰 참조 없으면 종료

        hintView.ResetToDefault(); // 기존 표시 상태 초기화
        hintView.ApplyMazeWalls(_answerData.HorizontalWalls, _answerData.VerticalWalls); // 벽 데이터 반영
        hintView.MoveGoalTo(_answerData.GoalCell); // 목표 위치 반영
    }

    /// <summary>
    /// 현재 재구성된 정답 데이터를 외부에서 읽을 수 있게 반환한다.
    /// </summary>
    public MazeAnswerGenerator.MazeAnswerData GetAnswerData()
    {
        return _answerData; // 현재 미로 데이터 반환
    }

    /// <summary>
    /// 일반 디버그 로그 출력.
    /// </summary>
    private void Log(string message)
    {
        if (!enableDebugLog)
            return; // 로그 꺼져 있으면 종료

        Debug.Log($"[MazeHint] {message}", this); // 힌트 디버그 로그 출력
    }
}