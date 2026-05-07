using UnityEngine;

/// <summary>
/// URP Render Graph 후처리 엔진과 연동되는 수동 외곽선 제어 스위치.
/// </summary>
public class ItemOutlineController : MonoBehaviour
{
    [Header("렌더러 피처 통신 주파수")]
    [Tooltip("후처리 엔진이 감시하고 있는 레이어 이름")]
    [SerializeField] private string outlineLayerName = "ItemOutline";

    [Header("외곽선을 켤 전구들 (메쉬 직접 할당)")]
    [SerializeField] private Renderer[] targetRenderers;

    private int _outlineLayer;
    private int[] _originalLayers;
    private bool _isHighlighted = false;

    private void Awake()
    {
        _outlineLayer = LayerMask.NameToLayer(outlineLayerName);
        if (_outlineLayer == -1)
            Debug.LogError($"[제미니 시스템 경고] '{outlineLayerName}' 레이어가 에디터에 없습니다!");

        if (targetRenderers == null || targetRenderers.Length == 0)
        {
            Debug.LogWarning($"[{gameObject.name}] 연결된 메쉬가 없습니다! 선을 꽂아주세요.");
            return;
        }

        // 각 부품의 원래 레이어(Default 등) 백업
        _originalLayers = new int[targetRenderers.Length];
        for (int i = 0; i < targetRenderers.Length; i++)
        {
            if (targetRenderers[i] != null)
                _originalLayers[i] = targetRenderers[i].gameObject.layer;
        }
    }

    public void SetOutline(bool state)
    {
        if (_isHighlighted == state || targetRenderers == null) return;
        _isHighlighted = state;

        // 명시된 메쉬들의 레이어를 'ItemOutline'으로 일괄 전환
        for (int i = 0; i < targetRenderers.Length; i++)
        {
            if (targetRenderers[i] != null)
            {
                targetRenderers[i].gameObject.layer = state ? _outlineLayer : _originalLayers[i];
            }
        }
    }
}