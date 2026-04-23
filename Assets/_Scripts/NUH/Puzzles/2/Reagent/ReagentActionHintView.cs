using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 2-3 시약 제조 퍼즐의 Action 힌트 화면 표시 담당.
/// 
/// 역할
/// - 행동 힌트 3쌍(아이콘 + 숫자)을 좌->우로 표시한다.
/// </summary>
public class ReagentActionHintView : MonoBehaviour
{
    [Header("행동 힌트 아이콘 SpriteRenderer")]
    [SerializeField] private List<SpriteRenderer> actionHintIconRenderers = new(); // 행동 힌트 아이콘 3칸 SpriteRenderer 목록

    [Header("행동 힌트 숫자 TextMeshPro")]
    [SerializeField] private List<TextMeshPro> actionHintNumberTexts = new(); // 행동 힌트 숫자 3칸 TextMeshPro 목록

    [Header("행동 아이콘 스프라이트")]
    [SerializeField] private Sprite heatSprite; // 가열(불 모양) 스프라이트
    [SerializeField] private Sprite coolSprite; // 냉각(눈결정 모양) 스프라이트

    [Header("텍스트 색상")]
    [SerializeField] private Color actionNumberColor = Color.white; // 행동 힌트 숫자 기본 색상

    [Header("화면 상태 루트")]
    [SerializeField] private GameObject monitorRoot; // Action 힌트 화면 루트

    [Header("디버그")]
    [SerializeField] private bool enableDebugLog = true; // 디버그 로그 출력 여부

    /// <summary>
    /// 행동 힌트 3쌍(아이콘 + 숫자)을 좌->우 순서대로 표시한다.
    /// 
    /// 전제
    /// - 입력된 actionSteps는 ProgressIndex 오름차순으로 이미 정렬되어 있다.
    /// </summary>
    public void ApplyActionHints(List<ReagentAnswerGenerator.ReagentActionStep> actionSteps)
    {
        if (actionSteps == null)
            return; // 행동 힌트 데이터가 없으면 종료

        int count = Mathf.Min(
            Mathf.Min(actionHintIconRenderers.Count, actionHintNumberTexts.Count),
            actionSteps.Count); // 실제 반영 가능한 개수 계산

        for (int i = 0; i < count; i++)
            SetActionHint(i, actionSteps[i].ActionType, actionSteps[i].ProgressIndex); // 각 행동 힌트 반영

        for (int i = count; i < actionHintIconRenderers.Count; i++)
        {
            if (actionHintIconRenderers[i] != null)
                actionHintIconRenderers[i].sprite = null; // 남는 아이콘 칸 비우기
        }

        for (int i = count; i < actionHintNumberTexts.Count; i++)
        {
            if (actionHintNumberTexts[i] != null)
                actionHintNumberTexts[i].text = string.Empty; // 남는 숫자 칸 비우기
        }
    }

    /// <summary>
    /// 개별 행동 힌트 1쌍을 표시한다.
    /// </summary>
    public void SetActionHint(int index, ReagentActionType actionType, int progressIndex)
    {
        if (index < 0)
            return;

        if (index >= actionHintIconRenderers.Count)
            return;

        if (index >= actionHintNumberTexts.Count)
            return;

        SpriteRenderer iconRenderer = actionHintIconRenderers[index]; // 대상 아이콘 렌더러 참조
        TextMeshPro numberText = actionHintNumberTexts[index]; // 대상 숫자 텍스트 참조

        if (iconRenderer != null)
        {
            iconRenderer.sprite = GetActionSprite(actionType); // 행동 타입에 맞는 아이콘 스프라이트 적용
            iconRenderer.color = Color.white;
        }

        if (numberText != null)
        {
            numberText.text = progressIndex.ToString(); // 숫자 텍스트 적용
            numberText.color = actionNumberColor;
        }
    }

    /// <summary>
    /// Action 힌트 화면 전체를 기본 상태로 초기화한다.
    /// </summary>
    public void ResetToDefault()
    {
        if (monitorRoot != null)
            monitorRoot.SetActive(true); // 기본 힌트 화면 표시

        for (int i = 0; i < actionHintIconRenderers.Count; i++)
        {
            if (actionHintIconRenderers[i] == null)
                continue;

            actionHintIconRenderers[i].sprite = null; // 행동 아이콘 비우기
            actionHintIconRenderers[i].color = Color.white;
        }

        for (int i = 0; i < actionHintNumberTexts.Count; i++)
        {
            if (actionHintNumberTexts[i] == null)
                continue;

            actionHintNumberTexts[i].text = string.Empty; // 숫자 텍스트 비우기
            actionHintNumberTexts[i].color = actionNumberColor;
        }

        Log("기본 상태로 초기화");
    }

    /// <summary>
    /// 행동 타입에 대응하는 아이콘 스프라이트를 반환한다.
    /// </summary>
    private Sprite GetActionSprite(ReagentActionType actionType)
    {
        switch (actionType)
        {
            case ReagentActionType.Heat:
                return heatSprite;
            case ReagentActionType.Cool:
                return coolSprite;
            default:
                return null;
        }
    }

    /// <summary>
    /// 일반 디버그 로그 출력.
    /// </summary>
    private void Log(string message)
    {
        if (!enableDebugLog)
            return;

        Debug.Log($"[ReagentActionHintView] {message}", this);
    }
}