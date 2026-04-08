using Fusion;
using UnityEngine;

/// <summary>
/// 로컬 카메라 기준으로 상호작용 타겟을 찾고 캐싱한다.
/// 실제 상호작용 성립은 PlayerController의 RPC 요청 후 서버가 판정한다.
/// </summary>
public class PlayerInteraction : MonoBehaviour
{
    [Header("상호작용")]
    [SerializeField] private float interactDistance = 2f;
    [SerializeField] private LayerMask interactMask = ~0;
    [SerializeField] private bool drawDebugRay = true;

    private PlayerController _controller;
    private Camera _viewCamera;

    private IInteractable _currentInteractable;
    private NetworkObject _currentTargetObject;

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
    /// 로컬 플레이어가 현재 바라보는 상호작용 타겟을 매 프레임 갱신한다.
    /// </summary>
    private void Update()
    {
        if (_controller == null)
            return;

        if (!_controller.HasInputAuthority)
            return;

        if (_controller.NetPlayerState != PlayerState.Normal)
        {
            ClearTarget();
            return;
        }

        if (_controller.NetHideState != HideState.None)
        {
            ClearTarget();
            return;
        }

        if (_viewCamera == null && _controller.LookView != null)
            _viewCamera = _controller.LookView.ViewCamera;

        if (_viewCamera == null)
        {
            ClearTarget();
            return;
        }

        Ray ray = new Ray(_viewCamera.transform.position, _viewCamera.transform.forward);

        if (drawDebugRay)
            Debug.DrawRay(ray.origin, ray.direction * interactDistance, Color.green);

        if (Physics.Raycast(ray, out RaycastHit hit, InteractDistance, interactMask, QueryTriggerInteraction.Ignore))
        {
            if (TryFindInteractable(hit.collider.transform, out NetworkObject targetObject, out IInteractable interactable))
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
    /// 현재 캐싱된 상호작용 대상의 NetworkId를 반환한다.
    /// </summary>
    public bool TryGetCurrentTargetId(out NetworkId targetId)
    {
        if (_currentTargetObject != null)
        {
            targetId = _currentTargetObject.Id;
            return true;
        }

        targetId = default;
        return false;
    }

    /// <summary>
    /// 현재 캐싱된 상호작용 대상을 초기화한다.
    /// </summary>
    private void ClearTarget()
    {
        _currentTargetObject = null;
        _currentInteractable = null;
    }

    /// <summary>
    /// Hit된 콜라이더부터 부모를 따라 올라가며 NetworkObject와 IInteractable을 함께 찾는다.
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
