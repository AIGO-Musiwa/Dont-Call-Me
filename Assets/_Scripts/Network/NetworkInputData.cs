using Fusion;
using UnityEngine;

public struct NetworkInputData : INetworkInput
{
    // WASD - 플레이어 이동 방향
    public Vector2 MoveDirection;

    // LShift - 달리기
    public NetworkBool IsSprinting;

    // LCtrl - 앉기
    public NetworkBool IsCrouching;

    // 상호작용 (퍼즐/오브젝트, 아이템, 다른 플레이어)
    public NetworkBool IsInteractPressed;

    // 우클릭 - PTT 송신
    public NetworkBool IsPTTPressed;
}
