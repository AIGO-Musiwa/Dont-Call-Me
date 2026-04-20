using UnityEngine;

/// <summary>
/// 점등 패턴 퍼즐의 개별 패널 시각 표현 담당
/// 루트 퍼즐이 전달하는 상태 enum을 그대로 라이트 색에 반영한다.
/// </summary>
public class LightPatternPanelView : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private Light panelLight; // 패널에 달린 라이트

    [Header("색상")]
    [SerializeField] private Color inputColor = Color.yellow; // 입력 시
    [SerializeField] private Color failColor = Color.red;     // 실패 시
    [SerializeField] private Color solvedColor = Color.green; // 성공 시

    private void Awake()
    {
        TurnOffImmediate();
    }

    /// <summary>
    /// 상태 enum을 받아 라이트에 적용
    /// </summary>
    public void ApplyVisualState(LightPatternPanelVisualState state)
    {
        switch (state)
        {
            case LightPatternPanelVisualState.Off:
                TurnOffImmediate();
                break;

            case LightPatternPanelVisualState.InputYellow:
                SetLight(true, inputColor);
                break;

            case LightPatternPanelVisualState.FailRed:
                SetLight(true, failColor);
                break;

            case LightPatternPanelVisualState.SolvedGreen:
                SetLight(true, solvedColor);
                break;
        }
    }

    /// <summary>
    /// 즉시 라이트 OFF
    /// </summary>
    public void TurnOffImmediate()
    {
        if (panelLight == null)
            return;

        panelLight.enabled = false;
    }

    /// <summary>
    /// 라이트 색과 켜짐 상태 반영
    /// </summary>
    private void SetLight(bool isOn, Color color)
    {
        if (panelLight == null)
            return;

        panelLight.color = color;
        panelLight.enabled = isOn;
    }
}