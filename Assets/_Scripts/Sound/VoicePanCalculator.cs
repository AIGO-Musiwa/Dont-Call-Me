using UnityEngine;

public static class VoicePanCalculator
{
    // listenerTransform 기준으로 targetPosition이 좌/우 어느 쪽인지 계산
    public static float Calculate(Transform listenerTransform, Vector3 targetPosition, float panRange = 1f)
    {
        if (listenerTransform == null) return 0f;

        // 리스너 -> 타겟 방향 벡터 (수평만)
        Vector3 toTarget = targetPosition - listenerTransform.position;
        toTarget.y = 0f;

        if (toTarget.sqrMagnitude < 0.0001f) return 0f;

        // 리스너의 오른쪽 벡터와 내적 => -1: 왼쪽 / 1: 오른쪽
        Vector3 listenerRight = listenerTransform.right;
        listenerRight.y = 0f;
        listenerRight.Normalize();

        float dot = Vector3.Dot(toTarget.normalized, listenerRight);

        return Mathf.Clamp(dot * panRange, -1f, 1f);
    }
}
