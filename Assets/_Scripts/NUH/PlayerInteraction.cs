using Fusion;
using UnityEngine;

/// <summary>
/// 로컬 카메라 기준 Raycast
/// 현재 타겟 캐싱
/// 프롬프트 문자열 제공
/// 상호작용 대상의 NetworkId 반환
/// 
/// !!!! 여기서는 네트워크 상태를 바꾸지 않음
/// 실제 상호작용 성립은 PlayerController의 RPC 요청 후 서버가 판정
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

    public string CurrentPromptText =>
        HasValidTarget ? _currentInteractable.GetPromptText(_controller) : string.Empty;

    public void Initialize(PlayerController controller)
    {
        _controller = controller;

        if (_controller != null && _controller.LookView != null)
            _viewCamera = _controller.LookView.ViewCamera;
    }

    private void Update()
    {
        if (_controller == null)
            return;

        if (!_controller.HasInputAuthority)
            return;

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


    /// <summary>
    /// Hit 된 Collider에서 상호작용 가능한 부모를 찾는다
    /// NetworkObject와 IInteractable 둘 다 있어야 유효한 타겟으로 본다
    /// </summary>
    /// <param name="start"></param>
    /// <param name="targetObject"></param>
    /// <param name="interactable"></param>
    /// <returns></returns>
    public static bool TryFindInteractable(Transform start, out NetworkObject targetObject, out IInteractable interactable)
    {
        targetObject = start.GetComponent<NetworkObject>();
        interactable = null;

        MonoBehaviour[] behaviours = start.GetComponentsInParent<MonoBehaviour>(true);
        foreach(var behaviour in behaviours)
        {
            if(behaviour is IInteractable found)
            {
                interactable = found;
                break;
            }
        }

        return targetObject != null && interactable != null;
    }
}
