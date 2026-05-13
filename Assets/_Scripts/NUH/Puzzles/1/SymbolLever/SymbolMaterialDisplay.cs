using UnityEngine;

/// <summary>
/// Quad / MeshRenderer 기반 문양 표시 담당.
/// 문양 Material을 적용하고, MaterialPropertyBlock으로 색상을 개별 제어한다.
/// </summary>
[RequireComponent(typeof(MeshRenderer))]
public class SymbolMaterialDisplay : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private MeshRenderer targetRenderer; // 문양을 표시할 MeshRenderer

    [Header("표시 옵션")]
    [SerializeField] private bool hideOnAwake = true;     // 시작 시 문양을 숨길지 여부

    private MaterialPropertyBlock _propertyBlock;         // Material 인스턴스 생성을 막고 색상만 개별 적용하기 위한 블록
    private Material _currentMaterial;                    // 현재 적용된 문양 Material
    private Color _currentColor = Color.white;            // 현재 표시 색상

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private void Awake()
    {
        ResolveReferences();

        if (_propertyBlock == null)
            _propertyBlock = new MaterialPropertyBlock();

        if (hideOnAwake)
            Clear();
        else
            ApplyColorProperty();
    }

    private void Reset()
    {
        ResolveReferences();
    }

    /// <summary>
    /// 필요한 참조를 자동으로 찾는다.
    /// </summary>
    private void ResolveReferences()
    {
        if (targetRenderer == null)
            targetRenderer = GetComponent<MeshRenderer>();
    }

    /// <summary>
    /// 문양 Material을 적용하고 표시를 켠다.
    /// </summary>
    public void SetMaterial(Material material)
    {
        ResolveReferences();

        _currentMaterial = material;

        if (targetRenderer == null)
            return;

        if (_currentMaterial == null)
        {
            Clear();
            return;
        }

        targetRenderer.sharedMaterial = _currentMaterial;
        targetRenderer.enabled = true;

        ApplyColorProperty();
    }

    /// <summary>
    /// 현재 문양의 색상을 변경한다.
    /// Material 에셋을 직접 수정하지 않고 이 Renderer에만 색상을 적용한다.
    /// </summary>
    public void SetColor(Color color)
    {
        _currentColor = color;
        ApplyColorProperty();
    }

    /// <summary>
    /// 문양 표시를 끈다.
    /// </summary>
    public void Clear()
    {
        ResolveReferences();

        _currentMaterial = null;

        if (targetRenderer == null)
            return;

        targetRenderer.enabled = false;
    }

    /// <summary>
    /// 현재 색상을 MaterialPropertyBlock으로 적용한다.
    /// URP/Lit 계열의 _BaseColor와 일부 셰이더 호환용 _Color를 함께 설정한다.
    /// </summary>
    private void ApplyColorProperty()
    {
        ResolveReferences();

        if (targetRenderer == null)
            return;

        if (_propertyBlock == null)
            _propertyBlock = new MaterialPropertyBlock();

        targetRenderer.GetPropertyBlock(_propertyBlock);

        _propertyBlock.SetColor(BaseColorId, _currentColor);
        _propertyBlock.SetColor(ColorId, _currentColor);

        targetRenderer.SetPropertyBlock(_propertyBlock);
    }
}