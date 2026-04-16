using UnityEngine;

/// <summary>
/// 문양 스프라이트 표시 전용 스크립트
/// 레버 옆 문양, 힌트 순서표 문양 모두 공용으로 사용
/// </summary>
public class SymbolSpriteDisplay : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private SpriteRenderer targetRenderer;     // 문양을 실제로 표시할  SR

    /// <summary>
    /// 외부에서 문양 스프라이트를 설정한다.
    /// </summary>
    public void SetSprite(Sprite sprite)
    {
        if (targetRenderer == null)
            return;

        targetRenderer.sprite = sprite;
        targetRenderer.enabled = sprite != null;
    }

    /// <summary>
    /// 표시를 비운다
    /// </summary>
    public void Clear()
    {
        if (targetRenderer == null)
            return;

        targetRenderer.sprite = null;
        targetRenderer.enabled = false;
    }
}
