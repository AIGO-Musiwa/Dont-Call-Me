using UnityEditor;
using UnityEngine;

public class CreatureSensor : MonoBehaviour
{
    [Header("시야 설정")]
    public float losRange = 20.0f;
    public float losAngle = 90.0f;
    public float eyeHeight = 2.0f;
    public LayerMask obstaclMask;

    [Header("소리 및 코스트 설정")]
    public float alertThresholdDB = 18f;
    public float criticalThresholdDB = 28f;

    [Header("포획 판정 설정")]
    public float captureRange = 2.0f;
    public float captureAngle = 90.0f;

    [Header("은신 발각 설정")]
    public float cabinetDetectRange = 1.5f;
    public float deskDetectRange = 1.2f;

    public float CalculatePerceivedDb(float voicedB, float distance, float obstaclePenalty)
    {
        //거리가 1m 미만일 때 log 값이 음수가 되는 방지하기 위해 최소 1f 적용
        float distanceDrop = 20f * Mathf.Log10(Mathf.Max(1f, distance));
        float perceivedDb = voicedB - distanceDrop - obstaclePenalty;

        return perceivedDb;
    }

    public bool CheckLineOfSight(Transform target)
    {
        //타겟이 존재하지 않으면 무시
        if (target == null) return false;

        //크리처의 눈 높이와 플레이어 몸통 기준(1m)의 실제 높이 차이 계산
        float eyeY = transform.position.y + eyeHeight;
        float targetCenterY = target.position.y + 1.0f;
        float yDiff = Mathf.Abs(transform.position.y - target.position.y);

        //Y축 높이 차이가 8m 이상 나면 다른 층으로 간주하고 시야 검사 생략
        if (yDiff > 8.0f) return false;

        //타겟 방향 및 거리 계산
        Vector3 directionToTarget = (target.position - transform.position).normalized;
        float distanceToTarget = Vector3.Distance(transform.position, target.position);

        //타겟이 시야 거리 내에 있는지 확인
        if (distanceToTarget <= losRange)
        {
            //타겟이 시야 각도 내에 있는지 확인
            float angle = Vector3.Angle(transform.forward, directionToTarget);
            if (angle <= losAngle / 2f)
            {
                //크리처 눈 위치 설정
                Vector3 eyePosition = transform.position + Vector3.up * eyeHeight;

                //플레이어 머리와 허리 지점 설정
                Vector3[] targetPoints = {
                    target.position + Vector3.up * 1.6f,
                    target.position + Vector3.up * 0.8f
                };

                //각 지점을 향해 레이캐스트 발사
                foreach (Vector3 targetPlayer in targetPoints)
                {
                    Vector3 dirtoTarget = (targetPlayer - eyePosition).normalized;

                    //레이캐스트가 장애물에 부딪히지 않으면 시야에 보인다고 판정
                    if (!Physics.Raycast(eyePosition, dirtoTarget, distanceToTarget, obstaclMask, QueryTriggerInteraction.Collide)) return true;
                }
            }
        }
        return false;
    }

    public bool CheckCaptureCondition(Transform target)
    {
        //타겟이 존재하지 않으면 무시
        if (target == null) return false;

        //평면 위치 계산
        Vector3 flatCreaturePos = new Vector3(transform.position.x, 0, transform.position.z);
        Vector3 flatTargetPos = new Vector3(target.position.x, 0, target.position.z);

        //평면 거리 및 높이 차이 계산
        float currentFlatDistance = Vector3.Distance(flatCreaturePos, flatTargetPos);
        float yDiff = Mathf.Abs(transform.position.y - target.position.y);

        //거리가 가깝고 같은 층일 때 각도 확인
        if (currentFlatDistance <= captureRange && yDiff < 2.0f)
        {
            //평면 기준 타겟 방향 계산
            Vector3 directionToTarget = (flatTargetPos - flatCreaturePos).normalized;
            float angle = Vector3.Angle(transform.forward, directionToTarget);

            //타겟이 포획 각도 내에 들어오면 포획 조건 성립
            if (angle <= captureAngle / 2f)
            {
                //크리쳐와 플레이어 가슴 높이를 기준으로 선을 그어 장애물이 있는지 확인
                Vector3 rayOrigin = transform.position + Vector3.up * 1.0f;
                Vector3 rayTarget = target.position + Vector3.up * 1.0f;
                Vector3 rayDir = (rayTarget - rayOrigin).normalized;
                float rayDist = Vector3.Distance(rayOrigin, rayTarget);

                //obstaclMask에 닿는 것이 없을 때만 포획
                if (!Physics.Raycast(rayOrigin, rayDir, rayDist, obstaclMask, QueryTriggerInteraction.Collide)) return true;
            }
        }
        return false;
    }

    public bool CheckHiddenPlayerDetect(Transform target, HideState hideState)
    {
        //타겟이 존재하지 않으면 무시
        if (target == null) return false;

        //평면 거리 및 높이 차이 계산
        Vector3 flatCreaturePos = new Vector3(transform.position.x, 0, transform.position.z);
        Vector3 flatTargetPos = new Vector3(target.position.x, 0, target.position.z);

        float distance = Vector3.Distance(flatCreaturePos, flatTargetPos);
        float yDiff = Mathf.Abs(transform.position.y - target.position.y);

        //층이 다르면 발각되지 않음
        if (yDiff > 2.0f) return false;

        //숨은 상태에 따른 발각 거리 설정
        float detectRange = (hideState == HideState.Cabinet) ? cabinetDetectRange : deskDetectRange;

        //발각 거리 이내로 들어오면 들킴
        return distance <= detectRange;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        //시야 범위 기즈모 (반투명 노란색 부채꼴)
        Vector3 eyePosition = transform.position + Vector3.up * eyeHeight;
        Vector3 leftDir = Quaternion.Euler(0, -losAngle / 2f, 0) * transform.forward;
        Vector3 rightDir = Quaternion.Euler(0, losAngle / 2f, 0) * transform.forward;

        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(eyePosition, leftDir * losRange);
        Gizmos.DrawRay(eyePosition, rightDir * losRange);

        Handles.color = new Color(1f, 0.92f, 0.016f, 0.1f);
        Handles.DrawSolidArc(eyePosition, Vector3.up, leftDir, losAngle, losRange);

        //포획 범위 기즈모 (반투명 빨간색 원)
        Handles.color = new Color(1f, 0f, 0f, 0.2f);
        Handles.DrawSolidDisc(transform.position, Vector3.up, captureRange);

        //은신 발각 범위 기즈모 (반투명 보라색 원 - 캐비닛 기준)
        Handles.color = new Color(0.5f, 0f, 0.5f, 0.2f);
        Handles.DrawSolidDisc(transform.position, Vector3.up, cabinetDetectRange);
    }
#endif
}