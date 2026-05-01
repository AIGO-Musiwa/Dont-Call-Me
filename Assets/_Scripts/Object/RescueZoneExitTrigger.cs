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

    private void OnTriggerEnter(Collider other)
    {
        //방을 빠져나간 오브젝트가 플레이어인지 확인
        PlayerController player = other.GetComponentInParent<PlayerController>();

        if (player != null && player.NetZone == myZone)
        {
            //신규 진입 플레이어 딕셔너리 등록
            if (!playerColliders.ContainsKey(player)) playerColliders[player] = 0;

            //진입한 콜라더 개수 누적
            playerColliders[player]++;

            //최초 진입 시 크리처 보호 시스템 가동
            if (playerColliders.Count == 1 && playerColliders[player] == 1) SetCreatureProtection(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        PlayerController player = other.GetComponentInParent<PlayerController>();

        if (player != null && player.NetZone == myZone)
        {
            if (playerColliders.ContainsKey(player))
            {
                //이탈한 콜라이더 개수 차감
                playerColliders[player]--;

                //해당 플레이어의 모든 콜라이더 이탈 시 딕셔너리에서 제거
                if (playerColliders[player] <= 0)
                {
                    playerColliders.Remove(player);
                }

                //안전 구역 내 체류 인원이 없을 경우 보호 해제
                if (playerColliders.Count == 0)
                {
                    SetCreatureProtection(false);

                    //인원이 0명이 되면 구출 구역 문을 무조건 닫음
                    RescueZoneDoor.CloseAllDoorsInZone(myZone);
                }
            }
        }
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

        if (isProtected) Debug.Log($"[RescueZone] 플레이어 진입! {myZone} 크리처의 시야 및 소리가 완벽히 차단됩니다.");
        else Debug.Log($"[RescueZone] 모든 플레이어 이탈. {myZone} 크리처의 보호가 해제됩니다!");
    }

    //인스펙터에서 우클릭하여 수동으로 10초 타이머를 발동시키는 테스트용 함수
    [ContextMenu("Debug/Activate Rescue Protection (10s)")]
    private void DebugActivateProtection()
    {
        if (!Application.isPlaying) return;

        SetCreatureProtection(true);
        Debug.Log($"[Debug] {myZone} 구역의 크리처 보호 시스템(10초)을 강제로 가동했습니다!");
    }
}
