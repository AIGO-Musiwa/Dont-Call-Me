using UnityEngine;

/// <summary>
/// 2-3 시약 제조 퍼즐의 Action 힌트 프리팹 루트 로직.
/// 
/// 역할
/// - answer seed를 받아 동일한 정답 데이터를 재구성한다.
/// - 행동 힌트 3쌍(아이콘 + 숫자)을 Action 힌트 뷰에 전달한다.
/// </summary>
public class ReagentActionHint : MonoBehaviour, IPuzzleSeedReceiver
{
    [Header("참조")]
    [SerializeField] private ReagentActionHintView hintView; // Action 힌트 화면 표시 담당 뷰

    [Header("디버그")]
    [SerializeField] private bool enableDebugLog = true; // 디버그 로그 출력 여부

    private ReagentAnswerGenerator.ReagentAnswerData _answerData; // seed 기반으로 재구성한 정답 데이터
    private bool _hasAnswerSeed; // answer seed 적용 완료 여부

    /// <summary>
    /// answer seed를 받아 Action 힌트 데이터를 재구성한다.
    /// </summary>
    public void ApplyAnswerSeed(int seed)
    {
        _answerData = ReagentAnswerGenerator.Generate(seed); // seed 기반 정답 데이터 생성
        _hasAnswerSeed = true; // 시드 적용 완료 표시

        ApplyHintState(); // Action 힌트 화면에 데이터 반영

        Log($"answer seed 적용 완료 | actionCount={_answerData.ActionSteps.Count}");
    }

    /// <summary>
    /// 현재 재구성한 Action 힌트 데이터를 화면에 반영한다.
    /// </summary>
    private void ApplyHintState()
    {
        if (!_hasAnswerSeed || _answerData == null)
            return; // 시드 미적용 상태면 종료

        if (hintView == null)
            return; // 뷰 참조 없으면 종료

        hintView.ResetToDefault(); // 기존 상태 초기화
        hintView.ApplyActionHints(_answerData.ActionSteps); // 행동 힌트 3쌍 반영
    }

    /// <summary>
    /// 현재 재구성된 정답 데이터를 외부에서 읽을 수 있게 반환한다.
    /// </summary>
    public ReagentAnswerGenerator.ReagentAnswerData GetAnswerData()
    {
        return _answerData; // 현재 정답 데이터 반환
    }

    /// <summary>
    /// 일반 디버그 로그 출력.
    /// </summary>
    private void Log(string message)
    {
        if (!enableDebugLog)
            return;

        Debug.Log($"[ReagentActionHint] {message}", this);
    }
}