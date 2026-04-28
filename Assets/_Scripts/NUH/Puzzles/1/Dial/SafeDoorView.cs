using UnityEngine;

/// <summary>
/// 금고 문 시각 표현 담당.
/// 문 열림 상태에 따라 Y축으로 -120도 회전시킨다.
/// </summary>
public class SafeDoorView : MonoBehaviour
{
    [Header("문 회전 대상")]
    [SerializeField] private Transform doorVisual;           // 실제 회전시킬 문 Transform

    [Header("문 각도")]
    [SerializeField] private float closedLocalY = 0f;        // 닫힌 상태 Y각도
    [SerializeField] private float openedLocalYOffset = -120f; // 열린 상태 추가 Y각도

    private Transform Target => doorVisual != null ? doorVisual : transform;

    private void Awake()
    {
        SetOpenedImmediate(false);
    }

    /// <summary>
    /// 문 열림 상태를 즉시 반영한다.
    /// </summary>
    public void SetOpenedImmediate(bool opened)
    {
        Vector3 euler = Target.localEulerAngles;
        euler.y = closedLocalY + (opened ? openedLocalYOffset : 0f);
        Target.localRotation = Quaternion.Euler(euler);
    }
}