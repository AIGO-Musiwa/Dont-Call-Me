using Fusion;
using UnityEngine;

/// <summary>
/// 캐비닛 문 열기/닫기 처리
/// IInteractable 구현 — 좌클릭으로 문 토글
///
/// 숨기 규칙:
///   문이 닫힌 상태 = 이동 불가 (CC 비활성화)
///   문이 열린 상태 = 자유 이동 가능
///   문 닫힘/열림은 캐비닛 안에 있는 플레이어에게만 적용
/// </summary>
public class CabinetDoor : NetworkBehaviour, IInteractable
{
    // ─────────────────────────────────────────
    // 네트워크 동기화 변수
    // ─────────────────────────────────────────
    [Networked, OnChangedRender(nameof(OnDoorStateChanged))]
    public bool IsDoorClosed { get; private set; }

    [Networked]
    public PlayerRef Occupant { get; private set; }  // 캐비닛 안에 있는 플레이어

    // ─────────────────────────────────────────
    // Inspector 설정값
    // ─────────────────────────────────────────
    [Header("캐비닛 설정")]
    [SerializeField] private Transform insidePoint;    // 진입 시 플레이어 이동 위치
    [SerializeField] private Transform doorPivot;      // 문 회전 피벗
    [SerializeField] private float     doorOpenAngle  = 90f;
    [SerializeField] private float     doorAnimSpeed  = 5f;

    // ─────────────────────────────────────────
    // 로컬 변수
    // ─────────────────────────────────────────
    private float _targetAngle;
    private float _currentAngle;

    // ─────────────────────────────────────────
    // IInteractable 구현
    // ─────────────────────────────────────────

    /// <summary>
    /// 상호작용 가능 조건:
    /// 1. 아직 캐비닛이 비어있고 플레이어가 밖에 있는 경우 (진입)
    /// 2. 플레이어가 이미 안에 있는 경우 (문 토글)
    /// </summary>
    public bool CanInteract(PlayerController actor)
    {
        if (actor.CurrentState != PlayerState.Normal) return false;

        bool isEmpty   = Occupant == PlayerRef.None;
        bool isOccupant = Occupant == actor.Object.InputAuthority;

        return isEmpty || isOccupant;
    }

    public void OnInteract(PlayerController actor)
    {
        if (!CanInteract(actor)) return;

        bool isEmpty = Occupant == PlayerRef.None;

        if (isEmpty)
        {
            // 진입 — 문 닫기
            RPC_EnterCabinet(actor.Object.InputAuthority);
        }
        else
        {
            // 안에 있는 플레이어가 상호작용 — 문 토글
            RPC_ToggleDoor();
        }
    }

    public string GetPromptText()
    {
        if (Occupant == PlayerRef.None) return "캐비닛 진입";
        return IsDoorClosed ? "문 열기" : "문 닫기";
    }

    // ─────────────────────────────────────────
    // 진입 / 퇴장
    // ─────────────────────────────────────────

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_EnterCabinet(PlayerRef playerRef)
    {
        Occupant = playerRef;

        // 플레이어를 insidePoint로 이동
        NetworkObject playerObj = Runner.GetPlayerObject(playerRef);
        if (playerObj != null && insidePoint != null)
        {
            playerObj.transform.position = insidePoint.position;
            playerObj.transform.rotation = insidePoint.rotation;
        }

        // 진입 시 문 닫기
        SetDoorClosed(true);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_ToggleDoor()
    {
        SetDoorClosed(!IsDoorClosed);

        // 문이 열리면 캐비닛 퇴장 처리
        if (!IsDoorClosed)
            ExitCabinet();
    }

    private void SetDoorClosed(bool closed)
    {
        IsDoorClosed = closed;

        // Occupant의 이동 잠금 처리
        if (Occupant == PlayerRef.None) return;

        NetworkObject playerObj = Runner.GetPlayerObject(Occupant);
        if (playerObj == null) return;

        FirstPersonController fpc = playerObj.GetComponent<FirstPersonController>();
        fpc?.SetCCEnabled(!closed);  // 문 닫힘 = CC 비활성화, 문 열림 = CC 활성화
    }

    private void ExitCabinet()
    {
        // CC 복구 후 Occupant 해제
        if (Occupant != PlayerRef.None)
        {
            NetworkObject playerObj = Runner.GetPlayerObject(Occupant);
            FirstPersonController fpc = playerObj?.GetComponent<FirstPersonController>();
            fpc?.SetCCEnabled(true);
        }

        Occupant = PlayerRef.None;
    }

    // ─────────────────────────────────────────
    // 포획 시 강제 퇴장
    // ─────────────────────────────────────────

    /// <summary>
    /// CaptureSystem에서 포획 시 호출
    /// 캐비닛 안에 있는 플레이어가 잡힐 경우 강제 퇴장
    /// </summary>
    public void ForceExit()
    {
        if (!Runner.IsServer) return;
        SetDoorClosed(false);
        ExitCabinet();
    }

    // ─────────────────────────────────────────
    // 문 애니메이션 ([Networked] 변경 감지)
    // ─────────────────────────────────────────
    private void OnDoorStateChanged()
    {
        _targetAngle = IsDoorClosed ? 0f : doorOpenAngle;
    }

    private void Update()
    {
        if (doorPivot == null) return;

        _currentAngle = Mathf.LerpAngle(
            _currentAngle,
            _targetAngle,
            Time.deltaTime * doorAnimSpeed
        );

        doorPivot.localRotation = Quaternion.Euler(0f, _currentAngle, 0f);
    }
}

/// <summary>
/// 캐비닛 숨기 상태 추적용 컴포넌트
/// PlayerController.IsHiding()에서 참조
/// CabinetDoor와 같은 캐비닛 GameObject 혹은
/// 플레이어 GameObject에 부착 가능
/// </summary>
public class CabinetHideState : NetworkBehaviour
{
    [Networked] public bool IsInsideCabinet { get; set; }
}
