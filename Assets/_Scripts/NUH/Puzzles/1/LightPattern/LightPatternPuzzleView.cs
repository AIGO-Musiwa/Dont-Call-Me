using System.Collections;
using UnityEngine;

/// <summary>
/// 전구 패턴 퍼즐의 시각 표현 통합 View.
/// - HintBulb: 힌트 전구, PointLight + Emission On/Off
/// - PuzzleLamp: 퍼즐 상태 램프, Emission 색상만 변경
/// - PanelPad: 입력 패드, 클릭 순간 Emission On/Off
/// </summary>
public class LightPatternPuzzleView : MonoBehaviour
{
    [Header("역할")]
    [SerializeField] private ViewRole viewRole = ViewRole.PanelPad; // 이 View의 사용 목적

    [Header("Emission 대상")]
    [SerializeField] private Renderer targetRenderer;               // Emission을 적용할 Renderer
    [SerializeField] private string emissionColorProperty = "_EmissionColor"; // Emission 색상 프로퍼티 이름
    [SerializeField] private float emissionIntensity = 1f;           // Emission 강도 배율

    [Header("선택적 PointLight")]
    [SerializeField] private Light pointLight;                       // 힌트 전구용 PointLight
    [SerializeField] private bool usePointLight = false;             // PointLight 사용 여부

    [Header("힌트 / 패드 Emission 색")]
    [SerializeField] private Color activeEmissionColor = Color.yellow; // 힌트/패드가 켜질 때 사용할 색

    [Header("퍼즐 램프 Emission 색")]
    [SerializeField] private Color normalEmissionColor = Color.white; // 기본/진행 중 색
    [SerializeField] private Color failedEmissionColor = Color.red;    // 실패 색
    [SerializeField] private Color solvedEmissionColor = Color.green;  // 클리어 색

    [Header("패드 입력 연출")]
    [SerializeField] private float panelFlashSeconds = 0.2f;          // 패드 Emission 유지 시간

    [Header("Emission Keyword")]
    [SerializeField] private bool enableEmissionKeywordOnAwake = true; // Material Emission 키워드 활성화 시도

    private MaterialPropertyBlock _propertyBlock;                    // Renderer별 Emission 값 덮어쓰기용
    private Coroutine _flashRoutine;                                 // 패드 점등 코루틴

    private void Awake()
    {
        EnsureInitialized();

        if (enableEmissionKeywordOnAwake)
            TryEnableEmissionKeyword();

        ApplyInitialState();
    }

    /// <summary>
    /// View 역할에 맞는 초기 상태를 적용한다.
    /// </summary>
    private void ApplyInitialState()
    {
        if (viewRole == ViewRole.PuzzleLamp)
        {
            SetPuzzleLampNormal();
            return;
        }

        SetActiveEmission(false, activeEmissionColor);
    }

    /// <summary>
    /// 내부 캐시가 외부 선호출에도 안전하도록 보장한다.
    /// </summary>
    private void EnsureInitialized()
    {
        if (_propertyBlock == null)
            _propertyBlock = new MaterialPropertyBlock();
    }

    /// <summary>
    /// URP/Lit 등에서 Emission이 보이도록 Material keyword를 활성화한다.
    /// </summary>
    private void TryEnableEmissionKeyword()
    {
        if (targetRenderer == null)
            return;

        Material[] materials = targetRenderer.sharedMaterials;

        for (int i = 0; i < materials.Length; i++)
        {
            if (materials[i] == null)
                continue;

            materials[i].EnableKeyword("_EMISSION");
        }
    }

    /// <summary>
    /// 힌트 전구를 켜거나 끈다.
    /// HintBulb 역할에서 주로 사용한다.
    /// </summary>
    public void SetHintActive(bool active)
    {
        SetActiveEmission(active, activeEmissionColor);
    }

    /// <summary>
    /// 패드 입력 시 Emission을 잠깐 켰다가 끈다.
    /// PanelPad 역할에서 주로 사용한다.
    /// </summary>
    public void PlayPanelInputFlash()
    {
        StopFlashRoutineIfRunning();
        _flashRoutine = StartCoroutine(CoPanelFlash());
    }

    /// <summary>
    /// 패드나 힌트 Emission을 즉시 끈다.
    /// </summary>
    public void TurnOffImmediate()
    {
        StopFlashRoutineIfRunning();
        SetActiveEmission(false, activeEmissionColor);
    }

    /// <summary>
    /// 퍼즐 램프를 기본/진행 중 색으로 설정한다.
    /// </summary>
    public void SetPuzzleLampNormal()
    {
        SetActiveEmission(true, normalEmissionColor);
    }

    /// <summary>
    /// 퍼즐 램프를 실패 색으로 설정한다.
    /// </summary>
    public void SetPuzzleLampFailed()
    {
        SetActiveEmission(true, failedEmissionColor);
    }

    /// <summary>
    /// 퍼즐 램프를 클리어 색으로 설정한다.
    /// </summary>
    public void SetPuzzleLampSolved()
    {
        SetActiveEmission(true, solvedEmissionColor);
    }

    /// <summary>
    /// 퍼즐 램프 상태 enum을 받아 상태별 Emission을 적용한다.
    /// </summary>
    public void SetPuzzleLampState(LightPatternLampState state)
    {
        switch (state)
        {
            case LightPatternLampState.Normal:
                SetPuzzleLampNormal();
                break;

            case LightPatternLampState.Failed:
                SetPuzzleLampFailed();
                break;

            case LightPatternLampState.Solved:
                SetPuzzleLampSolved();
                break;
        }
    }

    /// <summary>
    /// 패드 Emission 점등 코루틴.
    /// </summary>
    private IEnumerator CoPanelFlash()
    {
        SetActiveEmission(true, activeEmissionColor);
        yield return new WaitForSeconds(panelFlashSeconds);

        SetActiveEmission(false, activeEmissionColor);
        _flashRoutine = null;
    }

    /// <summary>
    /// Emission과 선택적 PointLight 상태를 같이 반영한다.
    /// </summary>
    private void SetActiveEmission(bool active, Color color)
    {
        EnsureInitialized();

        ApplyRendererEmission(active, color);
        ApplyPointLight(active, color);
    }

    /// <summary>
    /// Renderer MaterialPropertyBlock에 Emission 색상을 반영한다.
    /// </summary>
    private void ApplyRendererEmission(bool active, Color color)
    {
        if (targetRenderer == null)
            return;

        Color finalColor = active ? color * emissionIntensity : Color.black;

        targetRenderer.GetPropertyBlock(_propertyBlock);
        _propertyBlock.SetColor(emissionColorProperty, finalColor);
        targetRenderer.SetPropertyBlock(_propertyBlock);
    }

    /// <summary>
    /// HintBulb처럼 PointLight를 쓰는 경우 Light 상태를 반영한다.
    /// PuzzleLamp / PanelPad에서는 usePointLight를 false로 두면 된다.
    /// </summary>
    private void ApplyPointLight(bool active, Color color)
    {
        if (!usePointLight)
            return;

        if (pointLight == null)
            return;

        pointLight.color = color;
        pointLight.enabled = active;
    }

    /// <summary>
    /// 실행 중인 패드 점등 코루틴을 정리한다.
    /// </summary>
    private void StopFlashRoutineIfRunning()
    {
        if (_flashRoutine == null)
            return;

        StopCoroutine(_flashRoutine);
        _flashRoutine = null;
    }
}

/// <summary>
/// 전구 패턴 퍼즐 상태 램프의 네트워크 동기화용 상태.
/// 가능하면 프로젝트의 Enums.cs로 옮기는 것을 추천한다.
/// </summary>
public enum LightPatternLampState
{
    Normal = 0, // 기본/진행 중
    Failed = 1, // 실패
    Solved = 2  // 클리어
}