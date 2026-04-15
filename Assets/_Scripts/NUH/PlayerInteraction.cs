using Fusion;
using UnityEngine;

/// <summary>
/// 로컬 카메라 기준으로 상호작용 타겟을 찾고 캐싱한다.
/// 실제 상호작용 성립은 PlayerController의 RPC 요청 후 서버가 판정한다.
/// </summary>
public class PlayerInteraction : MonoBehaviour
{
    [Header("상호작용")]
    [SerializeField] private float interactDistance = 2f;       // 공통 상호작용 거리
    [SerializeField] private LayerMask interactMask = ~0;       // 상호작용 레이캐스트 대상 레이어
    [SerializeField] private bool drawDebugRay = true;          // 디버그 레이 표시 여부

    private PlayerController _controller;
    private Camera _viewCamera;                                 // 현재 상호작용 기준 카메라

    private IInteractable _currentInteractable;                 // 현재 바라보는 상호작용 대상 인터페이스
    private NetworkObject _currentTargetObject;                 // 현재 바라보는 상호작용 대상 NetworkObject

    public float InteractDistance => interactDistance;
    public bool HasValidTarget => _currentInteractable != null && _currentTargetObject != null;
    public string CurrentPromptText => HasValidTarget ? _currentInteractable.GetPromptText(_controller) : string.Empty;

    /// <summary>
    /// PlayerController에서 생성 시 호출되어 카메라 참조를 연결한다.
    /// </summary>
    public void Initialize(PlayerController controller)
    {
        _controller = controller;

        if (_controller != null && _controller.LookView != null)
            _viewCamera = _controller.LookView.ViewCamera;
    }

    /// <summary>
    ///  매 프레임 현재 바라보는 상호작용 대상을 찾는다.
    /// </summary>
    private void Update()
    {
        if (!CanSearchInteractable())
        {
            ClearTarget();
            return;
        }

        TryRefreshCameraReference();

        if(_viewCamera == null)
        {
            ClearTarget();
            return;
        }

        Ray ray = new Ray(_viewCamera.transform.position, _viewCamera.transform.forward);

        if (drawDebugRay)
            Debug.DrawRay(ray.origin, ray.direction * interactDistance, Color.green);

        if(Physics.Raycast(ray, out RaycastHit hit, InteractDistance, interactMask, QueryTriggerInteraction.Ignore))
        {
            if(TryFindInteractable(hit.collider.transform, out NetworkObject targetObject, out IInteractable interactable))
            {
                if (interactable.CanInteract(_controller))
                {
                    _currentTargetObject = targetObject;
                    _currentInteractable = interactable;
                    return;
                }
            }
        }

        ClearTarget();
    }

    /// <summary>
    /// 현재 캐싱된 상호작용 대상의 NetworkId 반환
    /// RPC 요청이나 서버 검증용으로 사용 가능
    /// </summary>
    public bool TryGetCurrentTargetId(out NetworkId targetId)
    {
        if(_currentTargetObject != null)
        {
            targetId = _currentTargetObject.Id;
            return true;
        }

        targetId = default;
        return false;
    }


    private void ClearTarget()
    {
        _currentTargetObject = null;
        _currentInteractable = null;
    }

    private bool CanSearchInteractable()
    {
        if (_controller == null)
            return false;

        if (!_controller.HasInputAuthority)
            return false;

        // 플레이어 상태가 Normal이 아니면 상호작용 대상 탐색 안함
        if (_controller.NetPlayerState != PlayerState.Normal)
            return false;

        return true;
    }

    /// <summary>
    /// 카메라 참조가 비어있으면 LookView에서 다시 받아옴
    /// </summary>
    private void TryRefreshCameraReference()
    {
        if (_viewCamera == null && _controller != null && _controller.LookView != null)
            _viewCamera = _controller.LookView.ViewCamera;
    }

    /// <summary>
    /// Hit된 Transform부터 부모 방향으로 올라가며
    /// NetworkObject와 IInteractable을 함께 찾는다.
    /// </summary>
    public static bool TryFindInteractable(Transform start, out NetworkObject targetObject, out IInteractable interactable)
    {
        targetObject = start.GetComponentInParent<NetworkObject>();
        interactable = null;

        MonoBehaviour[] behaviours = start.GetComponentsInParent<MonoBehaviour>(true);
        foreach (var behaviour in behaviours)
        {
            if (behaviour is IInteractable found)
            {
                interactable = found;
                break;
            }
        }

        return targetObject != null && interactable != null;
    }
}
