using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

public class CreatureTestMove : MonoBehaviour
{
    private NavMeshAgent agent; //NavMeshAgent 컴포넌트 변수

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    // Update is called once per frame
    void Update()
    {
        //마우스가 연결되어 있는지 확인, 마우스 왼쪽 버튼이 이번 프레임에 눌렸는지 확인
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            //현 마우스 커서의 화면 좌표를 읽어옴
            Vector2 mousePos = Mouse.current.position.ReadValue();

            Ray ray = Camera.main.ScreenPointToRay(mousePos); //카메라에서 마우스 위치를 향해 레이를 쏨
            RaycastHit hit;

            //레이가 바닥에 닿으면 크리쳐 이동 명령
            if (Physics.Raycast(ray, out hit))
            {
                //NavMeshAgent에게 이동할 위치를 설정
                agent.SetDestination(hit.point);
            }
        }
    }
}
