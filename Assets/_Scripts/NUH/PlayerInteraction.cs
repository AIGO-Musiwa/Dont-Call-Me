using Fusion;
using UnityEngine;

/// <summary>
/// 로컬 플레이어의 시야 기준 Transform으로 상호작용 타겟을 찾고 캐싱한다.
/// 실제 상호작용 성립은 PlayerController의 RPC 요청 후 서버가 판정한다.
/// </summary>
public class PlayerInteraction : MonoBehaviour
{
    [Header("상호작용")]
    [SerializeField] private float interactDistance = 2f;       // 공통 상호작용 거리
    [SerializeField] private LayerMask interactMask = ~0;       // 상호작용 레이캐스트 대상 레이어
    [SerializeField] private bool drawDebugRay = true;          // 디버그 레이 표시 여부

    private PlayerController _controller;
    private Transform _viewOrigin;                              // 현재 상호작용 Raycast 기준 Transform

    private IInteractable _currentInteractable;                 // 현재 바라보는 상호작용 대상 인터페이스
    private NetworkObject _currentTargetObject;                 // 현재 바라보는 상호작용 대상 루트 NetworkObject
    private int _currentInteractableId = -1;                    // 현재 자식 상호작용 ID

    public float InteractDistance => interactDistance;
    public bool HasValidTarget => _currentInteractable != null && _currentTargetObject != null;
    public string CurrentPromptText => HasValidTarget ? _currentInteractable.GetPromptText(_controller) : string.Empty;

    /// <summary>
    /// PlayerController에서 생성 시 호출되어 시야 기준 Transform을 연결한다.
    /// </summary>
    public void Initialize(PlayerController controller)
    {
        _controller = controller;
        RefreshViewOriginReference();
    }

    /// <summary>
    /// 매 프레임 현재 바라보는 상호작용 대상을 찾는다.
    /// </summary>
    private void Update()
    {
        if (!CanSearchInteractable())
        {
            ClearTarget();
            return;
        }

        RefreshViewOriginReference();

        if (_viewOrigin == null)
        {
            ClearTarget();
            return;
        }

        Ray ray = new Ray(_viewOrigin.position, _viewOrigin.forward);

        if (drawDebugRay)
            Debug.DrawRay(ray.origin, ray.direction * interactDistance, Color.green);

        if (Physics.Raycast(ray, out RaycastHit hit, InteractDistance, interactMask, QueryTriggerInteraction.Collide))
        {
            if (TryFindInteractable(hit.collider.transform, out NetworkObject targetObject, out IInteractable interactable, out int interactableId))
            {
                if (interactable.CanInteract(_controller))
                {
                    _currentTargetObject = targetObject;
                    _currentInteractable = interactable;
                    _currentInteractableId = interactableId;
                    return;
                }
            }
        }

        ClearTarget();
    }

    /// <summary>
    /// 현재 캐싱된 상호작용 대상의 루트 NetworkId와 자식 상호작용 ID 반환.
    /// </summary>
    public bool TryGetCurrentTargetInfo(out NetworkId targetId, out int interactableId)
    {
        if (_currentTargetObject != null)
        {
            targetId = _currentTargetObject.Id;
            interactableId = _currentInteractableId;
            return true;
        }

        targetId = default;
        interactableId = -1;
        return false;
    }

    private void ClearTarget()
    {
        _currentTargetObject = null;
        _currentInteractable = null;
        _currentInteractableId = -1;
    }

    private bool CanSearchInteractable()
    {
        if (_controller == null)
            return false;

        if (!_controller.HasInputAuthority)
            return false;

        if (_controller.NetPlayerState != PlayerState.Normal)
            return false;

        return true;
    }

    /// <summary>
    /// PlayerLookView에서 현재 게임플레이 시야 기준 Transform을 받아온다.
    /// Camera 컴포넌트가 아니라 Transform을 기준으로 사용해 ViewModelCamera와 MainCamera 의존을 분리한다.
    /// </summary>
    private void RefreshViewOriginReference()
    {
        if (_controller != null && _controller.LookView != null)
            _viewOrigin = _controller.LookView.ViewOrigin;
    }

    /// <summary>
    /// Hit된 Transform부터 부모 방향으로 올라가며
    /// 루트 NetworkObject, IInteractable, 자식 상호작용 ID를 함께 찾는다.
    /// </summary>
    public static bool TryFindInteractable(
        Transform start,
        out NetworkObject targetObject,
        out IInteractable interactable,
        out int interactableId)
    {
        targetObject = start.GetComponentInParent<NetworkObject>();
        interactable = null;
        interactableId = -1;

        MonoBehaviour[] behaviours = start.GetComponentsInParent<MonoBehaviour>(true);
        foreach (var behaviour in behaviours)
        {
            if (behaviour is IInteractable foundInteractable)
            {
                interactable = foundInteractable;

                if (behaviour is IChildPuzzleInteractable childPuzzleInteractable)
                    interactableId = childPuzzleInteractable.InteractableId;

                break;
            }
        }

        return targetObject != null && interactable != null;
    }
}
