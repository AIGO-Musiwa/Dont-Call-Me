using Fusion;
using UnityEngine;
using UnityEngine.AI;

public class StairTeleporter : MonoBehaviour
{
    [Header("이동할 도착 지점")]
    public Transform destination;

    [Header("대상의 레이어 이름")]
    public string playerLayerName = "Player";
    public string creatureLayerName = "Creature";

    private void OnTriggerEnter(Collider other)
    {
        if (destination == null) return;

        //플레이어 이동 처리
        PlayerKCCMotor playerMotor = other.GetComponent<PlayerKCCMotor>();
        if (playerMotor == null) playerMotor = other.GetComponentInParent<PlayerKCCMotor>();

        if (playerMotor != null)
        {
            //네트워크 권한 확인을 위해 NetworkObject 컴포넌트를 직접 가져옴
            NetworkObject networkObject = other.GetComponent<NetworkObject>();
            if (networkObject == null) networkObject = other.GetComponentInParent<NetworkObject>();

            //서버 권한을 가진 쪽에서만 물리적 텔레포트 명령을 내려 롤백 방지
            if (networkObject != null && networkObject.HasStateAuthority)
            {
                //강제 이동을 막으므로 잠시 비활성화
                CharacterController cc = playerMotor.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;

                if (playerMotor.KCC != null)
                {
                    //KCC 내부 좌표 및 시야 회전 텔레포트
                    playerMotor.KCC.SetPosition(destination.position);
                    playerMotor.KCC.SetLookRotation(destination.rotation.eulerAngles.x, destination.rotation.eulerAngles.y);
                }

                //NetWorkTransform 강제 텔레포트
                NetworkTransform networkTransform = networkObject.GetComponent<NetworkTransform>();
                if (networkTransform != null) networkTransform.Teleport(destination.position);

                //시각적 잔상 방지를 위한 위치와 회전값 강제 동기화
                playerMotor.transform.position = destination.position;
                playerMotor.transform.rotation = destination.rotation;

                //비활성화했던 CharacterController 다시 활성화
                if (cc != null) cc.enabled = true;
                Debug.Log("플레이어 층간 이동 완료");                
            }
            return;
        }

        //크리처 이동 처리
        NavMeshAgent agent = other.GetComponent<NavMeshAgent>();
        if (agent == null) agent = other.GetComponentInParent<NavMeshAgent>();

        if (agent != null)
        {
            //경로와 속도 초기화
            agent.isStopped = true;
            agent.ResetPath();
            agent.velocity = Vector3.zero;

            //Creature 순간 이동
            agent.Warp(destination.position);

            //Creature 내부 가상 좌표를 실제 좌표와 강제 동기화하여 층간 미끄러짐 방지
            agent.nextPosition = destination.position;
            agent.velocity = Vector3.zero;

            //회전 및 재시작
            agent.transform.rotation = destination.rotation;
            agent.isStopped = false;

            Debug.Log("Creature 층간 이동 완료");
        }
    }
}