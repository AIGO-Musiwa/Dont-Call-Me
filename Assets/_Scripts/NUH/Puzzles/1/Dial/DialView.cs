using UnityEngine;

/// <summary>
/// 다이얼 퍼즐의 시각 표현 담당
/// 입력 1회마다 45도 회전, 실패 시 시작 각도로 복귀
/// </summary>
public class DialView : MonoBehaviour
{
    [Header("회전 대상")]
    [SerializeField] private Transform dialVisual;      // 실제 회전시킬 다이얼 비주얼

    [Header("회전 설정")]
    [SerializeField] private float stepAngle = 45f;     // 한칸 당 회전 각도
    [SerializeField] private float startAngleZ = 0f;    // 시작 상태 각도

    private Transform Target => dialVisual != null ? dialVisual : transform;


    private void Awake()
    {
        ResetToStartImmediate();
    }

    /// <summary>
    /// 입력 1회에 따라 다이얼을 45도 회전
    /// 좌측은 -45, 우측은 +45
    /// </summary>
    /// <param name="dir"></param>
    public void ApplyStepRotation(RotationDirection dir)
    {
        Vector3 euler = Target.localEulerAngles;

        if (dir == RotationDirection.Left)
            euler.z -= stepAngle;
        else
            euler.z += stepAngle;

        Target.localRotation = Quaternion.Euler(euler);
    }

    /// <summary>
    /// 시작 상태 각도로 즉시 복귀
    /// </summary>
    public void ResetToStartImmediate()
    {
        Vector3 euler = Target.localEulerAngles;
        euler.z = startAngleZ;
        Target.localRotation = Quaternion.Euler(euler);
    }

}
