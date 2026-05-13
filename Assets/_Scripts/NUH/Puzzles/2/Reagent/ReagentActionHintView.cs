using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 2-3 시약 제조 퍼즐의 Action 힌트 화면 표시 담당.
/// 
/// 역할
/// - 행동 힌트 3쌍(Material 아이콘 + 숫자)을 좌->우로 표시한다.
/// - 벽면 힌트이므로 SpriteRenderer 대신 Quad / MeshRenderer / Material 기반으로 표시한다.
/// </summary>
public class ReagentActionHintView : MonoBehaviour
{
    [Header("행동 힌트 아이콘 MeshRenderer")]
    [SerializeField] private List<MeshRenderer> actionHintIconRenderers = new(); // 행동 힌트 아이콘 3칸 MeshRenderer 목록

    [Header("행동 힌트 숫자 TextMeshPro")]
    [SerializeField] private List<TextMeshPro> actionHintNumberTexts = new(); // 행동 힌트 숫자 3칸 TextMeshPro 목록

    [Header("행동 아이콘 Material")]
    [SerializeField] private Material heatMaterial; // 가열 아이콘 Material
    [SerializeField] private Material coolMaterial; // 냉각 아이콘 Material

    [Header("표시 색상")]
    [SerializeField] private Color actionIconColor = Color.white; // 행동 힌트 아이콘 기본 색상
    [SerializeField] private Color actionNumberColor = Color.white; // 행동 힌트 숫자 기본 색상

    [Header("화면 상태 루트")]
    [SerializeField] private GameObject monitorRoot; // Action 힌트 화면 루트

    [Header("디버그")]
    [SerializeField] private bool enableDebugLog = true; // 디버그 로그 출력 여부

    private MaterialPropertyBlock _propertyBlock; // Material 에셋을 직접 수정하지 않고 렌더러별 색상만 바꾸기 위한 블록

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private void Awake()
    {
        EnsurePropertyBlock();
    }

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
            ClearActionIcon(i); // 남는 아이콘 칸 비우기

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

        MeshRenderer iconRenderer = actionHintIconRenderers[index]; // 대상 아이콘 렌더러 참조
        TextMeshPro numberText = actionHintNumberTexts[index]; // 대상 숫자 텍스트 참조

        if (iconRenderer != null)
        {
            Material iconMaterial = GetActionMaterial(actionType);
            ApplyIconMaterial(iconRenderer, iconMaterial, actionIconColor);
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
            ClearActionIcon(i); // 행동 아이콘 비우기

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
    /// 행동 타입에 대응하는 아이콘 Material을 반환한다.
    /// </summary>
    private Material GetActionMaterial(ReagentActionType actionType)
    {
        switch (actionType)
        {
            case ReagentActionType.Heat:
                return heatMaterial;
            case ReagentActionType.Cool:
                return coolMaterial;
            default:
                return null;
        }
    }

    /// <summary>
    /// 특정 아이콘 칸에 Material을 적용하고 표시한다.
    /// </summary>
    private void ApplyIconMaterial(MeshRenderer renderer, Material material, Color color)
    {
        if (renderer == null)
            return;

        if (material == null)
        {
            renderer.enabled = false;
            renderer.sharedMaterial = null;
            return;
        }

        renderer.sharedMaterial = material;
        renderer.enabled = true;

        ApplyRendererColor(renderer, color);
    }

    /// <summary>
    /// 특정 아이콘 칸을 숨긴다.
    /// </summary>
    private void ClearActionIcon(int index)
    {
        if (index < 0 || index >= actionHintIconRenderers.Count)
            return;

        MeshRenderer renderer = actionHintIconRenderers[index];
        if (renderer == null)
            return;

        renderer.enabled = false;
        renderer.sharedMaterial = null;
    }

    /// <summary>
    /// MaterialPropertyBlock으로 렌더러별 색상을 적용한다.
    /// Material 에셋 자체는 변경하지 않는다.
    /// </summary>
    private void ApplyRendererColor(MeshRenderer renderer, Color color)
    {
        if (renderer == null)
            return;

        EnsurePropertyBlock();

        renderer.GetPropertyBlock(_propertyBlock);

        _propertyBlock.SetColor(BaseColorId, color);
        _propertyBlock.SetColor(ColorId, color);

        renderer.SetPropertyBlock(_propertyBlock);
    }

    /// <summary>
    /// MaterialPropertyBlock 인스턴스를 보장한다.
    /// </summary>
    private void EnsurePropertyBlock()
    {
        if (_propertyBlock == null)
            _propertyBlock = new MaterialPropertyBlock();
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