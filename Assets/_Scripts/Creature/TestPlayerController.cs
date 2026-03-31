using UnityEngine;
using UnityEngine.InputSystem;

public class TestPlayerController : MonoBehaviour
{
    [Header("이동 설정")]
    public float walkSpeed = 5f;
    public float gravity = -9.81f;

    [Header("카메라 회전 설정")]
    public float mouseSensitivity = 0.5f;   //마우스 감도
    public Transform playerCamera;          //내 머리에 달린 카메라

    private CharacterController controller;
    private float cameraPitch = 0f;         //카메라 위아래 각도 기억용
    private Vector3 velocity;               //중력용 속도

    void Start()
    {
        controller = GetComponent<CharacterController>();

        //시작할 때 마우스 커서를 화면 중앙에 가두고 숨김
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        //시점 회전
        if (Mouse.current != null)
        {
            //마우스가 얼마나 움직였는지 값을 가져옴
            Vector2 mouseDelta = Mouse.current.delta.ReadValue();

            //좌우 회전
            transform.Rotate(Vector3.up * mouseDelta.x * mouseSensitivity);

            //위아래 회전
            cameraPitch -= mouseDelta.y * mouseSensitivity;
            cameraPitch = Mathf.Clamp(cameraPitch, -80f, 80f);

            //카메라에 각도 적용
            playerCamera.localEulerAngles = Vector3.right * cameraPitch;
        }

        //이동
        if (Keyboard.current != null)
        {
            float moveX = 0f;
            float moveZ = 0f;

            //키보드 입력을 확인
            if (Keyboard.current.wKey.isPressed) moveZ = 1f;
            if (Keyboard.current.sKey.isPressed) moveZ = -1f;
            if (Keyboard.current.aKey.isPressed) moveX = -1f;
            if (Keyboard.current.dKey.isPressed) moveX = 1f;

            //내가 바라보는 방향을 기준으로 앞뒤좌우 이동 벡터 생성
            Vector3 move = transform.right * moveX + transform.forward * moveZ;

            //이동 명령
            controller.Move(move.normalized * walkSpeed * Time.deltaTime);
        }

        //중력 적용
        if (controller.isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }
        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }
}