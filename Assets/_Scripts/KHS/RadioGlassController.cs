using UnityEngine;

public class RadioGlassController : MonoBehaviour
{
    [Header("컴포넌트 참조")]
    [SerializeField] private MeshRenderer glassRenderer;
    [SerializeField] private int materialIndex = 0;
    [SerializeField] private Behaviour outlineComponent; // 외곽선 컴포넌트 (Outline, OutlineEffect 등)

    [Header("발광 색상 설정 (HDR)")]
    [ColorUsage(true, true)][SerializeField] private Color brokenColor = Color.red * 2.5f;     // Broken: 빨간색
    [ColorUsage(true, true)][SerializeField] private Color progressColor = Color.blue * 2.5f;   // InProgress: 파란색
    [ColorUsage(true, true)][SerializeField] private Color readyColor = new Color(0.5f, 0.5f, 0.5f) * 2.5f; // Ready (InActive): 밝은 회색
    [ColorUsage(true, true)][SerializeField] private Color activeColor = Color.green * 2.5f;    // Active: 녹색 (작동 중)
    [ColorUsage(true, true)][SerializeField] private Color disabledColor = new Color(0.2f, 0.2f, 0.2f) * 1.0f; // Disabled (DeActive): 어두운 회색

    private Material glassMat;
    private static readonly int EmissionColorID = Shader.PropertyToID("_EmissionColor");

    private void Awake()
    {
        if (glassRenderer != null)
        {
            glassMat = glassRenderer.materials[materialIndex];
        }

        // 초기 상태 설정
        SetRadioState(RadioState.Broken);
    }

    public void SetRadioState(RadioState newState)
    {
        if (glassMat == null) return;

        // 1. 상태별 발광 색상 제어
        switch (newState)
        {
            case RadioState.Broken:
                glassMat.SetColor(EmissionColorID, brokenColor);
                break;
            case RadioState.InProgress:
                glassMat.SetColor(EmissionColorID, progressColor);
                break;
            case RadioState.Ready:
                glassMat.SetColor(EmissionColorID, readyColor);
                break;
            case RadioState.Active:
                glassMat.SetColor(EmissionColorID, activeColor);
                break;
            case RadioState.Disabled:
                glassMat.SetColor(EmissionColorID, disabledColor);
                break;
        }

        // 2. 외곽선 제어: Disabled 상태일 때 상호작용 불가를 표현하기 위해 외곽선을 끔 
        if (outlineComponent != null)
        {
            outlineComponent.enabled = (newState != RadioState.Disabled);
        }
    }
}