using UnityEngine;

/// <summary>
/// 라디오 상태(RadioState)에 따른 머티리얼 Emission 제어기
/// 고장(빨강), 대기(꺼짐), 활성(녹색) 3가지 상태를 표현하는 디스플레이 회로
/// </summary>
public class RadioGlassController : MonoBehaviour
{
    [Header("디스플레이 패널 참조")]
    [SerializeField] private MeshRenderer glassRenderer;
    [SerializeField] private int materialIndex = 0; // 유리 머티리얼의 인덱스

    [Header("발광 설정")]
    [ColorUsage(true, true)] // 인스펙터에서 HDR 컬러 피커 띄우기
    [SerializeField] private Color activeEmissionColor = Color.green * 2.5f; // 작동 중 (녹색)

    [ColorUsage(true, true)]
    [SerializeField] private Color brokenEmissionColor = Color.red * 2.5f;   // 고장 상태 (빨간색)

    private Material glassMat;
    private static readonly int EmissionColorID = Shader.PropertyToID("_EmissionColor");

    // 현재 기기 상태 메모리
    private RadioState currentRadioState = RadioState.InActive;

    private void Awake()
    {
        // 머티리얼 인스턴스화 (원본 오염 방지)
        if (glassRenderer != null)
        {
            glassMat = glassRenderer.materials[materialIndex];

            // 초기화: 씬 시작 시 기본적으로 꺼짐(InActive) 상태로 세팅
            // (만약 시작부터 고장 상태로 두고 싶다면 RadioState.Broken을 넘겨주면 돼!)
            SetRadioState(RadioState.InActive);
        }
    }

    // ─── [외부 제어용 단자 (API)] ───────────────────────────

    /// <summary>
    /// 라디오 상태를 전달받아 디스플레이 패널의 불빛을 제어한다.
    /// </summary>
    public void SetRadioState(RadioState newState)
    {
        if (glassMat == null) return;

        currentRadioState = newState;

        // 🛠️ 3단 기어 분배기
        switch (currentRadioState)
        {
            case RadioState.Broken:
                // 고장 상태: 붉은색 경고등
                glassMat.SetColor(EmissionColorID, brokenEmissionColor);
                break;

            case RadioState.InActive:
                // 대기/수리완료 직후: 빛을 끄고 반투명 유리로 복구
                glassMat.SetColor(EmissionColorID, Color.black);
                break;

            case RadioState.Active:
                // 정상 작동: 녹색 발광
                glassMat.SetColor(EmissionColorID, activeEmissionColor);
                break;
        }
    }

    // ─── [인스펙터 테스트 스위치] ─────────────────

    [ContextMenu("테스트 : Broken (고장 - 빨간불)")]
    private void TestBroken()
    {
        SetRadioState(RadioState.Broken);
        Debug.Log("[RadioGlass] 상태 변경 -> Broken (빨간불 점등)");
    }

    [ContextMenu("테스트 : InActive (대기 - 꺼짐)")]
    private void TestInActive()
    {
        SetRadioState(RadioState.InActive);
        Debug.Log("[RadioGlass] 상태 변경 -> InActive (발광 꺼짐)");
    }

    [ContextMenu("테스트 : Active (작동 - 녹색불)")]
    private void TestActive()
    {
        SetRadioState(RadioState.Active);
        Debug.Log("[RadioGlass] 상태 변경 -> Active (녹색불 점등)");
    }
}