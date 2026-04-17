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

    [Header("입력 깜빡임 시간")]
    [SerializeField] private float inputFlashOnTime = 0.2f; // 정답/입력 피드백 켜짐 시간

    [Header("실패 깜빡임 시간")]
    [SerializeField] private float failFlashOnTime = 0.2f;  // 실패 연출 시 켜짐 시간

    private Coroutine _flashRoutine; // 현재 실행 중인 점등 연출 코루틴

    private void Awake()
    {
        SetLight(false); // 시작 시 꺼진 상태로 초기화
    }

    /// <summary>
    /// 입력 시 패널 라이트를 한 번 켠다.
    /// </summary>
    public void PlayInputFlash()
    {
        if (_flashRoutine != null)
            StopCoroutine(_flashRoutine);

        _flashRoutine = StartCoroutine(CoFlash(inputFlashOnTime));
    }

    /// <summary>
    /// 실패 연출 시 패널 라이트를 한 번 켠다.
    /// </summary>
    public void PlayFailFlash()
    {
        if (_flashRoutine != null)
            StopCoroutine(_flashRoutine);

        _flashRoutine = StartCoroutine(CoFlash(failFlashOnTime));
    }

    /// <summary>
    /// 즉시 라이트를 끈다.
    /// 퍼즐 초기화나 클리어 직후 정리에 사용
    /// </summary>
    public void TurnOffImmediate()
    {
        if (_flashRoutine != null)
        {
            StopCoroutine(_flashRoutine);
            _flashRoutine = null;
        }

        SetLight(false);
    }

    /// <summary>
    /// 일정 시간 동안만 라이트를 켰다가 끄는 공용 코루틴
    /// </summary>
    private IEnumerator CoFlash(float onTime)
    {
        SetLight(true);
        yield return new WaitForSeconds(onTime);
        SetLight(false);
        _flashRoutine = null;
    }

    /// <summary>
    /// 실제 라이트 켜짐/꺼짐 반영
    /// </summary>
    private void SetLight(bool isOn)
    {
        if (panelLight == null)
            return;

        panelLight.enabled = isOn;
    }
}