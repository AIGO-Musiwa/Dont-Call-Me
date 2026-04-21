using UnityEngine;

/// <summary>
/// 스마트 글래스 활성화 유무에 따른 머티리얼 Emission 제어기
/// bool 값으로 On/Off만 제어하는 심플한 스위치 회로
/// </summary>
public class SmartGlassController : MonoBehaviour
{
    [Header("디스플레이 패널 참조")]
    [SerializeField] private MeshRenderer glassRenderer;
    [SerializeField] private int materialIndex = 0; // 유리 머티리얼의 인덱스

    [Header("발광 설정")]
    [ColorUsage(true, true)] // 인스펙터에서 HDR 컬러 피커를 띄우기 위한 태그
    [SerializeField] private Color activeEmissionColor = Color.green * 2.5f; // 활성화 시 색상과 강도

    private Material glassMat;
    private static readonly int EmissionColorID = Shader.PropertyToID("_EmissionColor");

    // 현재 전원 상태 (true: 켜짐, false: 꺼짐)
    private bool isPoweredOn = false;

    private void Awake()
    {
        // 머티리얼 인스턴스화 (원본 오염 방지)
        if (glassRenderer != null)
        {
            glassMat = glassRenderer.materials[materialIndex];

            // 초기화: 씬 시작 시 전원 꺼짐 상태(Black)로 덮어씌움
            glassMat.SetColor(EmissionColorID, Color.black);
        }
    }

    // ─── [외부 제어용 단자 (API)] ───────────────────────────

    /// <summary>
    /// 유리판의 전원 상태를 켜거나 끕니다.
    /// 외부 퍼즐 스크립트나 트리거에서 이 함수에 true/false를 쏴주면 됨!
    /// </summary>
    public void SetGlassState(bool isOn)
    {
        if (glassMat == null) return;

        isPoweredOn = isOn;

        if (isPoweredOn)
        {
            // 전원 인가: 설정한 HDR 색상으로 강하게 발광
            glassMat.SetColor(EmissionColorID, activeEmissionColor);
        }
        else
        {
            // 전원 차단: 빛을 끄고 원래의 반투명 유리로 복구
            glassMat.SetColor(EmissionColorID, Color.black);
        }
    }

    /// <summary>
    /// 현재 상태의 반대로 스위치를 토글합니다. (버튼 상호작용 등에 유용)
    /// </summary>
    public void ToggleGlassState()
    {
        SetGlassState(!isPoweredOn);
    }

    // ─── [인스펙터 테스트 스위치] ─────────────────

    [ContextMenu("테스트 : 전원 ON (녹색 발광)")]
    private void TestPowerOn()
    {
        SetGlassState(true);
        Debug.Log("[SmartGlass] 전원 인가 -> 활성화됨");
    }

    [ContextMenu("테스트 : 전원 OFF (발광 꺼짐)")]
    private void TestPowerOff()
    {
        SetGlassState(false);
        Debug.Log("[SmartGlass] 전원 차단 -> 반투명 상태로 복구");
    }
}