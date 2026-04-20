using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 전선 연결 퍼즐의 시각 표현 전담
/// - 좌우 소켓 색 표시
/// - 선택된 좌측 하이라이트
/// - 연결선 표시 / 숨김 및 위치 반영
/// </summary>
public class WireConnectionView : MonoBehaviour
{
    [Header("좌측 소켓 렌더러")]
    [SerializeField] private List<Renderer> leftSocketRenderers = new();

    [Header("우측 소켓 렌더러")]
    [SerializeField] private List<Renderer> rightSocketRenderers = new();

    [Header("좌측 소켓 선택 하이라이트")]
    [SerializeField] private List<GameObject> leftSelectionHighlights = new();

    [Header("좌측 소켓 위치")]
    [SerializeField] private List<Transform> leftSocketPoints = new();

    [Header("우측 소켓 위치")]
    [SerializeField] private List<Transform> rightSocketPoints = new();

    [Header("연걸선 비주얼 (left 기준 1개씩")]
    [SerializeField] private List<Transform> connectionLineVisuals = new();

    [Header("연결선 기본 축 설정")]
    [SerializeField] private Vector3 cylinderAxis = Vector3.up;

    [Header("연결선 두께")]
    [SerializeField] private float lineThickness = 0.03f;

    [Header("색상 머터리얼 프로퍼티")]
    [SerializeField] private string colorPropertyName = "_BaseColor";

    private MaterialPropertyBlock _mpb;

    private void Awake()
    {
        _mpb = new MaterialPropertyBlock();
        HideAllLines();
        ClearAllSelections();
    }

    /// <summary>
    /// 좌우 소켓 색 표시 반영
    /// </summary>
    public void ApplySocketColors(IReadOnlyList<WireSocketColor> leftColors, IReadOnlyList<WireSocketColor> rightColors)
    {
        ApplyColorsToRenderers(leftSocketRenderers, leftColors);
        ApplyColorsToRenderers(rightSocketRenderers, rightColors);
    }

    /// <summary>
    /// 현재 선택된 좌측 소켓 표시 반영
    /// </summary>
    public void ApplySelection(int selectedLeftIndex, bool hasSelection)
    {
        for (int i = 0; i < leftSelectionHighlights.Count; i++)
        {
            if (leftSelectionHighlights[i] == null)
                continue;

            bool active = hasSelection && i == selectedLeftIndex;
            leftSelectionHighlights[i].SetActive(active);
        }
    }



    /// <summary>
    /// 현재 연결 상태를 연결선 비주얼에 반영
    /// </summary>
    public void ApplyConnections(IReadOnlyList<int> connectedRightIndexByLeft)
    {
        int count = Mathf.Min(connectionLineVisuals.Count, connectedRightIndexByLeft.Count);

        for (int leftIndex = 0; leftIndex < count; leftIndex++)
        {
            Transform line = connectionLineVisuals[leftIndex];
            if (line == null)
                continue;

            int rightIndex = connectedRightIndexByLeft[leftIndex];
            if (rightIndex < 0 || rightIndex >= rightSocketPoints.Count)
            {
                line.gameObject.SetActive(false);
                continue;
            }

            if (leftIndex >= leftSocketPoints.Count || leftSocketPoints[leftIndex] == null || rightSocketPoints[rightIndex] == null)
            {
                line.gameObject.SetActive(false);
                continue;
            }

            Transform leftPoint = leftSocketPoints[leftIndex];
            Transform rightPoint = rightSocketPoints[rightIndex];

            Vector3 start = leftPoint.position;
            Vector3 end = rightPoint.position;
            Vector3 dir = end - start;
            float distance = dir.magnitude;

            if (distance <= 0.0001f)
            {
                line.gameObject.SetActive(false);
                continue;
            }

            line.gameObject.SetActive(true);
            line.position = (start + end) * 0.5f;
            line.rotation = Quaternion.FromToRotation(cylinderAxis, dir.normalized);

            Vector3 scale = line.localScale;
            scale.x = lineThickness;
            scale.z = lineThickness;
            scale.y = distance * 0.5f;
            line.localScale = scale;
        }
    }



    /// <summary>
    /// 모든 선택 하이라이트 OFF
    /// </summary>
    public void ClearAllSelections()
    {
        ApplySelection(-1, false);
    }

    /// <summary>
    /// 모든 연결선 숨김
    /// </summary>
    public void HideAllLines()
    {
        for(int i = 0; i < connectionLineVisuals.Count; i++)
        {
            if (connectionLineVisuals[i] == null)
                continue;

            connectionLineVisuals[i].gameObject.SetActive(false);
        }
    }

    private void ApplyColorsToRenderers(List<Renderer> renderers, IReadOnlyList<WireSocketColor> colors)
    {
        int count = Mathf.Min(renderers.Count, colors.Count);

        for(int i = 0; i < count; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
                continue;

            renderer.GetPropertyBlock(_mpb);
            _mpb.SetColor(colorPropertyName, ToUnityColor(colors[i]));
            renderer.SetPropertyBlock(_mpb);
        }
    }

    private Color ToUnityColor(WireSocketColor socketColor)
    {
        return socketColor switch
        {
            WireSocketColor.Red => Color.red,
            WireSocketColor.Orange => Color.orange,
            WireSocketColor.Yellow => Color.yellow,
            WireSocketColor.Green => Color.green,
            WireSocketColor.Blue => Color.blue,
            WireSocketColor.Navy => Color.navyBlue,
            WireSocketColor.Purple => Color.purple,
            WireSocketColor.White => Color.white,
            WireSocketColor.Black => Color.black,
            _ => Color.white
        };
    }
}
