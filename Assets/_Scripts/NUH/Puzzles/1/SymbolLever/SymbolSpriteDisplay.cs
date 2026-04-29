using UnityEngine;

/// <summary>
/// 문양 스프라이트 표시 전용 스크립트.
/// 레버 옆 문양, 힌트 순서표 문양 모두 공용으로 사용한다.
/// </summary>
public class SymbolSpriteDisplay : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private SpriteRenderer targetRenderer;       // 문양을 실제로 표시할 SpriteRenderer

    [Header("색상")]
    [SerializeField] private Color defaultColor = Color.white;    // 기본 문양 색상

    /// <summary>
    /// 외부에서 문양 스프라이트를 설정한다.
    /// </summary>
    public void SetSprite(Sprite sprite)
    {
        if (targetRenderer == null)
            return;

        targetRenderer.sprite = sprite;
        targetRenderer.enabled = sprite != null;
        targetRenderer.color = defaultColor;
    }

    /// <summary>
    /// 문양 색상을 변경한다.
    /// </summary>
    public void SetColor(Color color)
    {
        if (targetRenderer == null)
            return;

        targetRenderer.color = color;
    }

    /// <summary>
    /// 문양 색상을 기본색으로 되돌린다.
    /// </summary>
    public void ResetColor()
    {
        SetColor(defaultColor);
    }

    /// <summary>
    /// 표시를 비운다.
    /// </summary>
    public void Clear()
    {
        if (targetRenderer == null)
            return;

        targetRenderer.sprite = null;
        targetRenderer.enabled = false;
        targetRenderer.color = defaultColor;
    }
}