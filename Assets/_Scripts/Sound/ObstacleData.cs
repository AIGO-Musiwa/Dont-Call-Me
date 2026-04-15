using UnityEngine;

public class ObstacleData : MonoBehaviour
{
    [Tooltip("현재 차폐 패널티 dB (양수 입력 - 내부에서 음수로 처리)")]
    [SerializeField] private float penaltydB = 3f;

    [Tooltip("true면 차폐 없음 (열린 문 등) - 없지만 필요할 수도 있으니")]
    [SerializeField] private bool isPassthrough = false;

    public float GetPenalty()
    {
        if (isPassthrough) return 0f;
        return penaltydB;
    }

    // 열림/닫힘 같은 상태 변화시 호출
    public void SetPassthrough(bool passthrough)
    {
        isPassthrough = passthrough;
    }
}
