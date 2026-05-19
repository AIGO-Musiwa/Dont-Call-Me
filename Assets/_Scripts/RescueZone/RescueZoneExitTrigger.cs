using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(BoxCollider))]
public class RescueZoneExitTrigger : MonoBehaviour
{
    [Header("구역 설정")]
    [Tooltip("이 트리거가 배치된 맵의 구역을 설정해주세요 (ZoneA 또는 ZoneB)")]
    public Zone myZone;

    //다중 콜라이더 오작동 방지용 체류 인원 추적 딕셔너리
    private Dictionary<PlayerController, int> playerColliders = new Dictionary<PlayerController, int>();

    //다중 콜라이더 및 비활성화 오브젝트로 인한 오작동 방지용
    private HashSet<Collider> activeColliders = new HashSet<Collider>();

    private void OnTriggerEnter(Collider other)
    {
        //방을 빠져나간 오브젝트가 플레이어인지 확인
        PlayerController player = other.GetComponentInParent<PlayerController>();

        if (player != null)
        {
            //콜라이더 정리
            CleanUpColliders();

            bool wasEmpty = activeColliders.Count == 0;

            //신규 진입 플레이어 딕셔너리 등록
            if (!playerColliders.ContainsKey(player)) playerColliders[player] = 0;

            activeColliders.Add(other);

            //최초 진입 시 크리처 보호 시스템 가동
            if (wasEmpty && activeColliders.Count > 0) SetCreatureProtection(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        PlayerController player = other.GetComponentInParent<PlayerController>();

        if (player != null)
        {
            activeColliders.Remove(other);

            //콜라이더 정리
            CleanUpColliders();

            //안전 구역 내 체류 인원이 완전히 없을 경우 보호 해제 및 문 닫기
            if (activeColliders.Count == 0)
            {
                SetCreatureProtection(false);

                //인원이 0명이 되면 구출 구역 문을 닫고 퍼즐 리셋
                RescueZoneDoor.CloseAllDoorsInZone(myZone);
            }
        }
    }

    //유니티 물리 엔진 버그(트리거 안에서 콜라이더가 꺼질 때 OnTriggerExit가 호출되지 않는 현상)를 해결
    private void CleanUpColliders()
    {
        activeColliders.RemoveWhere(c => c == null || !c.gameObject.activeInHierarchy || !c.enabled);
    }

    private void SetCreatureProtection(bool isProtected)
    {
        CreatureAI[] allCreatures = FindObjectsByType<CreatureAI>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (CreatureAI creature in allCreatures)
        {
            if (creature.myZone == myZone)
            {
                if (isProtected) creature.ActivateRescueProtection();
                else creature.CancelRescueProtection();
            }
        }

        //if (isProtected) Debug.Log($"[RescueZone] 플레이어 진입! {myZone} 크리처의 시야 및 소리가 완벽히 차단됩니다.");
        //else Debug.Log($"[RescueZone] 모든 플레이어 이탈. {myZone} 크리처의 보호가 해제됩니다!");
    }
}
