using Fusion;
using UnityEngine;

/// <summary>
/// 플레이어 루트 컴포넌트
/// 같은 GameObject에 부착된 모든 플레이어 시스템의 참조를 중앙 관리
/// 외부 스크립트는 PlayerController를 통해 각 시스템에 접근
/// 
/// 부착 컴포넌트 목록 : 
/// - FirstPersonController  — 이동/시야/앉기
/// - InteractionSystem      — 레이캐스트 상호작용
///   HandSystem             — 손 슬롯 관리
///   RoleSystem             — 역할/건물 배정
///   CaptureSystem          — 포획/구출/사망 상태
///   DeathGauge             — 사망 게이지
///   SpectatorSystem        — 관전 모드
/// </summary>
[RequireComponent(typeof(FirstPersonController))]
//[RequireComponent(typeof(InteractionSystem))]
//[RequireComponent(typeof(HandSystem))]
//[RequireComponent(typeof(RoleSystem))]
//[RequireComponent(typeof(CaptureSystem))]
//[RequireComponent(typeof(DeathGauge))]
public class PlayerController : NetworkBehaviour
{
    // 컴포넌트 참조 (캐싱)
    public FirstPersonController FPController { get; private set; }
    //public InteractionSystem Interaction { get; private set; }
    //public HandSystem Hand { get; private set; }
    //public RoleSystem Role { get; private set; }
    //public CaptureSystem Capture { get; private set; }
    //public DeathGauge DeathGauge { get; private set; }

    // 네트워크 동기화 변수
    //[Networked] public PlayerState CurrentState { get; set; }

    // Fusion 생명주기
    public override void Spawned()
    {
        FPController = GetComponent<FirstPersonController>();
        //Interaction = GetComponent<InteractionSystem>();
        //Hand = GetComponent<HandSystem>();
        //Role = GetComponent<RoleSystem>();
        //Capture = GetComponent<CaptureSystem>();
        //DeathGauge = GetComponent<DeathGauge>();
        //
        //CurrentState = PlayerState.Normal;
    }

    // 외부 참조 메서드
    public Transform GetCameraLightRoot()
    {
        return FPController != null ? FPController.GetCameraLightRoot() : null;
    }

    //public bool IsHiding()
    //{
        //return CurrentState == PlayerState.Normal &&
            //(GetComponent<CabinetHideState>()?.IsInsideCabinet == true ||
            //GetComponent<DeskHideZone>()?.IsHiding == true);
    //}

    //public bool CanInteract()
    //{
    //    return CurrentState == PlayerState.Normal;
    //}
}
