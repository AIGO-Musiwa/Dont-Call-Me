using UnityEngine;

/// <summary>
/// 숫자 입력 퍼즐의 시계 힌트 월드 표시 담당
/// 
/// 역할
/// - 정각 기준 시침 숫자를 받아 시계 바늘 각도 맞추기
/// - 분침x
/// </summary>
public class ClockHintDisplay : MonoBehaviour
{
    [Header("시침")]
    [SerializeField] private Transform hourHand;        //실제 회전시킬 시침

    [Header("회전 설정")]
    [SerializeField] private float startAngleZ = 0f;    // 12시 기준 시작 각도
    [SerializeField] private float hourStepAngle = 30f; // 한 시간당 회전 각도

    [Header("디버그")]
    [SerializeField] private bool enableDebugLog = true;


    /// <summary>
    /// 시계 숫자를 받아 정각 상태로 시침 맞추기
    /// </summary>
    public void SetHour(int hour)
    {
        if (hourHand == null)
            return;

        // 퍼즐 규칙상 1~9만 쓰지만, 방어적으로 0~11 범위로 한 번 정리
        int normalizedHour = Mathf.Abs(hour) % 12;

        // 12시는 0으로 들어오면 그대로 12시 방향을 가리키게 하기
        float targetAngleZ = startAngleZ + (normalizedHour * hourStepAngle);

        Vector3 localEuler = hourHand.localEulerAngles;
        localEuler.z = targetAngleZ;
        hourHand.localRotation = Quaternion.Euler(localEuler);

        Log($"시계 설정 완료 | hour = {hour} | normalizedHour = {normalizedHour} | targetAngleZ = {targetAngleZ}");
    }

    /// <summary>
    /// 시계 초기화
    /// </summary>
    public void ResetToDefault()
    {
        if (hourHand == null)
            return;

        Vector3 localEuler = hourHand.localEulerAngles;
        localEuler.z = startAngleZ;
        hourHand.localRotation = Quaternion.Euler(localEuler); 

        Log("시계 기본 상태로 초기화");
    }

    /// <summary>
    /// 일반 디버그 로그 출력.
    /// </summary>
    private void Log(string message)
    {
        if (!enableDebugLog)
            return;

        Debug.Log($"[ClockHintDisplay] {message}", this);
    }
}
