using UnityEngine;

/// <summary>
/// 다이얼 퍼즐의 시각 표현 담당.
/// 현재 signed position 값을 받아 즉시 각도에 반영한다.
/// </summary>
public class DialView : MonoBehaviour
{
    [Header("회전 대상")]
    [SerializeField] private Transform dialVisual;       // 실제 회전시킬 다이얼 비주얼

    [Header("회전 설정")]
    [SerializeField] private float stepAngle = 36f;      // 한 칸당 회전 각도, 36도 = 10칸 다이얼
    [SerializeField] private float startAngleZ = 0f;     // 시작 상태 각도

    private Transform Target => dialVisual != null ? dialVisual : transform;

    private void Awake()
    {
        ResetToStartImmediate();
    }

    /// <summary>
    /// signed position 값을 받아 다이얼 각도를 즉시 반영한다.
    /// </summary>
    public void SetSignedPositionImmediate(int signedPosition)
    {
        Vector3 euler = Target.localEulerAngles;
        euler.z = startAngleZ + signedPosition * stepAngle;
        Target.localRotation = Quaternion.Euler(euler);
    }

    /// <summary>
    /// 시작 상태 각도로 즉시 복귀한다.
    /// </summary>
    public void ResetToStartImmediate()
    {
        SetSignedPositionImmediate(0);
    }
}