using System.Collections;
using UnityEngine;

/// <summary>
/// 금고 문 시각 표현 담당.
/// 문 열림 상태에 따라 Y축으로 -120도 회전시킨다.
/// 즉시 반영과 부드러운 열림 애니메이션을 모두 지원한다.
/// </summary>
public class SafeDoorView : MonoBehaviour
{
    [Header("문 회전 대상")]
    [SerializeField] private Transform doorVisual;                   // 실제 회전시킬 문 Transform

    [Header("문 각도")]
    [SerializeField] private float closedLocalY = 0f;                // 닫힌 상태 Y각도
    [SerializeField] private float openedLocalYOffset = -120f;       // 열린 상태 추가 Y각도

    [Header("문 애니메이션")]
    [SerializeField] private float openDuration = 0.8f;              // 문 열림/닫힘 시간
    [SerializeField] private AnimationCurve openCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f); // 문 보간 곡선

    private Coroutine _doorRoutine;                                  // 현재 실행 중인 문 회전 코루틴

    private Transform Target => doorVisual != null ? doorVisual : transform;

    private void Awake()
    {
        SetOpenedImmediate(false);
    }

    /// <summary>
    /// 문 열림 상태를 즉시 반영한다.
    /// 스폰 초기화나 중간 접속 상태 보정에 사용한다.
    /// </summary>
    public void SetOpenedImmediate(bool opened)
    {
        StopDoorRoutineIfRunning();

        Vector3 euler = Target.localEulerAngles;
        euler.y = GetTargetLocalY(opened);
        Target.localRotation = Quaternion.Euler(euler);
    }

    /// <summary>
    /// 문 열림 상태를 애니메이션으로 반영한다.
    /// 실제 퍼즐 진행 중 문이 열릴 때 사용한다.
    /// </summary>
    public void SetOpenedAnimated(bool opened)
    {
        StopDoorRoutineIfRunning();
        _doorRoutine = StartCoroutine(CoRotateDoor(opened));
    }

    /// <summary>
    /// 문을 목표 Y각도까지 부드럽게 회전시킨다.
    /// </summary>
    private IEnumerator CoRotateDoor(bool opened)
    {
        Quaternion startRotation = Target.localRotation;

        Vector3 targetEuler = Target.localEulerAngles;
        targetEuler.y = GetTargetLocalY(opened);
        Quaternion targetRotation = Quaternion.Euler(targetEuler);

        float elapsed = 0f;

        while (elapsed < openDuration)
        {
            elapsed += Time.deltaTime;

            float t = openDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / openDuration);
            float curvedT = openCurve != null ? openCurve.Evaluate(t) : t;

            Target.localRotation = Quaternion.Slerp(startRotation, targetRotation, curvedT);

            yield return null;
        }

        Target.localRotation = targetRotation;
        _doorRoutine = null;
    }

    /// <summary>
    /// 열린 상태에 맞는 목표 Y각도를 반환한다.
    /// </summary>
    private float GetTargetLocalY(bool opened)
    {
        return closedLocalY + (opened ? openedLocalYOffset : 0f);
    }

    /// <summary>
    /// 실행 중인 문 회전 코루틴을 정리한다.
    /// </summary>
    private void StopDoorRoutineIfRunning()
    {
        if (_doorRoutine == null)
            return;

        StopCoroutine(_doorRoutine);
        _doorRoutine = null;
    }
}