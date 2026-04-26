using System.Collections;
using UnityEngine;

/// <summary>
/// 점등 패턴 퍼즐의 개별 패널 시각 표현 담당
/// 입력 시 노랑, 실패 시 빨강, 성공 시 초록을 표시한다.
/// </summary>
public class LightPatternPanelView : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private Light panelLight; // 패널에 달린 라이트

    [Header("색상")]
    [SerializeField] private Color inputColor = Color.yellow;   // 입력 시 색
    [SerializeField] private Color failColor = Color.red;       // 실패 시 색
    [SerializeField] private Color solvedColor = Color.green;   // 성공 시 색

    [Header("시간")]
    [SerializeField] private float inputFlashOnTime = 0.2f;     // 노랑 유지 시간
    [SerializeField] private float failFlashOnTime = 0.2f;      // 빨강 유지 시간

    private Coroutine _flashRoutine; // 현재 실행 중인 점등 연출 코루틴

    private void Awake()
    {
        TurnOffImmediate();
    }

    /// <summary>
    /// 입력 시 노란색으로 잠깐 켠다.
    /// </summary>
    public void PlayInputFlash()
    {
        PlayFlash(inputColor, inputFlashOnTime);
    }

    /// <summary>
    /// 실패 시 빨간색으로 잠깐 켠다.
    /// </summary>
    public void PlayFailFlash()
    {
        PlayFlash(failColor, failFlashOnTime);
    }

    /// <summary>
    /// 성공 시 초록색으로 계속 켜둔다.
    /// </summary>
    public void SetSolvedOn()
    {
        StopFlashRoutineIfRunning();
        SetLight(true, solvedColor);
    }

    /// <summary>
    /// 즉시 라이트를 끈다.
    /// </summary>
    public void TurnOffImmediate()
    {
        StopFlashRoutineIfRunning();

        if (panelLight == null)
            return;

        panelLight.enabled = false;
    }

    /// <summary>
    /// 지정한 색으로 일정 시간만 켠다.
    /// </summary>
    private void PlayFlash(Color color, float onTime)
    {
        StopFlashRoutineIfRunning();
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
    /// 실행 중인 플래시 코루틴 정리
    /// </summary>
    private void StopFlashRoutineIfRunning()
    {
        if (_flashRoutine == null)
            return;

        StopCoroutine(_flashRoutine);
        _flashRoutine = null;
    }

    /// <summary>
    /// 실제 라이트 상태 반영
    /// </summary>
    private void SetLight(bool isOn, Color color)
    {
        if (panelLight == null)
            return;

        panelLight.color = color;
        panelLight.enabled = isOn;
    }
}