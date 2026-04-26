using UnityEngine;

/// <summary>
/// 문양 레버 퍼즐의 개별 레버 시각 표현 담당
/// false = 위 상태 / true = 아래 상태
/// </summary>
public class SymbolLeverView : MonoBehaviour
{
    [Header("회전 대상")]
    [SerializeField] private Transform leverVisual; // 실제 회전시킬 레버 비주얼 오브젝트

    [Header("레버 각도")]
    [SerializeField] private float upAngleX = -40f;   // 위 상태 x각도
    [SerializeField] private float downAngleX = 40f;  // 아래 상태 x각도

    private Transform Target => leverVisual != null ? leverVisual : transform;

    /// <summary>
    /// 레버 상태를 받아 실제 각도를 적용한다.
    /// </summary>
    public void SetState(bool isPulled)
    {
        Vector3 localEuler = Target.localEulerAngles;
        localEuler.x = isPulled ? downAngleX : upAngleX;
        Target.localRotation = Quaternion.Euler(localEuler);
    }

    /// <summary>
    /// 기본 위 상태로 즉시 복귀
    /// </summary>
    public void ResetToDefaultImmediate()
    {
        SetState(false);
    }
}