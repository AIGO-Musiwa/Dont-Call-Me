using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class RescueZoneExitTrigger : MonoBehaviour
{
    [Header("구역 설정")]
    [Tooltip("이 트리거가 배치된 맵의 구역을 설정해주세요 (ZoneA 또는 ZoneB)")]
    public Zone myZone;

    private void OnTriggerExit(Collider other)
    {
        //방을 빠져나간 오브젝트가 플레이어인지 확인
        PlayerController player = other.GetComponentInParent<PlayerController>();

        if (player != null)
        {
            //플레이어가 속한 구역이 이 트리거의 구역과 다르면 무시
            if (player.NetZone != myZone) return;

            //씬에 존재하는 모든 크리처 AI를 찾음
            CreatureAI[] allCreature = FindObjectsByType<CreatureAI>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            //찾은 크리처 중 '같은 구역'의 크리처에게만 보호 해제 명령을 내림
            foreach (CreatureAI creature in allCreature)
            {
                //타이머가 0일 때는 내부에서 자동으로 무시
                if (creature.myZone == myZone) creature.CancelRescueProtection();
            }

            Debug.Log($"[RescueZone] 플레이어({player.gameObject.name})가 {myZone} 구출 구역을 이탈했습니다.");
        }
    }

    //인스펙터에서 우클릭하여 수동으로 10초 타이머를 발동시키는 테스트용 함수
    [ContextMenu("Debug/Activate Rescue Protection (10s)")]
    private void DebugActivateProtection()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[RescueZone] 플레이 모드에서만 작동합니다.");
            return;
        }

        CreatureAI[] allCreatures = FindObjectsByType<CreatureAI>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        foreach (CreatureAI creature in allCreatures)
        {
            if (creature.myZone == myZone)
            {
                creature.ActivateRescueProtection();
            }
        }
        Debug.Log($"[Debug] {myZone} 구역의 크리처 보호 시스템(10초)을 강제로 가동했습니다!");
    }
}
