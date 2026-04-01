using Fusion;
using Unity.VisualScripting;
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
        if(other.gameObject.layer == LayerMask.NameToLayer(playerLayerName))
        {
            CharacterController cc = other.GetComponent<CharacterController>();

            if(cc != null)
            {
                //플레이어를 도착 지점으로 순간 이동
                cc.enabled = false;
                other.transform.position = destination.position;

                //도착 지점의 회전값도 복사
                other.transform.rotation = destination.rotation;

                cc.enabled = true;
                Debug.Log("플레이어 층간 이동 완료");
            }            
        }

        else if (other.gameObject.layer == LayerMask.NameToLayer(creatureLayerName))
        {
            NavMeshAgent agent = other.GetComponent<NavMeshAgent>();
            if (agent != null)
            {
                //Creature를 도착 지점으로 순간 이동
                agent.Warp(destination.position);
                other.transform.rotation = destination.rotation;
                Debug.Log("Creature 층간 이동 완료");
            }
        }
    }  
}
