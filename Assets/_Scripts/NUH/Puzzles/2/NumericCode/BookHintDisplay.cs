using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 숫자 입력 퍼즐 책 힌트 월드 표시 담당
/// 
/// 역할
/// - 미리 배치된 20개의 책에
/// seed 결과에 맞는 색 매터리얼 적용
/// </summary>
public class BookHintDisplay : MonoBehaviour
{
    [Header("책 렌더러")]
    [SerializeField] private List<Renderer> bookRenderers = new();      //고정 배치된 책 렌더러 20개

    [Header("색 Material")]
    [SerializeField] private Material redMaterial;
    [SerializeField] private Material greenMaterial;
    [SerializeField] private Material blueMaterial;
    [SerializeField] private Material yellowMaterial;
    [SerializeField] private Material defaultMaterial;                  // 책 갯수 늘릴 경우 사용할 기본 매터리얼

    [SerializeField] private bool enableDebugLog = true;

    /// <summary>
    /// Generator가 만든 20권 책 색 배치를 실제 책 렌더러들에 반영
    /// 순서는 bookRenderers 인스펙터 순서 따르기
    /// </summary>
    public void SetBookColors(IReadOnlyList<NumericBookColor> placement)
    {
        if (placement == null)
        {
            LogWarning("placement가 null | 책 색 적용 건너뛰기");
            return;
        }

        int count = Mathf.Min(bookRenderers.Count, placement.Count);

        // 실제 배치 가능한 범위만 색을 적용
        for(int i = 0; i < count; i++)
        {
            Renderer targetRenderer = bookRenderers[i];
            if (targetRenderer == null)
                continue;

            Material targetMaterial = GetMaterialByColor(placement[i]);
            ApplyMaterial(targetRenderer, targetMaterial);
        }

        //placement보다 renderer가 더 많으면 남는 책은 기본 매터리얼로 되돌리기
        for(int i = count; i < bookRenderers.Count; i++)
        {
            Renderer targetRenderer = bookRenderers[i];
            if (targetRenderer == null)
                continue;

            ApplyMaterial(targetRenderer, defaultMaterial);
        }

        Log($"책 색 배치 적용 완료 | renderers={bookRenderers.Count} | placement={placement.Count}");
    }

    /// <summary>
    /// 책 힌트 초기화
    /// 기본 매터리얼로 적용
    /// </summary>
    public void ResetToDefault()
    {
        for (int i = 0; i < bookRenderers.Count; i++)
        {
            Renderer targetRenderer = bookRenderers[i];
            if (targetRenderer == null)
                continue;

            ApplyMaterial(targetRenderer, defaultMaterial);
        }

        Log("책 힌트 초기화");
    }


    /// <summary>
    /// 책 색 enum에 맞는 매터리얼 반환
    /// </summary>
    private Material GetMaterialByColor(NumericBookColor color)
    {
        return color switch
        {
            NumericBookColor.Red => redMaterial,
            NumericBookColor.Green => greenMaterial,
            NumericBookColor.Blue => blueMaterial,
            NumericBookColor.Yellow => yellowMaterial,
            _ => defaultMaterial
        };
    }

    /// <summary>
    /// 실제 렌더러에 매터리얼 적용
    /// </summary>
    private void ApplyMaterial(Renderer targetRenderer, Material material)
    {
        if (targetRenderer == null)
            return;

        if (material == null)
            return;

        targetRenderer.material = material;
    }


    /// <summary>
    /// 일반 디버그 로그 출력.
    /// </summary>
    private void Log(string message)
    {
        if (!enableDebugLog)
            return;

        Debug.Log($"[BookHintDisplay] {message}", this);
    }

    /// <summary>
    /// 경고 로그 출력.
    /// </summary>
    private void LogWarning(string message)
    {
        if (!enableDebugLog)
            return;

        Debug.LogWarning($"[BookHintDisplay] {message}", this);
    }
}
