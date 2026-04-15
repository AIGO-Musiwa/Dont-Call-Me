using UnityEngine;

/// <summary>
/// 문양 레버 퍼즐의 개별 레버 시각 표현 담당
/// flase = 위 -40도 / true = 아래 
/// </summary>
public class SymbolLeverView : MonoBehaviour
{
    [Header("레버 회전 대상")]
    [SerializeField] private Transform leverVisual;         // 실제 회전시킬 레버 비주얼 오브젝트

    [Header("레버 각도")]
    [SerializeField] private float upAngleX = -40f;         //위 상태 x각도
    [SerializeField] private float downAngleX = 40f;        //아래 상태 X각도


    /// <summary>
    /// 레버 상태를 받아 실제 각도를 적용한다
    /// </summary>
    public void SetState(bool isDown)
    {
        Transform target = leverVisual != null ? leverVisual : transform;

        Vector3 localEuler = target.localEulerAngles;
        localEuler.x = isDown ? downAngleX : upAngleX;
        target.localRotation = Quaternion.Euler(localEuler);
    }
}
