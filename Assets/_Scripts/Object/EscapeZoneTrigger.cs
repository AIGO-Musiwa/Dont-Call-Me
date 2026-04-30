using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;

[RequireComponent(typeof(BoxCollider))]
public class EscapeZoneTrigger : MonoBehaviour
{
    private HashSet<PlayerController> escapingPlayers = new HashSet<PlayerController>();

    private void OnTriggerEnter(Collider other)
    {
        PlayerController player = other.GetComponentInParent<PlayerController>();        

        //상태 권한이 있는 서버 측의 트리거 판정에서만 탈출 요청을 보냄
        if (player != null && player.Object != null)
        {
            //이미 탈출 진행 중인 플레이어면 무시
            if (escapingPlayers.Contains(player)) return;

            //아직 살아있거나 납치 중인 상태의 플레이어만 탈출 가능 (이미 탈출했거나 죽은 경우 무시)
            if (player.NetPlayerState == PlayerState.Normal || player.NetPlayerState == PlayerState.Captured)
            {
                escapingPlayers.Add(player);
                Debug.Log($"[{player.gameObject.name}] 탈출 트리거 밟음. 페이드 아웃 및 텔레포트를 시작.");

                if (player.Object.HasStateAuthority)
                {
                    //즉시 이동 및 시야 회전 정지
                    player.SetInputLock(true, true);
                    StartCoroutine(DelayedEscapeRoution(player));
                }

                if (player.Object.HasInputAuthority)
                {
                    if (ScreenFader.Instance != null) ScreenFader.Instance.FadeOut(2.5f);
                }
            }
        }
    }

    private IEnumerator DelayedEscapeRoution(PlayerController player)
    {
        Debug.Log($"[{player.gameObject.name}] 탈출 구역 진입! 즉시 이동을 정지하고 2.5초 페이드 아웃 대기...");

        //페이드 아웃이 완료될 때까지 2.5초 대기
        yield return new WaitForSeconds(2.5f);

        //2.5초 뒤 완전히 화면이 까맣게 된 시점에서 탈출 처리 실행
        if (player != null && player.Object != null && player.Object.IsValid) player.ServerEnterEscaped();
        Debug.Log($"[{player.gameObject.name}] 최종 탈출 성공 처리 완료!");
    }
}
