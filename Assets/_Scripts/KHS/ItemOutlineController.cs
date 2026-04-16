using UnityEngine;

/// <summary>
/// URP 렌더러 피처(Render Objects)를 활용한 외곽선 제어 모듈.
/// 시각 부품(자식 메쉬)의 Layer를 스위칭하여 메인 렌더러의 도색 공정을 유도한다.
/// </summary>
public class ItemOutlineController : MonoBehaviour
{
    [Header("렌더러 피처 레이어 설정")]
    [Tooltip("외곽선을 덧칠할 전용 주파수(Layer) 이름")]
    [SerializeField] private string outlineLayerName = "ItemOutline";

    private int _outlineLayer;
    private int _originalLayer;
    private Renderer[] _renderers;
    private bool _isHighlighted = false;

    private void Awake()
    {
        // 1. 외곽선 전용 레이어(Layer) 주파수 번호 획득
        _outlineLayer = LayerMask.NameToLayer(outlineLayerName);
        if (_outlineLayer == -1)
        {
            Debug.LogError($"[제미니 시스템 경고] '{outlineLayerName}' 레이어가 존재하지 않아! 에디터에서 꼭 추가해줘.");
        }

        // 2. 자식 시각 부품들의 렌더러 스캔 및 원래 레이어(Default 등) 기억
        // 부모의 물리 센서(Collider) 레이어는 건드리지 않음
        _renderers = GetComponentsInChildren<Renderer>();
        if (_renderers.Length > 0)
        {
            _originalLayer = _renderers[0].gameObject.layer;
        }
    }

    /// <summary>
    /// 외부(PlayerController)에서 호출하여 외곽선 스위치를 켜거나 끈다.
    /// </summary>
    /// <param name="state">활성화 여부</param>
    public void SetOutline(bool state)
    {
        // 동일한 상태라면 스위칭 연산 방지 (전력 절약)
        if (_isHighlighted == state) return;

        _isHighlighted = state;

        // 목표 상태에 따라 스위칭할 레이어 결정
        int targetLayer = state ? _outlineLayer : _originalLayer;

        // 모든 시각 부품의 레이어를 일괄 전환하여 URP 카메라 레이더망에 노출
        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] != null)
            {
                _renderers[i].gameObject.layer = targetLayer;
            }
        }
    }
}