using System.Collections;
using UnityEngine;

/// <summary>
/// 전선 퍼즐 Confirm 레버 전용 View.
/// 문양 레버와 같은 모델링을 쓰더라도 동작은 전선 퍼즐 전용으로 분리한다.
/// Y축 기준으로 -pullAngleY 방향으로 당겨진다.
/// </summary>
public class WireJudgeLeverView : MonoBehaviour
{
    [Header("회전 대상")]
    [SerializeField] private Transform leverVisual;              // 실제 회전시킬 Confirm 레버 피벗

    [Header("레버 회전")]
    [SerializeField] private float pullAngleY = 180f;            // 당길 때 Y축으로 적용할 각도, 실제 적용은 -값
    [SerializeField] private float rotateDuration = 0.25f;       // 레버 회전 시간
    [SerializeField] private AnimationCurve rotateCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f); // 회전 보간 곡선

    private Quaternion _defaultLocalRotation;                    // 기본 localRotation
    private bool _hasCachedDefaultRotation;                      // 기본 회전 캐싱 여부
    private Coroutine _rotateRoutine;                            // 현재 회전 코루틴

    private Transform Target => leverVisual != null ? leverVisual : transform;

    private void Awake()
    {
        CacheDefaultRotation();
        SetResetImmediate();
    }

    /// <summary>
    /// Confirm 레버를 당긴 상태로 애니메이션한다.
    /// </summary>
    public void PlayPull()
    {
        RotateToState(true);
    }

    /// <summary>
    /// Confirm 레버를 기본 상태로 애니메이션 복귀시킨다.
    /// </summary>
    public void PlayReset()
    {
        RotateToState(false);
    }

    /// <summary>
    /// Confirm 레버를 당긴 상태로 즉시 반영한다.
    /// 성공 상태 복원용이다.
    /// </summary>
    public void SetPulledImmediate()
    {
        StopRotateRoutineIfRunning();
        Target.localRotation = GetTargetRotation(true);
    }

    /// <summary>
    /// Confirm 레버를 기본 상태로 즉시 반영한다.
    /// 초기화용이다.
    /// </summary>
    public void SetResetImmediate()
    {
        StopRotateRoutineIfRunning();
        Target.localRotation = GetTargetRotation(false);
    }

    /// <summary>
    /// 목표 상태로 회전 코루틴을 시작한다.
    /// </summary>
    private void RotateToState(bool pulled)
    {
        CacheDefaultRotation();

        StopRotateRoutineIfRunning();
        _rotateRoutine = StartCoroutine(CoRotateToState(pulled));
    }

    /// <summary>
    /// 레버를 목표 상태까지 부드럽게 회전시킨다.
    /// </summary>
    private IEnumerator CoRotateToState(bool pulled)
    {
        Quaternion startRotation = Target.localRotation;
        Quaternion targetRotation = GetTargetRotation(pulled);

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
    /// pulled 상태에 맞는 목표 회전을 계산한다.
    /// 항상 Y축 -pullAngleY 방향으로 당긴다.
    /// </summary>
    private Quaternion GetTargetRotation(bool pulled)
    {
        CacheDefaultRotation();

        if (!pulled)
            return _defaultLocalRotation;

        return _defaultLocalRotation * Quaternion.Euler(0f, -pullAngleY, 0f);
    }

    /// <summary>
    /// 기본 localRotation을 저장한다.
    /// </summary>
    private void CacheDefaultRotation()
    {
        if (_hasCachedDefaultRotation)
            return;

        _defaultLocalRotation = Target.localRotation;
        _hasCachedDefaultRotation = true;
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