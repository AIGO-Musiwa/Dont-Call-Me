using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 2-3 시약 제조 퍼즐의 Recipe 힌트 화면 표시 담당.
/// 
/// 역할
/// - 시약 순서 힌트 3개를 좌->우로 표시한다.
/// - 벽면 힌트이므로 SpriteRenderer 대신 Quad / MeshRenderer / Material 기반으로 표시한다.
/// </summary>
public class ReagentRecipeHintView : MonoBehaviour
{
    [Header("시약 순서 힌트 MeshRenderer")]
    [SerializeField] private List<MeshRenderer> recipeHintRenderers = new(); // 시약 순서 힌트 3칸 MeshRenderer 목록

    [Header("시약 Material 매핑")]
    [SerializeField] private Material emptyReagentMaterial; // 비어 있는 시약 힌트 Material
    [SerializeField] private Material reagentAMaterial; // 시약 A Material
    [SerializeField] private Material reagentBMaterial; // 시약 B Material
    [SerializeField] private Material reagentCMaterial; // 시약 C Material
    [SerializeField] private Material reagentDMaterial; // 시약 D Material
    [SerializeField] private Material reagentEMaterial; // 시약 E Material
    [SerializeField] private Material reagentFMaterial; // 시약 F Material

    [Header("표시 색상")]
    [SerializeField] private Color reagentHintColor = Color.white; // 시약 힌트 기본 색상

    [Header("화면 상태 루트")]
    [SerializeField] private GameObject monitorRoot; // Recipe 힌트 화면 루트

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
    /// 시약 순서 힌트 3개를 좌->우 순서대로 표시한다.
    /// </summary>
    public void ApplyRecipeHint(List<ReagentType> recipeSequence)
    {
        if (recipeSequence == null)
            return; // 시약 순서 데이터가 없으면 종료

        int count = Mathf.Min(recipeHintRenderers.Count, recipeSequence.Count); // 실제 반영 가능한 개수 계산

        for (int i = 0; i < count; i++)
        {
            MeshRenderer renderer = recipeHintRenderers[i]; // 현재 시약 힌트 렌더러 참조
            if (renderer == null)
                continue;

            Material material = GetReagentMaterial(recipeSequence[i]);
            ApplyReagentMaterial(renderer, material, reagentHintColor);
        }

        for (int i = count; i < recipeHintRenderers.Count; i++)
        {
            MeshRenderer renderer = recipeHintRenderers[i];
            if (renderer == null)
                continue;

            ApplyReagentMaterial(renderer, emptyReagentMaterial, reagentHintColor); // 남는 칸은 빈 Material
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
            MeshRenderer renderer = recipeHintRenderers[i];
            if (renderer == null)
                continue;

            ApplyReagentMaterial(renderer, emptyReagentMaterial, reagentHintColor); // 시약 힌트 칸 비우기
        }

        Log("기본 상태로 초기화");
    }

    /// <summary>
    /// 시약 타입에 대응하는 Material을 반환한다.
    /// </summary>
    private Material GetReagentMaterial(ReagentType reagentType)
    {
        switch (reagentType)
        {
            case ReagentType.ReagentA:
                return reagentAMaterial;
            case ReagentType.ReagentB:
                return reagentBMaterial;
            case ReagentType.ReagentC:
                return reagentCMaterial;
            case ReagentType.ReagentD:
                return reagentDMaterial;
            case ReagentType.ReagentE:
                return reagentEMaterial;
            case ReagentType.ReagentF:
                return reagentFMaterial;
            default:
                return emptyReagentMaterial;
        }
    }

    /// <summary>
    /// 특정 시약 힌트 칸에 Material을 적용하고 표시한다.
    /// </summary>
    private void ApplyReagentMaterial(MeshRenderer renderer, Material material, Color color)
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

        Debug.Log($"[ReagentRecipeHintView] {message}", this);
    }
}