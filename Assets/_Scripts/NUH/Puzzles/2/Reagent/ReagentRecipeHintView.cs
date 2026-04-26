using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 2-3 시약 제조 퍼즐의 Recipe 힌트 화면 표시 담당.
/// 
/// 역할
/// - 시약 순서 힌트 3개를 좌->우로 표시한다.
/// </summary>
public class ReagentRecipeHintView : MonoBehaviour
{
    [Header("시약 순서 힌트 SpriteRenderer")]
    [SerializeField] private List<SpriteRenderer> recipeHintRenderers = new(); // 시약 순서 힌트 3칸 SpriteRenderer 목록

    [Header("시약 스프라이트 매핑")]
    [SerializeField] private Sprite emptyReagentSprite; // 비어 있는 시약 힌트 스프라이트
    [SerializeField] private Sprite reagentASprite; // 시약 A 스프라이트
    [SerializeField] private Sprite reagentBSprite; // 시약 B 스프라이트
    [SerializeField] private Sprite reagentCSprite; // 시약 C 스프라이트
    [SerializeField] private Sprite reagentDSprite; // 시약 D 스프라이트
    [SerializeField] private Sprite reagentESprite; // 시약 E 스프라이트
    [SerializeField] private Sprite reagentFSprite; // 시약 F 스프라이트

    [Header("화면 상태 루트")]
    [SerializeField] private GameObject monitorRoot; // Recipe 힌트 화면 루트

    [Header("디버그")]
    [SerializeField] private bool enableDebugLog = true; // 디버그 로그 출력 여부

    /// <summary>
    /// 시약 순서 힌트 3개를 좌->우 순서대로 표시한다.
    /// </summary>
    public void ApplyRecipeHint(List<ReagentType> recipeSequence)
    {
        if (recipeSequence == null)
            return; // 시약 순서 데이터가 없으면 종료

        int count = Mathf.Min(recipeHintRenderers.Count, recipeSequence.Count); // 실제 반영 가능한 개수 계산

        for (int i = 0; i < count; i++)
        {
            SpriteRenderer renderer = recipeHintRenderers[i]; // 현재 시약 힌트 렌더러 참조
            if (renderer == null)
                continue;

            renderer.sprite = GetReagentSprite(recipeSequence[i]); // 시약 타입에 맞는 스프라이트 적용
            renderer.color = Color.white; // 기본 흰색 표시
        }

        for (int i = count; i < recipeHintRenderers.Count; i++)
        {
            if (recipeHintRenderers[i] == null)
                continue;

            recipeHintRenderers[i].sprite = emptyReagentSprite; // 남는 칸은 빈 스프라이트
            recipeHintRenderers[i].color = Color.white;
        }
    }

    /// <summary>
    /// Recipe 힌트 화면을 기본 상태로 초기화한다.
    /// </summary>
    public void ResetToDefault()
    {
        if (monitorRoot != null)
            monitorRoot.SetActive(true); // 기본 힌트 화면 표시

        for (int i = 0; i < recipeHintRenderers.Count; i++)
        {
            if (recipeHintRenderers[i] == null)
                continue;

            recipeHintRenderers[i].sprite = emptyReagentSprite; // 시약 힌트 칸 비우기
            recipeHintRenderers[i].color = Color.white;
        }

        Log("기본 상태로 초기화");
    }

    /// <summary>
    /// 시약 타입에 대응하는 스프라이트를 반환한다.
    /// </summary>
    private Sprite GetReagentSprite(ReagentType reagentType)
    {
        switch (reagentType)
        {
            case ReagentType.ReagentA:
                return reagentASprite;
            case ReagentType.ReagentB:
                return reagentBSprite;
            case ReagentType.ReagentC:
                return reagentCSprite;
            case ReagentType.ReagentD:
                return reagentDSprite;
            case ReagentType.ReagentE:
                return reagentESprite;
            case ReagentType.ReagentF:
                return reagentFSprite;
            default:
                return emptyReagentSprite;
        }
    }

    /// <summary>
    /// 일반 디버그 로그 출력.
    /// </summary>
    private void Log(string message)
    {
        if (!enableDebugLog)
            return;

        Debug.Log($"[ReagentRecipeHintView] {message}", this);
    }
}