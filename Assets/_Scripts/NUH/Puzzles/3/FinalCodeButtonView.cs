using System.Collections;
using UnityEngine;

/// <summary>
/// FinalCode 숫자/지우기 버튼의 눌림 애니메이션 담당.
/// RectTransform이어도 Transform을 상속하므로 localPosition.z 이동이 가능하다.
/// </summary>
public class FinalCodeButtonView : MonoBehaviour
{
    [Header("버튼 비주얼")]
    [SerializeField] private Transform buttonVisual; // 실제로 앞뒤로 움직일 버튼 Transform

    [Header("눌림 이동")]
    [SerializeField] private float pressLocalZOffset = -0.02f; // 눌렸을 때 로컬 Z 이동량
    [SerializeField] private float pressInSeconds = 0.05f;     // 들어가는 시간
    [SerializeField] private float pressOutSeconds = 0.08f;    // 돌아오는 시간

    [Header("보간")]
    [SerializeField] private AnimationCurve pressCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f); // 이동 보간 곡선

    private Vector3 _defaultLocalPosition; // 시작 localPosition
    private bool _hasCachedDefaultPosition; // 기본 위치 캐싱 여부
    private Coroutine _pressRoutine; // 현재 실행 중인 애니메이션 코루틴

    private Transform Target => buttonVisual != null ? buttonVisual : transform;

    private void Awake()
    {
        CacheDefaultPosition();
    }

    /// <summary>
    /// 버튼 눌림 애니메이션을 재생한다.
    /// 이미 재생 중이면 중단 후 처음부터 다시 재생한다.
    /// </summary>
    public void PlayPress()
    {
        CacheDefaultPosition();

        if (_pressRoutine != null)
        {
            StopCoroutine(_pressRoutine);
            _pressRoutine = null;
        }

        _pressRoutine = StartCoroutine(CoPlayPress());
    }

    /// <summary>
    /// 버튼 위치를 즉시 기본 위치로 되돌린다.
    /// </summary>
    public void ResetImmediate()
    {
        CacheDefaultPosition();

        if (_pressRoutine != null)
        {
            StopCoroutine(_pressRoutine);
            _pressRoutine = null;
        }

        Target.localPosition = _defaultLocalPosition;
    }

    /// <summary>
    /// 현재 위치에서 -Z 방향으로 들어갔다가 원래 위치로 복귀한다.
    /// </summary>
    private IEnumerator CoPlayPress()
    {
        Vector3 startPosition = Target.localPosition;
        Vector3 pressedPosition = _defaultLocalPosition + new Vector3(0f, 0f, pressLocalZOffset);

        yield return CoMoveLocalPosition(startPosition, pressedPosition, pressInSeconds);
        yield return CoMoveLocalPosition(pressedPosition, _defaultLocalPosition, pressOutSeconds);

        Target.localPosition = _defaultLocalPosition;
        _pressRoutine = null;
    }

    /// <summary>
    /// 지정한 localPosition 사이를 부드럽게 이동한다.
    /// </summary>
    private IEnumerator CoMoveLocalPosition(Vector3 from, Vector3 to, float duration)
    {
        if (duration <= 0f)
        {
            Target.localPosition = to;
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / duration);
            float curvedT = pressCurve != null ? pressCurve.Evaluate(t) : t;

            Target.localPosition = Vector3.LerpUnclamped(from, to, curvedT);

            yield return null;
        }

        Target.localPosition = to;
    }

    /// <summary>
    /// 시작 localPosition을 기본 위치로 저장한다.
    /// </summary>
    private void CacheDefaultPosition()
    {
        if (_hasCachedDefaultPosition)
            return;

        _defaultLocalPosition = Target.localPosition;
        _hasCachedDefaultPosition = true;
    }
}