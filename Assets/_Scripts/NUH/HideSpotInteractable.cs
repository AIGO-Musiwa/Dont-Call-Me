using Fusion;
using UnityEngine;

/// <summary>
/// 캐비넷 / 책상 은신 포인트 공통 상호작용 스크립트.
/// 숨을 때는 아이템을 드랍하지 않고, 좌클릭으로 즉시 입장 / 퇴장한다.
/// 
/// 캐비넷 전용 표시 규칙:
/// - 아무도 숨어 있지 않으면 열린 캐비넷 모델을 켠다.
/// - 누군가 숨어 있으면 닫힌 캐비넷 모델을 켠다.
/// - 책상 HideSpot은 모델 토글을 하지 않는다.
/// </summary>
public class HideSpotInteractable : NetworkBehaviour, IInteractable
{
    [Header("은신 포인트")]
    [SerializeField] private HideState hideType = HideState.Cabinet;
    [SerializeField] private Transform enterPoint;
    [SerializeField] private Transform exitPoint;

    [Header("캐비넷 전용 모델 표시")]
    [SerializeField] private GameObject openCabinetVisual;   // 비어 있을 때 켜질 열린 캐비넷 모델
    [SerializeField] private GameObject closeCabinetVisual;  // 점유 중일 때 켜질 닫힌 캐비넷 모델

    [Header("디버그")]
    [SerializeField] private bool enableDebugLog = false;    // 디버그 로그 출력 여부

    [Networked, OnChangedRender(nameof(OnOccupiedChanged))]
    public NetworkBool NetIsOccupied { get; private set; }   // 현재 은신처 점유 여부

    [Networked]
    public PlayerRef NetOccupant { get; private set; }       // 현재 은신처를 점유 중인 플레이어

    /// <summary>
    /// 은신 포인트가 스폰될 때 점유 상태를 초기화하고,
    /// 현재 점유 상태에 맞춰 캐비넷 모델 표시를 갱신한다.
    /// </summary>
    public override void Spawned()
    {
        if (HasStateAuthority)
        {
            NetIsOccupied = false;
            NetOccupant = PlayerRef.None;
        }

        RefreshCabinetVisual();
    }

    /// <summary>
    /// 현재 플레이어가 이 은신 포인트와 상호작용 가능한지 검사한다.
    /// 비어 있으면 입장 가능하고, 이미 이 은신처 안에 들어간 본인만 퇴장 가능하다.
    /// </summary>
    public bool CanInteract(PlayerController actor)
    {
        if (actor == null)
            return false;

        if (actor.NetPlayerState != PlayerState.Normal)
            return false;

        if (actor.NetHideState == HideState.None)
            return !NetIsOccupied;

        return actor.NetCurrentHideSpotId == Object.Id;
    }

    /// <summary>
    /// 빈 은신처라면 입장시키고,
    /// 현재 이 은신처 안에 있는 플레이어라면 퇴장시킨다.
    /// </summary>
    public void Interact(PlayerController actor)
    {
        if (!HasStateAuthority)
            return;

        if (!CanInteract(actor))
            return;

        if (actor.NetHideState == HideState.None)
        {
            ServerTryEnter(actor);
            return;
        }

        if (actor.NetCurrentHideSpotId == Object.Id)
            ServerTryExit(actor);
    }

    /// <summary>
    /// 플레이어 컨트롤러가 숨은 상태에서 좌클릭을 감지했을 때,
    /// 현재 숨고 있는 은신처에 퇴장을 요청하기 위한 진입점이다.
    /// 퇴장 실제 처리 책임은 HideSpotInteractable이 가진다.
    /// </summary>
    public bool RequestExit(PlayerController actor)
    {
        if (!HasStateAuthority)
            return false;

        return ServerTryExit(actor);
    }

    /// <summary>
    /// 현재 은신 포인트 상호작용 프롬프트를 반환한다.
    /// </summary>
    public string GetPromptText(PlayerController actor)
    {
        if (actor == null)
            return string.Empty;

        if (actor.NetHideState == HideState.None)
            return hideType == HideState.Desk ? "책상 아래 숨기" : "캐비넷에 숨기";

        if (actor.NetCurrentHideSpotId == Object.Id)
            return hideType == HideState.Desk ? "책상 아래에서 나오기" : "캐비넷에서 나오기";

        return string.Empty;
    }

    /// <summary>
    /// 플레이어를 은신 포인트 안으로 입장시킨다.
    /// 입장 성공 시 은신처 점유 상태도 함께 갱신한다.
    /// </summary>
    private bool ServerTryEnter(PlayerController actor)
    {
        if (actor == null)
            return false;

        if (NetIsOccupied)
            return false;

        Vector3 targetPosition = enterPoint != null ? enterPoint.position : transform.position;
        Quaternion targetRotation = enterPoint != null ? enterPoint.rotation : transform.rotation;

        if (!actor.ServerEnterHide(hideType, Object, targetPosition, targetRotation))
            return false;

        NetIsOccupied = true;
        NetOccupant = actor.Object.InputAuthority;

        RefreshCabinetVisual();

        Log($"은신 입장 | HideType={hideType} | Occupant={NetOccupant}");
        return true;
    }

    /// <summary>
    /// 플레이어를 은신 포인트 밖으로 퇴장시킨다.
    /// 퇴장 성공 시 은신처 점유 상태를 해제한다.
    /// </summary>
    public bool ServerTryExit(PlayerController actor)
    {
        if (actor == null)
            return false;

        if (!NetIsOccupied)
            return false;

        if (actor.NetCurrentHideSpotId != Object.Id)
            return false;

        Vector3 targetPosition = exitPoint != null ? exitPoint.position : transform.position;
        Quaternion targetRotation = exitPoint != null ? exitPoint.rotation : transform.rotation;

        if (!actor.ServerExitHide(targetPosition, targetRotation))
            return false;

        NetIsOccupied = false;
        NetOccupant = PlayerRef.None;

        RefreshCabinetVisual();

        Log($"은신 퇴장 | HideType={hideType}");
        return true;
    }

    /// <summary>
    /// NetIsOccupied 변경 시 모든 클라이언트에서 캐비넷 모델 표시를 갱신한다.
    /// </summary>
    private void OnOccupiedChanged()
    {
        RefreshCabinetVisual();
    }

    /// <summary>
    /// 캐비넷 타입일 때만 현재 점유 상태에 맞춰 열린/닫힌 모델을 갱신한다.
    /// 책상 타입은 모델 토글을 하지 않는다.
    /// </summary>
    private void RefreshCabinetVisual()
    {
        if (hideType != HideState.Cabinet)
            return;

        ApplyCabinetVisualState(NetIsOccupied);
    }

    /// <summary>
    /// 캐비넷 모델 표시 상태를 실제로 적용한다.
    /// occupied가 true면 닫힌 캐비넷을 켜고,
    /// false면 열린 캐비넷을 켠다.
    /// </summary>
    private void ApplyCabinetVisualState(bool occupied)
    {
        if (openCabinetVisual != null)
            openCabinetVisual.SetActive(!occupied);

        if (closeCabinetVisual != null)
            closeCabinetVisual.SetActive(occupied);
    }

    /// <summary>
    /// 현재 은신처의 퇴장 위치/회전을 외부에서 읽어올 수 있게 제공한다.
    /// 필요 시 디버그나 보조 로직에서 사용할 수 있다.
    /// </summary>
    public void GetExitPose(out Vector3 targetPosition, out Quaternion targetRotation)
    {
        targetPosition = exitPoint != null ? exitPoint.position : transform.position;
        targetRotation = exitPoint != null ? exitPoint.rotation : transform.rotation;
    }

    /// <summary>
    /// 일반 디버그 로그를 출력한다.
    /// </summary>
    private void Log(string message)
    {
        if (!enableDebugLog)
            return;

        Debug.Log($"[HideSpotInteractable] {message}", this);
    }
}