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

    public void SetAct3(bool act3Active)
    {
        //3막 이벤트 발생 시 플래그 발동
        isAct3 = act3Active;
    }

    public void ResetCost()
    {
        //코스트 초기화
        currentWalkieCost = 0f;
    }

    //매 프레임 호출되어 코스트를 누적/감쇠, 임계치 도달 시 true를 반환
    public bool ProcessWalkieCost(float deltaTime, Zone myZone, Vector3 creaturePos, out Vector3 targetWalkiePos)
    {
        targetWalkiePos = creaturePos;
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

                //수평 반경 60m, 수직 반경 10m 이내인지 확인
                if (flatDist <= 60f && yDiff <= 10f)
                {
                    //Accumulating 상태일 때 크리처 인식 범위 내 -> 코스트 누적
                    isAccumulating = true;
                    targetWalkiePos = myZoneWalkie.transform.position;
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
        float amount = 0f;

        if (isAccumulating) amount = costRate * deltaTime;

        //코스트가 남아있을 때만 감쇠
        else if (isDecaying && currentWalkieCost > 0f) amount = -costDecayRate * deltaTime;

        //두 조건에 안 걸리면 amount는 0 유지 (Idle 상태: 코스트 변화 없음)

        //막에 따른 최대 임계치 설정
        float currentMax = isAct3 ? costDangerThresholdFinal : costDangerThreshold;        
        currentWalkieCost += amount;
        currentWalkieCost = Mathf.Clamp(currentWalkieCost, 0f, currentMax);

        //임계치 도달 여부 반환
        return currentWalkieCost >= currentMax;
    }
}
