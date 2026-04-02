using Fusion;
using UnityEngine;
using TMPro;

/// <summary>
/// 플레이어가 보고 있는 정면 대상과 상호작용을 담당합니다.
///
/// 흐름:
/// 1. Update에서 레이캐스트로 보고 있는 대상 감지
/// 2. 대상이 IInteractable이면 프롬프트 표시
/// 3. 네트워크 입력 틱에서 좌클릭이 들어오면 실제 상호작용 실행
///
/// 중요한 점:
/// "감지"는 프레임마다 하고,
/// "실제 실행"은 FixedUpdateNetwork에서 합니다.
/// </summary>
public class InteractionSystem : NetworkBehaviour
{
    [Header("레이캐스트")]
    [SerializeField] private float interactRange = 2.5f; // 상호작용 거리
    [SerializeField] private LayerMask interactMask; // 어떤 레이어를 상호작용 대상으로 볼지

    [Header("레퍼런스")]
    [SerializeField] private Camera fpCamera; // 1인칭 카메라
    [SerializeField] private TextMeshProUGUI promptText; // HUD 상호작용 문구
                                                         // 현재 보고 있는 상호작용 대상 캐시
    private IInteractable _currentTarget;

    // 상호작용을 시도하는 내 플레이어 참조
    private PlayerController _playerController;

    public override void Spawned()
    {
        _playerController = GetComponent<PlayerController>();

        // 로컬 플레이어만 레이캐스트/UI/입력 소비를 해야 합니다.
        if (!HasInputAuthority)
        {
            enabled = false;
            return;
        }

        if (promptText != null)
            promptText.text = string.Empty;
    }

    private void Update()
    {
        if (!HasInputAuthority) return;
        DetectTarget();
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasInputAuthority) return;
        if (!GetInput(out PlayerNetworkInput input)) return;
        if (input.Buttons.IsSet(InputButtons.Interact))
            TryInteract();
    }

/// <summary>
/// 카메라 정면으로 레이캐스트를 쏴서 보고 있는 대상을 찾습니다.
/// </summary>
private void DetectTarget()
    {
        Ray ray = new Ray(fpCamera.transform.position, fpCamera.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, interactRange, interactMask))
        {
            IInteractable interactable = hit.collider.GetComponent<IInteractable>();
            // 구현체가 있고, 지금 상호작용 가능한 상태라면 현재 타겟으로 지정
            if (interactable != null && interactable.CanInteract(_playerController))
            {
                _currentTarget = interactable;
                UpdatePromptUI(_currentTarget.GetPromptText());
                return;
            }
        }

        // 아무것도 없거나 상호작용 불가면 비움
        ClearTarget();
    }

    /// <summary>
    /// 실제 상호작용 실행.
    /// FixedUpdateNetwork 시점에 다시 CanInteract를 확인하는 이유는,
    /// 감지 시점과 클릭 시점 사이에 상태가 바뀔 수 있기 때문입니다.
    /// </summary>
    private void TryInteract()
    {
        if (_currentTarget == null) return;
        if (!_currentTarget.CanInteract(_playerController)) return;
        _currentTarget.OnInteract(_playerController);
    }

    private void UpdatePromptUI(string text)
    {
        if (promptText == null) return;
        promptText.text = text;
    }

    private void ClearTarget()
    {
        _currentTarget = null;
        UpdatePromptUI(string.Empty);
    }
}