using UnityEngine;

public static class VoiceObstructionDetector
{
    // GC방지용 static 버퍼 (Unity 물리는 메인 스레드 단일 실행이므로 안전)
    private static readonly RaycastHit[] hitBuffer = new RaycastHit[16];

    public static float GetObstructionMultiplier(Vector3 from, Vector3 to, 
        LayerMask obstructionMask, float perWallAttenuation = 0.75f, int maxWallCount = 3)
    {
        Vector3 direction = to - from;
        float distance = direction.magnitude;

        if (distance < 0.01f) return 1f;

        int hitCount = Physics.RaycastNonAlloc(
            from,
            direction.normalized,
            hitBuffer,
            distance,
            obstructionMask);

        if (hitCount <= 0) return 1f;

        int clampedCount = Mathf.Min(hitCount, maxWallCount);
        return Mathf.Pow(perWallAttenuation, clampedCount);
    }
}
