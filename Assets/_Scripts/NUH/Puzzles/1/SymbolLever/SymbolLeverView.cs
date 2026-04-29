using System.Collections;
using UnityEngine;

/// <summary>
/// 문양 레버 퍼즐의 개별 레버 시각 표현 담당.
/// false = 위 상태 / true = 아래 상태.
/// Y축 기준으로 레버를 부드럽게 회전시킨다.
/// </summary>
public class SymbolLeverView : MonoBehaviour
{
    [Header("회전 대상")]
    [SerializeField] private Transform leverVisual;              // 실제 회전시킬 레버 비주얼 오브젝트

    [Header("레버 각도")]
    [SerializeField] private float upAngleY = 0f;                // 위 상태 Y각도
    [SerializeField] private float pullAngleY = 180f;            // 위 상태에서 당길 때 추가할 Y각도
    [SerializeField] private bool invertPullDirection = false;   // 당기는 방향 반전 여부

    [Header("레버 애니메이션")]
    [SerializeField] private float rotateDuration = 0.25f;       // 레버 회전 시간
    [SerializeField] private AnimationCurve rotateCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f); // 회전 보간 곡선

    private Coroutine _rotateRoutine;                            // 현재 실행 중인 회전 코루틴

    private Transform Target => leverVisual != null ? leverVisual : transform;

    /// <summary>
    /// 레버 상태를 받아 부드럽게 각도를 적용한다.
    /// </summary>
    public void SetState(bool isPulled)
    {
        SetStateAnimated(isPulled);
    }

    /// <summary>
    /// 레버 상태를 즉시 반영한다.
    /// 스폰 초기화나 강제 동기화에 사용한다.
    /// </summary>
    public void SetStateImmediate(bool isPulled)
    {
        StopRotateRoutineIfRunning();
        Target.localRotation = GetTargetRotation(isPulled);
    }

    /// <summary>
    /// 레버 상태를 애니메이션으로 반영한다.
    /// </summary>
    public void SetStateAnimated(bool isPulled)
    {
        StopRotateRoutineIfRunning();
        _rotateRoutine = StartCoroutine(CoRotateToState(isPulled));
    }

    /// <summary>
    /// 기본 위 상태로 즉시 복귀한다.
    /// </summary>
    public void ResetToDefaultImmediate()
    {
        SetStateImmediate(false);
    }

    /// <summary>
    /// 기본 위 상태로 부드럽게 복귀한다.
    /// </summary>
    public void ResetToDefaultAnimated()
    {
        SetStateAnimated(false);
    }

    /// <summary>
    /// 현재 상태에 해당하는 목표 회전을 계산한다.
    /// </summary>
    private Quaternion GetTargetRotation(bool isPulled)
    {
        float signedPullAngle = invertPullDirection ? -pullAngleY : pullAngleY;
        float targetY = isPulled ? upAngleY + signedPullAngle : upAngleY;

        Vector3 euler = Target.localEulerAngles;
        euler.y = targetY;

        return Quaternion.Euler(euler);
    }

    /// <summary>
    /// 목표 상태까지 Y축 회전을 보간한다.
    /// </summary>
    private IEnumerator CoRotateToState(bool isPulled)
    {
        Quaternion startRotation = Target.localRotation;
        Quaternion targetRotation = GetTargetRotation(isPulled);

        float elapsed = 0f;

        while (elapsed < rotateDuration)
        {
            elapsed += Time.deltaTime;

            float t = rotateDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / rotateDuration);
            float curvedT = rotateCurve != null ? rotateCurve.Evaluate(t) : t;

            Target.localRotation = Quaternion.Slerp(startRotation, targetRotation, curvedT);

            yield return null;
        }

        Target.localRotation = targetRotation;
        _rotateRoutine = null;
    }

    /// <summary>
    /// 실행 중인 회전 코루틴을 정리한다.
    /// </summary>
    private void StopRotateRoutineIfRunning()
    {
        if (_rotateRoutine == null)
            return;

        StopCoroutine(_rotateRoutine);
        _rotateRoutine = null;
    }
}