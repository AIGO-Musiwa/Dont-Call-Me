using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 전선 연결 퍼즐의 시각 표현 전담.
/// - 좌우 소켓 색 표시
/// - 선택된 좌측 하이라이트
/// - 연결선 표시 / 숨김 및 위치 반영
/// - 연결선 색상 검은색 적용
/// - Confirm 레버 애니메이션
/// - Confirm 판정 Emission 표시
/// </summary>
public class WireConnectionView : MonoBehaviour
{
    [Header("좌측 소켓 렌더러")]
    [SerializeField] private List<Renderer> leftSocketRenderers = new(); // 좌측 소켓 색 표시 대상

    [Header("우측 소켓 렌더러")]
    [SerializeField] private List<Renderer> rightSocketRenderers = new(); // 우측 소켓 색 표시 대상

    [Header("좌측 소켓 선택 하이라이트")]
    [SerializeField] private List<GameObject> leftSelectionHighlights = new(); // 좌측 선택 표시 오브젝트

    [Header("좌측 소켓 위치")]
    [SerializeField] private List<Transform> leftSocketPoints = new(); // 연결선 시작점

    [Header("우측 소켓 위치")]
    [SerializeField] private List<Transform> rightSocketPoints = new(); // 연결선 끝점

    [Header("연결선 비주얼 left 기준 1개씩")]
    [SerializeField] private List<Transform> connectionLineVisuals = new(); // 연결선 비주얼

    [Header("연결선 기본 축 설정")]
    [SerializeField] private Vector3 cylinderAxis = Vector3.up; // 연결선 모델의 길이 방향 축

    [Header("연결선 두께")]
    [SerializeField] private float lineThickness = 0.1f; // 연결선 두께

    [Header("연결선 색상")]
    [SerializeField] private Color connectionLineColor = Color.black; // 실제 연결선 표시 색상
    [SerializeField] private string connectionLineColorPropertyName = "_BaseColor"; // 연결선 머티리얼 색상 프로퍼티

    [Header("색상 머터리얼 프로퍼티")]
    [SerializeField] private string colorPropertyName = "_BaseColor"; // 소켓 색상 프로퍼티

    [Header("Confirm 레버")]
    [SerializeField] private WireJudgeLeverView judgeLeverView; // 전선 퍼즐 Confirm 레버 전용 View

    [Header("Confirm 판정 Emission")]
    [SerializeField] private Renderer judgeIndicatorRenderer; // Emission을 바꿀 Confirm 표시 Renderer
    [SerializeField] private string emissionColorPropertyName = "_EmissionColor"; // URP/Lit Emission 색상 프로퍼티
    [SerializeField] private Color judgeNormalEmissionColor = Color.white; // 기본 상태 Emission 색상
    [SerializeField] private Color judgeFailEmissionColor = Color.red; // 실패 상태 Emission 색상
    [SerializeField] private Color judgeSolvedEmissionColor = Color.green; // 성공 상태 Emission 색상
    [SerializeField] private float judgeEmissionIntensity = 1f; // Emission 강도 배율

    [Header("Confirm 실패 깜빡임")]
    [SerializeField] private int judgeFailBlinkCount = 3; // 실패 깜빡임 횟수
    [SerializeField] private float judgeFailBlinkOnTime = 0.15f; // 빨간색 유지 시간
    [SerializeField] private float judgeFailBlinkOffTime = 0.15f; // 검정색 유지 시간

    private MaterialPropertyBlock _mpb; // 소켓 색상용 MPB
    private MaterialPropertyBlock _lineMpb; // 연결선 색상용 MPB
    private MaterialPropertyBlock _judgeMpb; // Confirm Emission용 MPB

    private Coroutine _judgeFailRoutine; // 실패 깜빡임 코루틴

    /// <summary>
    /// 실패 깜빡임 전체 소요 시간.
    /// Puzzle 쪽에서 초기화 지연 시간으로 사용한다.
    /// </summary>
    public float JudgeFailEffectSeconds =>
        (judgeFailBlinkOnTime + judgeFailBlinkOffTime) * judgeFailBlinkCount;

    private void Awake()
    {
        _mpb = new MaterialPropertyBlock();
        _lineMpb = new MaterialPropertyBlock();
        _judgeMpb = new MaterialPropertyBlock();

        HideAllLines();
        ClearAllSelections();

        SetJudgeNormalImmediate();
        ResetJudgeLeverImmediate();
    }

    /// <summary>
    /// 좌우 소켓 색 표시를 반영한다.
    /// </summary>
    public void ApplySocketColors(IReadOnlyList<WireSocketColor> leftColors, IReadOnlyList<WireSocketColor> rightColors)
    {
        ApplyColorsToRenderers(leftSocketRenderers, leftColors);
        ApplyColorsToRenderers(rightSocketRenderers, rightColors);
    }

    /// <summary>
    /// 현재 선택된 좌측 소켓 표시를 반영한다.
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
    /// 현재 연결 상태를 연결선 비주얼에 반영한다.
    /// 연결선은 활성화될 때 검은색으로 보정한다.
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

            if (leftIndex >= leftSocketPoints.Count ||
                leftSocketPoints[leftIndex] == null ||
                rightSocketPoints[rightIndex] == null)
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

            // 연결선은 실제 연결이 생성된 상태이므로 검은색으로 표시한다.
            ApplyConnectionLineColor(line);

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
    /// 모든 선택 하이라이트를 끈다.
    /// </summary>
    public void ClearAllSelections()
    {
        ApplySelection(-1, false);
    }

    /// <summary>
    /// 모든 연결선을 숨긴다.
    /// </summary>
    public void HideAllLines()
    {
        for (int i = 0; i < connectionLineVisuals.Count; i++)
        {
            if (connectionLineVisuals[i] == null)
                continue;

            connectionLineVisuals[i].gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Confirm 레버를 당기는 애니메이션을 재생한다.
    /// </summary>
    public void PlayJudgeLeverPulled()
    {
        if (judgeLeverView == null)
            return;

        judgeLeverView.PlayPull();
    }

    /// <summary>
    /// Confirm 레버를 위 상태로 되돌린다.
    /// 실패 후 복귀용이다.
    /// </summary>
    public void ResetJudgeLever()
    {
        if (judgeLeverView == null)
            return;

        judgeLeverView.PlayReset();
    }

    /// <summary>
    /// Confirm 레버를 즉시 기본 상태로 되돌린다.
    /// 초기화용이다.
    /// </summary>
    public void ResetJudgeLeverImmediate()
    {
        if (judgeLeverView == null)
            return;

        judgeLeverView.SetResetImmediate();
    }

    /// <summary>
    /// Confirm 레버를 즉시 당긴 상태로 둔다.
    /// 성공 상태 복원용이다.
    /// </summary>
    public void SetJudgeLeverPulledImmediate()
    {
        if (judgeLeverView == null)
            return;

        judgeLeverView.SetPulledImmediate();
    }

    /// <summary>
    /// Confirm 표시를 기본 흰색으로 즉시 설정한다.
    /// </summary>
    public void SetJudgeNormalImmediate()
    {
        StopJudgeFailRoutineIfRunning();
        ApplyJudgeEmission(judgeNormalEmissionColor);
    }

    /// <summary>
    /// Confirm 표시를 기본 흰색으로 설정한다.
    /// </summary>
    public void SetJudgeNormal()
    {
        StopJudgeFailRoutineIfRunning();
        ApplyJudgeEmission(judgeNormalEmissionColor);
    }

    /// <summary>
    /// Confirm 표시를 성공 초록색으로 설정한다.
    /// </summary>
    public void SetJudgeSolved()
    {
        StopJudgeFailRoutineIfRunning();
        ApplyJudgeEmission(judgeSolvedEmissionColor);
    }

    /// <summary>
    /// Confirm 표시 실패 깜빡임을 재생한다.
    /// 끝나면 기본 흰색으로 복귀하고 Confirm 레버를 위로 올린다.
    /// </summary>
    public void PlayJudgeFailBlink()
    {
        StopJudgeFailRoutineIfRunning();
        _judgeFailRoutine = StartCoroutine(CoJudgeFailBlink());
    }

    /// <summary>
    /// 실패 시 빨간색과 검정색을 반복한다.
    /// </summary>
    private IEnumerator CoJudgeFailBlink()
    {
        for (int i = 0; i < judgeFailBlinkCount; i++)
        {
            ApplyJudgeEmission(judgeFailEmissionColor);
            yield return new WaitForSeconds(judgeFailBlinkOnTime);

            ApplyJudgeEmission(Color.black);
            yield return new WaitForSeconds(judgeFailBlinkOffTime);
        }

        ApplyJudgeEmission(judgeNormalEmissionColor);
        ResetJudgeLever();

        _judgeFailRoutine = null;
    }

    /// <summary>
    /// 연결선 Renderer에 검은색을 적용한다.
    /// 연결선 머티리얼이 이미 검은색이어도, 런타임에서 한 번 더 보정한다.
    /// </summary>
    private void ApplyConnectionLineColor(Transform line)
    {
        if (line == null)
            return;

        Renderer lineRenderer = line.GetComponent<Renderer>();

        if (lineRenderer == null)
            lineRenderer = line.GetComponentInChildren<Renderer>();

        if (lineRenderer == null)
            return;

        if (_lineMpb == null)
            _lineMpb = new MaterialPropertyBlock();

        lineRenderer.GetPropertyBlock(_lineMpb);
        _lineMpb.SetColor(connectionLineColorPropertyName, connectionLineColor);
        lineRenderer.SetPropertyBlock(_lineMpb);
    }

    /// <summary>
    /// Confirm Emission 색상을 반영한다.
    /// Material의 Emission은 미리 켜두고, 여기서는 Black ↔ Color만 바꾼다.
    /// </summary>
    private void ApplyJudgeEmission(Color color)
    {
        if (judgeIndicatorRenderer == null)
            return;

        if (_judgeMpb == null)
            _judgeMpb = new MaterialPropertyBlock();

        Color finalColor = color * judgeEmissionIntensity;

        judgeIndicatorRenderer.GetPropertyBlock(_judgeMpb);
        _judgeMpb.SetColor(emissionColorPropertyName, finalColor);
        judgeIndicatorRenderer.SetPropertyBlock(_judgeMpb);
    }

    /// <summary>
    /// 실패 깜빡임 코루틴을 정리한다.
    /// </summary>
    private void StopJudgeFailRoutineIfRunning()
    {
        if (_judgeFailRoutine == null)
            return;

        StopCoroutine(_judgeFailRoutine);
        _judgeFailRoutine = null;
    }

    /// <summary>
    /// 소켓 Renderer 목록에 색상 목록을 반영한다.
    /// </summary>
    private void ApplyColorsToRenderers(List<Renderer> renderers, IReadOnlyList<WireSocketColor> colors)
    {
        int count = Mathf.Min(renderers.Count, colors.Count);

        for (int i = 0; i < count; i++)
        {
            Renderer renderer = renderers[i];

            if (renderer == null)
                continue;

            if (_mpb == null)
                _mpb = new MaterialPropertyBlock();

            renderer.GetPropertyBlock(_mpb);
            _mpb.SetColor(colorPropertyName, ToUnityColor(colors[i]));
            renderer.SetPropertyBlock(_mpb);
        }
    }

    /// <summary>
    /// WireSocketColor 값을 Unity Color로 변환한다.
    /// </summary>
    private Color ToUnityColor(WireSocketColor socketColor)
    {
        return socketColor switch
        {
            WireSocketColor.Red => Color.red,
            WireSocketColor.DarkOrange => new Color(1f, 0.5f, 0f),
            WireSocketColor.Yellow => Color.yellow,
            WireSocketColor.Green => Color.green,
            WireSocketColor.Blue => Color.blue,
            WireSocketColor.DarkGray => new Color(0.25f, 0.25f, 0.25f),
            WireSocketColor.Purple => Color.purple,
            WireSocketColor.White => Color.white,
            WireSocketColor.Black => Color.black,
            _ => Color.white
        };
    }
}