using UnityEngine;

/// <summary>
/// 금고 내부 버튼의 시각 표현 담당.
/// 버튼이 눌리면 초록불 상태로 표시한다.
/// </summary>
public class DialInsideButtonView : MonoBehaviour
{
    [Header("표시 대상")]
    [SerializeField] private Renderer targetRenderer;          // 버튼 Renderer
    [SerializeField] private Light greenLight;                 // 선택 사항: 초록불 Light
    [SerializeField] private GameObject greenIndicator;        // 선택 사항: 초록불 오브젝트

    [Header("색상")]
    [SerializeField] private Color normalColor = Color.white;  // 기본 색상
    [SerializeField] private Color pressedColor = Color.green; // 눌림 색상

    private MaterialPropertyBlock _propertyBlock;              // 머티리얼 인스턴스 생성을 피하기 위한 블록

    private void Awake()
    {
        EnsureInitialized();                                   // 내부 캐시 초기화 보장
        SetPressedImmediate(false);                            // 기본 상태 반영
    }

    /// <summary>
    /// 외부에서 Awake보다 먼저 호출되어도 안전하도록 내부 캐시를 보장한다.
    /// </summary>
    private void EnsureInitialized()
    {
        if (_propertyBlock == null)
            _propertyBlock = new MaterialPropertyBlock();       // PropertyBlock 지연 생성
    }

    /// <summary>
    /// 버튼 눌림 상태를 즉시 반영한다.
    /// </summary>
    public void SetPressedImmediate(bool pressed)
    {
        EnsureInitialized();                                   // 외부 선호출 대비 초기화 보장

        ApplyRendererColor(pressed ? pressedColor : normalColor);

        if (greenLight != null)
            greenLight.enabled = pressed;

        if (greenIndicator != null)
            greenIndicator.SetActive(pressed);
    }

    /// <summary>
    /// Renderer 색상을 MaterialPropertyBlock으로 반영한다.
    /// </summary>
    private void ApplyRendererColor(Color color)
    {
        if (targetRenderer == null)
            return;

        EnsureInitialized();                                   // PropertyBlock null 방지

        targetRenderer.GetPropertyBlock(_propertyBlock);
        _propertyBlock.SetColor("_BaseColor", color);
        _propertyBlock.SetColor("_Color", color);
        targetRenderer.SetPropertyBlock(_propertyBlock);
    }
}