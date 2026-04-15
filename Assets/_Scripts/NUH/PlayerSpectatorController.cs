using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

/// <summary>
/// 진짜 Cinemachine Orbital Follow 기반 관전 컨트롤러.
/// - 카메라 Transform을 직접 움직이지 않는다.
/// - SpectatorOrbitCamera의 Orbital Follow 축값만 구동한다.
/// - 관전 대상은 Normal + Captured, 제외는 Dead + Escaped.
/// - 좌클릭 / 우클릭으로 대상 전환
/// - 마우스 이동으로 Orbit
/// - 휠로 Radius 줌
/// </summary>
public class PlayerSpectatorController : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private Camera spectatorCamera;
    [SerializeField] private CinemachineCamera spectatorOrbitCamera;
    [SerializeField] private InputActionReference lookAction;
    [SerializeField] private InputActionReference zoomAction;

    [Header("Orbit")]
    [SerializeField] private float horizontalSpeed = 0.2f;
    [SerializeField] private float verticalSpeed = 0.15f;
    [SerializeField] private float defaultVerticalAngle = 15f;
    [SerializeField] private float minVerticalAngle = -30f;
    [SerializeField] private float maxVerticalAngle = 65f;

    [Header("Zoom")]
    [SerializeField] private float zoomSpeed = 0.1f;
    [SerializeField] private float defaultDistance = 4.0f;
    [SerializeField] private float minDistance = 2.5f;
    [SerializeField] private float maxDistance = 6.5f;

    private PlayerController _owner;
    private CinemachineOrbitalFollow _orbitalFollow;

    private readonly List<PlayerController> _targets = new();
    private int _targetIndex;

    private bool _isSpectating;
    private bool _prevInteractPressed;
    private bool _prevWalkiePressed;

    /// <summary>
    /// 로컬 플레이어를 연결하고 Cinemachine 참조를 초기화한다.
    /// </summary>
    public void Initialize(PlayerController owner)
    {
        _owner = owner;

        if (spectatorOrbitCamera != null)
            _orbitalFollow = spectatorOrbitCamera.GetComponent<CinemachineOrbitalFollow>();

        if (spectatorCamera != null)
            spectatorCamera.gameObject.SetActive(false);

        if (spectatorOrbitCamera != null)
            spectatorOrbitCamera.gameObject.SetActive(false);

        ResetOrbitState();
    }

    /// <summary>
    /// 관전 상태 진입/유지/종료를 관리한다.
    /// Orbit/Zoom은 로컬 입력으로만 구동한다.
    /// </summary>
    private void LateUpdate()
    {
        if (_owner == null || !_owner.HasInputAuthority)
            return;

        bool shouldSpectate = _owner.IsSpectatorState();

        if (!shouldSpectate)
        {
            if (_isSpectating)
                ExitSpectatorMode();
            return;
        }

        if (!_isSpectating)
            EnterSpectatorMode();

        RefreshTargetsIfNeeded();
        ApplyOrbitInput();
        ApplyZoomInput();
        ApplyCurrentTarget();
    }

    /// <summary>
    /// PlayerController.FixedUpdateNetwork에서 넘어온 버튼 입력으로
    /// 관전 대상 전환만 처리한다.
    /// </summary>
    public void TickSpectatorInput(PlayerNetworkInput input)
    {
        if (!_isSpectating)
            return;

        bool interactPressed = input.Buttons.IsSet(InputButtons.Interact);
        bool walkiePressed = input.Buttons.IsSet(InputButtons.Walkie);

        if (interactPressed && !_prevInteractPressed)
            SelectPreviousTarget();

        if (walkiePressed && !_prevWalkiePressed)
            SelectNextTarget();

        _prevInteractPressed = interactPressed;
        _prevWalkiePressed = walkiePressed;
    }

    /// <summary>
    /// 관전 모드에 진입한다.
    /// </summary>
    private void EnterSpectatorMode()
    {
        _isSpectating = true;
        ResetOrbitState();
        RefreshTargets();
        ClampTargetIndex();
        SetSpectatorRigActive(true);
        ApplyCurrentTarget();
    }

    /// <summary>
    /// 관전 모드를 종료한다.
    /// </summary>
    private void ExitSpectatorMode()
    {
        _isSpectating = false;
        _targets.Clear();
        _targetIndex = 0;
        _prevInteractPressed = false;
        _prevWalkiePressed = false;

        if (spectatorOrbitCamera != null)
        {
            spectatorOrbitCamera.Follow = null;
            spectatorOrbitCamera.LookAt = null;
        }

        SetSpectatorRigActive(false);
    }

    /// <summary>
    /// 관전 가능한 대상 목록을 다시 구성한다.
    /// </summary>
    private void RefreshTargets()
    {
        _targets.Clear();

        PlayerController[] allPlayers =
            Object.FindObjectsByType<PlayerController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        foreach (var player in allPlayers)
        {
            if (!IsValidSpectatorTarget(player))
                continue;

            _targets.Add(player);
        }

        ClampTargetIndex();
    }

    /// <summary>
    /// 현재 타겟이 사라졌거나 상태가 바뀌었을 때만 목록을 다시 갱신한다.
    /// </summary>
    private void RefreshTargetsIfNeeded()
    {
        if (_targets.Count == 0)
        {
            RefreshTargets();
            return;
        }

        if (_targetIndex < 0 || _targetIndex >= _targets.Count)
        {
            RefreshTargets();
            return;
        }

        if (!IsValidSpectatorTarget(_targets[_targetIndex]))
            RefreshTargets();
    }

    /// <summary>
    /// 관전 대상이 될 수 있는지 검사한다.
    /// </summary>
    private bool IsValidSpectatorTarget(PlayerController target)
    {
        if (target == null)
            return false;

        if (target == _owner)
            return false;

        return target.CanBeSpectated();
    }

    /// <summary>
    /// 현재 선택된 플레이어의 SpectatorTargetAnchor를
    /// Cinemachine Follow / LookAt 대상으로 적용한다.
    /// </summary>
    private void ApplyCurrentTarget()
    {
        if (spectatorOrbitCamera == null || _targets.Count == 0)
            return;

        PlayerController target = _targets[_targetIndex];
        if (target == null)
            return;

        Transform anchor = GetSpectatorAnchor(target);
        if (anchor == null)
            return;

        spectatorOrbitCamera.Follow = anchor;
        spectatorOrbitCamera.LookAt = anchor;
    }

    /// <summary>
    /// 마우스 이동으로 Orbital Follow 축값을 직접 구동한다.
    /// </summary>
    private void ApplyOrbitInput()
    {
        if (_orbitalFollow == null || lookAction == null || lookAction.action == null)
            return;

        Vector2 look = lookAction.action.ReadValue<Vector2>();
        if (look.sqrMagnitude <= 0.000001f)
            return;

        _orbitalFollow.HorizontalAxis.Value += look.x * horizontalSpeed;
        _orbitalFollow.HorizontalAxis.Value =
            _orbitalFollow.HorizontalAxis.ClampValue(_orbitalFollow.HorizontalAxis.Value);

        _orbitalFollow.VerticalAxis.Value -= look.y * verticalSpeed;
        _orbitalFollow.VerticalAxis.Value = Mathf.Clamp(
            _orbitalFollow.VerticalAxis.Value,
            minVerticalAngle,
            maxVerticalAngle);
    }

    /// <summary>
    /// 마우스 휠로 Orbital Follow의 Radius를 조절한다.
    /// </summary>
    private void ApplyZoomInput()
    {
        if (_orbitalFollow == null || zoomAction == null || zoomAction.action == null)
            return;

        float zoom = zoomAction.action.ReadValue<float>();
        if (Mathf.Approximately(zoom, 0f))
            return;

        float nextRadius = _orbitalFollow.Radius - zoom * zoomSpeed;
        _orbitalFollow.Radius = Mathf.Clamp(nextRadius, minDistance, maxDistance);
    }

    /// <summary>
    /// 다음 관전 대상으로 순환한다.
    /// </summary>
    private void SelectNextTarget()
    {
        if (_targets.Count == 0)
            return;

        _targetIndex++;
        if (_targetIndex >= _targets.Count)
            _targetIndex = 0;

        ApplyCurrentTarget();
    }

    /// <summary>
    /// 이전 관전 대상으로 순환한다.
    /// </summary>
    private void SelectPreviousTarget()
    {
        if (_targets.Count == 0)
            return;

        _targetIndex--;
        if (_targetIndex < 0)
            _targetIndex = _targets.Count - 1;

        ApplyCurrentTarget();
    }

    /// <summary>
    /// SpectatorCamera / SpectatorOrbitCamera 활성 상태를 토글한다.
    /// </summary>
    private void SetSpectatorRigActive(bool active)
    {
        if (spectatorCamera != null)
            spectatorCamera.gameObject.SetActive(active);

        if (spectatorOrbitCamera != null)
            spectatorOrbitCamera.gameObject.SetActive(active);
    }

    /// <summary>
    /// 관전 진입 시 Orbital Follow 기본 상태를 초기화한다.
    /// </summary>
    private void ResetOrbitState()
    {
        if (_orbitalFollow == null)
            return;

        _orbitalFollow.HorizontalAxis.Value = 0f;
        _orbitalFollow.VerticalAxis.Value = defaultVerticalAngle;
        _orbitalFollow.Radius = Mathf.Clamp(defaultDistance, minDistance, maxDistance);
    }

    /// <summary>
    /// 현재 인덱스를 안전 범위로 보정한다.
    /// </summary>
    private void ClampTargetIndex()
    {
        if (_targets.Count == 0)
        {
            _targetIndex = 0;
            return;
        }

        _targetIndex = Mathf.Clamp(_targetIndex, 0, _targets.Count - 1);
    }

    /// <summary>
    /// 플레이어 프리팹에 붙은 SpectatorTargetAnchor를 찾아 반환한다.
    /// 없으면 플레이어 루트를 fallback으로 사용한다.
    /// </summary>
    private Transform GetSpectatorAnchor(PlayerController target)
    {
        SpectatorTargetAnchor anchor = target.GetComponentInChildren<SpectatorTargetAnchor>(true);
        if (anchor != null)
            return anchor.GetAnchor();

        return target.transform;
    }

    public string GetCurrentTargetName()
    {
        if (!_isSpectating || _targets.Count == 0 || _targetIndex < 0 || _targetIndex >= _targets.Count)
            return "대상 없음";

        // 대상 PlayerController가 붙은 객체의 이름을 반환 (보통 플레이어 이름으로 설정됨)
        return _targets[_targetIndex].gameObject.name;
    }
}