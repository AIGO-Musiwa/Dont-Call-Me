using UnityEngine;

/// <summary>
/// 무전기 상태에 따른 머티리얼 Emission 제어기
/// 상태가 바뀔 때 SetWalkieState()만 호출해 주면 됨.
/// </summary>
public class WalkieMeterialController : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private MeshRenderer walkieRenderer;
    [SerializeField] private int materialIndex = 1; // LED가 있는 머티리얼 인덱스

    private Material walkieMat;
    private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

    private void Awake()
    {
        // 머티리얼 인스턴스화 (원본 머티리얼 오염 방지)
        if (walkieRenderer != null)
        {
            walkieMat = walkieRenderer.materials[materialIndex];
        }
    }

    // ─── [실제 프로그래머 연동용 외부 API] ───────────────────────────

    public void SetWalkieState(WalkieState state)
    {
        if (walkieMat == null) return;

        switch (state)
        {
            case WalkieState.Idle:
                // 대기: 블랙 (꺼짐)
                walkieMat.SetColor(EmissionColor, Color.black);
                break;

            case WalkieState.TX:
                // 송신: 강렬한 레드 (고정 발광)
                // Color에 강도를 곱해서 HDR Emission을 증폭시킴
                walkieMat.SetColor(EmissionColor, Color.red * 2.0f);
                break;

            case WalkieState.RX:
                // 수신: 녹색 발광 
                walkieMat.SetColor(EmissionColor, Color.green * 2.0f);
                break;
        }
    }

    // ─── [인스펙터 테스트 스위치 (점 3개 메뉴)] ─────────────────

    [ContextMenu("테스트 : Idle (대기 상태)")]
    private void TestIdle()
    {
        SetWalkieState(WalkieState.Idle);
        Debug.Log("[WalkieMaterial] 테스트 -> Idle (LED 꺼짐)");
    }

    [ContextMenu("테스트 : TX (송신 중 - 빨간불)")]
    private void TestTX()
    {
        SetWalkieState(WalkieState.TX);
        Debug.Log("[WalkieMaterial] 테스트  -> TX (빨간색 Emission)");
    }

    [ContextMenu("테스트 : RX (수신 중 - 녹색불)")]
    private void TestRX()
    {
        SetWalkieState(WalkieState.RX);
        Debug.Log("[WalkieMaterial] 테스트  -> RX (녹색색 Emission)");
    }
}