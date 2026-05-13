using UnityEngine;

public class CreatureWalkieTracker : MonoBehaviour
{
    [Header("무전 코스트 설정")]
    [Tooltip("1-2막 코스트 임계치")]
    public float costDangerThreshold = 60.0f;
    [Tooltip("3막 코스트 임계치")]
    public float costDangerThresholdFinal = 10.0f;

    [Header("무전 코스트 속도 설정")]
    [Tooltip("무전 사용 시 코스트 누적 속도")]
    public float costRate = 1.0f;
    [Tooltip("무전 미사용 시 또는 범위 밖 감쇠 속도")]
    public float costDecayRate = 1.0f;

    private float currentWalkieCost = 0f;
    private bool isAct3 = false;

    // 서브 크리처 NoiseEnhancer 기믹용 코스트 배율
    private float costRateMultiplier = 1f;

    // 로그 중복 방지용
    private const float LogInterval = 1f;
    private float _logTimer = 0f;

    public void SetAct3(bool act3Active)
    {
        //3막 이벤트 발생 시 플래그 발동
        isAct3 = act3Active;
    }

    // 무전 코스트 누적 배율 설정
    public void SetCostRateMultiplier(float multiplier)
    {
        costRateMultiplier = Mathf.Max(0f, multiplier);
    }

    public void ResetCost()
    {
        //코스트 초기화
        currentWalkieCost = 0f;

        currentWalkieCost = 0f;
        _logTimer = 0f;
    }

    //매 프레임 호출되어 코스트를 누적/감쇠, 임계치 도달 시 true를 반환
    public bool ProcessWalkieCost(float deltaTime, Zone myZone, Vector3 creaturePos, out Vector3 targetWalkiePos)
    {
        targetWalkiePos = creaturePos;

        //상태 플래그
        bool isAccumulating = false;
        bool isDecaying = false;

        if (WalkieTalkieManager.Instance != null)
        {
            WalkieTalkieItem myZoneWalkie = WalkieTalkieManager.Instance.GetWalkieTalkieByZone(myZone);

            //수신 공간 위험 판정: 내 구역 무전기가 수신(RX) 중일 때
            if (myZoneWalkie != null && myZoneWalkie.NetWalkieState == WalkieState.RX)
            {
                Vector3 flatCreaturePos = new Vector3(creaturePos.x, 0f, creaturePos.z);
                Vector3 flatWalkiePos = new Vector3(myZoneWalkie.transform.position.x, 0f, myZoneWalkie.transform.position.z);

                float flatDist = Vector3.Distance(flatCreaturePos, flatWalkiePos);
                float yDiff = Mathf.Abs(creaturePos.y - myZoneWalkie.transform.position.y);

                //크리처가 무전기 범위 안인지 밖인지 먼저 판별
                if (flatDist <= 60f && yDiff <= 10f)
                {
                    //크리처 범위 안일 때
                    if (myZoneWalkie.NetWalkieState == WalkieState.RX)
                    {
                        //Accumulating 상태일 때 크리처 인식 범위 내 -> 코스트 누적
                        isAccumulating = true;
                        targetWalkiePos = myZoneWalkie.transform.position;
                    }

                    else
                    {
                        //무전을 안 하면 코스트 유지 (isAccumulating과 isDecaying 둘 다 false로 두어 변화량 0 처리)
                    }
                }

                //무전 중이나 인식 범위 밖
                else
                {
                    //크리처가 인식 범위 밖으로 이동 -> 코스트 감쇠
                    isDecaying = true;
                }
            }

            //무전 미사용 시
            else isDecaying = true;
        }

        //무전기가 아예 없을 때도 미사용으로 간주하여 감쇠
        else isDecaying = true;

        //증감량 계산 (Idle 0값 처리)
        float costChange = 0f;

        if (isAccumulating) costChange = costRate * deltaTime;

        //코스트가 남아있을 때만 감쇠
        else if (isDecaying && currentWalkieCost > 0f) costChange = -costDecayRate * deltaTime;

        //두 조건에 안 걸리면 amount는 0 유지 되어, 아래 연산에서 현재 보유량(currentWalkieCost)이 깎이지 않고 그대로 유지

        //막에 따른 최대 임계치 설정
        float currentMax = isAct3 ? costDangerThresholdFinal : costDangerThreshold;        
        currentWalkieCost += costChange;
        currentWalkieCost = Mathf.Clamp(currentWalkieCost, 0f, currentMax);

        // ── 로그 ──────────────────────────────────────────
        _logTimer += deltaTime;
        if (_logTimer >= LogInterval)
        {
            _logTimer = 0f;

            if (currentWalkieCost > 0f)
            {
                string status = isAccumulating ? "누적 중" : (isDecaying ? "감쇠 중" : "유지");
                Debug.Log(
                    $"[WalkieTracker] ({myZone}) {status} { (currentWalkieCost / currentMax * 100f):F0}%"
                );
            }
        }

        // 임계치 도달 시 로그 + 트리거
        bool triggered = currentWalkieCost >= currentMax;
        if (triggered)
        {
            Debug.Log(
                $"[WalkieTracker] ({myZone}) ★ 임계치 도달! " +
                $"코스트 {currentWalkieCost:F1} / {currentMax:F1} → 크리처 AlertMove 트리거"
            );
        }

        //임계치 도달 여부 반환
        return currentWalkieCost >= currentMax;
    }
}
