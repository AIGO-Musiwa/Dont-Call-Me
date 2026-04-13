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
    public float maxWalkieCost = 100f;
    public float dbDropPerMeter = 2f;

    [Header("포획 판정 설정")]
    public float captureRange = 2.0f;
    public float captureAngle = 90.0f;

    private float currentWalkieCost = 0f;
    private float actMultiplier = 1f;

    public void SetActMultiplier(float multiplier)
    {
        //삼막 등 이벤트 발생 시 코스트 배율 설정
        actMultiplier = multiplier;
    }

    public void AddWalkieCost(float amount)
    {
        //무전 코스트 누적
        currentWalkieCost += amount;
    }

    public void ResetWalkieCost()
    {
        //무전 코스트 초기화
        currentWalkieCost = 0f;
    }

    public bool IsCostThresholdReached()
    {
        //배율이 적용된 코스트 임계치 계산 및 도달 여부 반환
        float threshold = maxWalkieCost * actMultiplier;
        return currentWalkieCost >= threshold;
    }

    public float CalculatePerceivedDb(Vector3 noisePosition, float rawDb, bool isGlobal)
    {
        float perceivedDb = rawDb;
        //글로벌 소리가 아닐 경우 거리에 따른 소리 감쇠 적용
        if (!isGlobal)
        {
            perceivedDb -= (Vector3.Distance(transform.position, noisePosition) * dbDropPerMeter);
        }
        return perceivedDb;
    }

    public bool CheckLineOfSight(Transform target)
    {
        //타겟이 존재하지 않으면 무시
        if (target == null) return false;

        //층간 시야 차단 로직
        //Y축 높이 차이가 4.5m 이상 나면 다른 층으로 간주하고 시야 검사 생략
        float yDiff = Mathf.Abs(transform.position.y - target.position.y);
        if (yDiff > 4.5f) return false;

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
                    if (!Physics.Raycast(eyePosition, dirtoTarget, distanceToTarget, obstaclMask)) return true;
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
            if (angle <= captureAngle / 2f) return true;
        }
        return false;
    }
}