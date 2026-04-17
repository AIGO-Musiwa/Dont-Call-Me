using System.Collections;
using UnityEngine;

/// <summary>
/// 점등 패턴 퍼즐의 개별 패널 시각 표현 담당
/// 입력 시 한 번 켜지고, 실패 시 전체 깜빡임 연출에 참여한다.
/// </summary>
public class LightPatternPanelView : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private Light panelLight; // 패널에 달린 라이트

    [Header("색상")]
    [SerializeField] private Color inputColor = Color.yellow;       // 입력 시 
    [SerializeField] private Color failColor = Color.red;           // 틀렸을 시
    [SerializeField] private Color solvedColor = Color.green;       // 성공 시

    [Header("시간")]
    [SerializeField] private float inputFlashOnTime = 0.2f; // 정답/입력 피드백 켜짐 시간
    [SerializeField] private float failFlashOnTime = 0.2f;  // 실패 연출 시 켜짐 시간

    private Coroutine _flashRoutine; // 현재 실행 중인 점등 연출 코루틴

    private void Awake()
    {
        TurnOffImmediate();
    }

    /// <summary>
    /// 입력 시 패널 라이트를 한 번 켠다.
    /// </summary>
    public void PlayInputFlash()
    {
        PlayFlash(inputColor, inputFlashOnTime);
    }

    /// <summary>
    /// 실패 연출 시 패널 라이트를 한 번 켠다.
    /// </summary>
    public void PlayFailFlash()
    {
        PlayFlash(failColor, failFlashOnTime);
    }

    /// <summary>
    /// 성공 시 초록색으로 계속 켜두기
    /// </summary>
    public void SetSolvedOn()
    {
        if(_flashRoutine != null)
        {
            StopCoroutine(_flashRoutine);
            _flashRoutine = null;
        }

        SetLight(true, solvedColor);
    }

    /// <summary>
    /// 즉시 라이트를 끈다.
    /// 퍼즐 초기화나 클리어 직후 정리에 사용
    /// </summary>
    public void TurnOffImmediate()
    {
        if(_flashRoutine != null)
        {
            StopCoroutine(_flashRoutine);
            _flashRoutine = null;
        }

        if (panelLight == null)
            return;

        panelLight.enabled = false;
    }


    private void PlayFlash(Color color, float onTime)
    {
        if (_flashRoutine != null)
            StopCoroutine(_flashRoutine);

        _flashRoutine = StartCoroutine(CoFlash(color, onTime));
    }

    private IEnumerator CoFlash(Color color, float onTime)
    {
        SetLight(true, color);
        yield return new WaitForSeconds(onTime);

        if (panelLight != null)
            panelLight.enabled = false;

        _flashRoutine = null;
    }

    /// <summary>
    /// 실제 라이트 켜짐/꺼짐 반영
    /// </summary>
    private void SetLight(bool isOn, Color color)
    {
        if (panelLight == null)
            return;

        panelLight.color = color;
        panelLight.enabled = isOn;
    }
}