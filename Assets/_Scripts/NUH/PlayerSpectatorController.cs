using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

/// <summary>
/// Cinemachine Orbital Follow 기반 관전 컨트롤러.
/// - 로컬 플레이어가 Dead / Escaped 상태가 되면 관전 모드로 진입한다.
/// - 관전 중에는 평상시 1인칭 카메라를 끄고 spectator rig를 켠다.
/// - 관전 대상은 Normal + Captured만 허용한다.
/// - 좌클릭 / 우클릭 계열 입력으로 대상 전환, 마우스 이동으로 Orbit, 휠로 Zoom을 처리한다.
/// </summary>
public class PlayerSpectatorController : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private Camera spectatorCamera;                      // 관전 화면을 실제로 출력하는 카메라
    [SerializeField] private CinemachineCamera spectatorOrbitCamera;      // 관전용 가상 카메라
    [SerializeField] private InputActionReference lookAction;             // 관전 회전 입력 액션
    [SerializeField] private InputActionReference zoomAction;             // 관전 줌 입력 액션

    [Header("Orbit")]
    [SerializeField] private float horizontalSpeed = 0.2f;                // 수평 회전 민감도
    [SerializeField] private float verticalSpeed = 0.15f;                 // 수직 회전 민감도
    [SerializeField] private float defaultVerticalAngle = 15f;            // 관전 진입 시 기본 수직 각도
    [SerializeField] private float minVerticalAngle = -30f;               // 수직 회전 최소 각도
    [SerializeField] private float maxVerticalAngle = 65f;                // 수직 회전 최대 각도

    [Header("Zoom")]
    [SerializeField] private float zoomSpeed = 0.3f;                      // 줌 민감도
    [SerializeField] private float defaultDistance = 2.0f;                // 관전 진입 시 기본 거리
    [SerializeField] private float minDistance = 1.0f;                    // 최소 줌 거리
    [SerializeField] private float maxDistance = 4.5f;                    // 최대 줌 거리

    private PlayerController _owner;                                      // 이 관전 컨트롤러를 사용하는 로컬 플레이어
    private CinemachineOrbitalFollow _orbitalFollow;                      // Orbit / Zoom 값을 실제로 적용할 Cinemachine 컴포넌트

    private Camera _ownerGameplayCamera;                                  // 평상시 1인칭 시점 카메라
    private AudioListener _ownerGameplayAudioListener;                    // 평상시 카메라의 오디오 리스너
    private AudioListener _spectatorAudioListener;                        // 관전 카메라의 오디오 리스너

    private readonly List<PlayerController> _targets = new();             // 현재 관전 가능한 플레이어 목록
    private int _targetIndex;                                             // 현재 선택된 관전 대상 인덱스

    private bool _isSpectating;                                           // 현재 관전 모드 진입 여부
    private bool _prevInteractPressed;                                    // 이전 프레임 대상 전환 입력 상태(이전 대상)
    private bool _prevWalkiePressed;                                      // 이전 프레임 대상 전환 입력 상태(다음 대상)

    /// <summary>
    /// 로컬 플레이어를 연결하고 관전 리그와 평상시 카메라 참조를 초기화한다.
    /// PlayerController.Spawned()가 모든 플레이어에서 호출하므로
    /// 여기서 반드시 로컬 플레이어만 owner로 받도록 방어한다.
    /// </summary>
    public void Initialize(PlayerController owner)
    {
        // 잘못된 owner는 무시한다.
        if (owner == null)
            return;

        // 관전 카메라는 로컬 플레이어만 사용하므로 InputAuthority가 없는 객체는 owner로 삼지 않는다.
        if (!owner.HasInputAuthority)
            return;

        // 이미 같은 로컬 플레이어로 초기화되었다면 중복 작업을 피한다.
        if (_owner == owner)
            return;

        _owner = owner;

        // Cinemachine Orbit 제어 컴포넌트를 캐싱한다.
        if (spectatorOrbitCamera != null)
            _orbitalFollow = spectatorOrbitCamera.GetComponent<CinemachineOrbitalFollow>();

        // 로컬 플레이어의 평상시 1인칭 카메라를 캐싱한다.
        if (_owner.LookView != null)
            _ownerGameplayCamera = _owner.LookView.ViewCamera;

        // 평상시 카메라의 AudioListener를 캐싱한다.
        if (_ownerGameplayCamera != null)
            _ownerGameplayAudioListener = _ownerGameplayCamera.GetComponent<AudioListener>();

        // 관전 카메라의 AudioListener를 캐싱한다.
        if (spectatorCamera != null)
            _spectatorAudioListener = spectatorCamera.GetComponent<AudioListener>();

        // 초기에는 관전 상태가 아니므로 spectator rig를 꺼둔다.
        SetSpectatorRigActive(false);

        // 초기에는 평상시 카메라만 켜두는 모드로 맞춘다.
        ApplyCameraMode(false);

        // 관전 Orbit 기본값을 준비한다.
        ResetOrbitState();
    }

    /// <summary>
    /// 로컬 플레이어의 상태를 보고 관전 진입 / 유지 / 종료를 관리한다.
    /// Orbit / Zoom은 로컬 입력으로만 처리한다.
    /// </summary>
    private void LateUpdate()
    {
        // owner가 없거나 로컬 플레이어가 아니면 아무것도 하지 않는다.
        if (_owner == null || !_owner.HasInputAuthority)
            return;

        // Dead / Escaped이면 관전 상태로 본다.
        bool shouldSpectate = _owner.IsSpectatorState();

        // 관전 상태가 아니면 필요 시 관전 모드를 종료한다.
        if (!shouldSpectate)
        {
            if (_isSpectating)
                ExitSpectatorMode();

            return;
        }

        // 아직 관전 모드가 아니라면 최초 진입 처리.
        if (!_isSpectating)
            EnterSpectatorMode();

        // 현재 대상이 유효한지 점검하고 필요 시 목록 갱신.
        RefreshTargetsIfNeeded();

        // 마우스 이동으로 orbit 값을 갱신.
        ApplyOrbitInput();

        // 휠 입력으로 줌 값을 갱신.
        ApplyZoomInput();

        // 현재 대상 anchor를 Follow / LookAt에 계속 반영.
        ApplyCurrentTarget();
    }

    /// <summary>
    /// PlayerController.FixedUpdateNetwork에서 전달받은 버튼 입력으로
    /// 관전 대상 전환만 처리한다.
    /// </summary>
    public void TickSpectatorInput(PlayerNetworkInput input)
    {
        // 관전 중이 아닐 때는 대상 전환 입력을 받지 않는다.
        if (!_isSpectating)
            return;

        // 이전 대상 전환 입력 상태.
        bool interactPressed = input.Buttons.IsSet(InputButtons.Interact);

        // 다음 대상 전환 입력 상태.
        bool walkiePressed = input.Buttons.IsSet(InputButtons.Walkie);

        // 입력이 막 눌린 순간에만 이전 대상으로 이동.
        if (interactPressed && !_prevInteractPressed)
            SelectPreviousTarget();

        // 입력이 막 눌린 순간에만 다음 대상으로 이동.
        if (walkiePressed && !_prevWalkiePressed)
            SelectNextTarget();

        // 엣지 트리거 판정을 위해 이전 입력 상태를 저장.
        _prevInteractPressed = interactPressed;
        _prevWalkiePressed = walkiePressed;
    }

    /// <summary>
    /// 관전 모드에 진입한다.
    /// - Orbit 값을 초기화하고
    /// - 관전 대상 목록을 갱신하고
    /// - 카메라 출력을 관전 모드로 전환한다.
    /// </summary>
    private void EnterSpectatorMode()
    {
        _isSpectating = true;                 // 내부적으로 관전 진입 표시
        ResetOrbitState();                    // 관전 진입 시 Orbit 기본값 복원
        RefreshTargets();                     // 현재 관전 가능한 대상 목록 구성
        ClampTargetIndex();                   // 대상 인덱스를 안전 범위로 보정
        ApplyCameraMode(true);                // 평상시 카메라를 끄고 관전 카메라를 켠다.
        ApplyCurrentTarget();                 // 첫 관전 대상에 Follow / LookAt 적용
    }

    /// <summary>
    /// 관전 모드를 종료한다.
    /// - 대상 목록 / 입력 상태를 초기화하고
    /// - Follow / LookAt을 해제하고
    /// - 카메라 출력을 평상시 모드로 되돌린다.
    /// </summary>
    private void ExitSpectatorMode()
    {
        _isSpectating = false;                // 내부적으로 관전 종료 표시
        _targets.Clear();                     // 관전 대상 목록 비움
        _targetIndex = 0;                     // 인덱스 초기화
        _prevInteractPressed = false;         // 이전 입력 상태 초기화
        _prevWalkiePressed = false;           // 이전 입력 상태 초기화

        // 관전 가상 카메라가 더 이상 누구도 따라보지 않도록 해제한다.
        if (spectatorOrbitCamera != null)
        {
            spectatorOrbitCamera.Follow = null;
            spectatorOrbitCamera.LookAt = null;
        }

        // 평상시 카메라를 다시 켜고 관전 카메라는 끈다.
        ApplyCameraMode(false);
    }

    /// <summary>
    /// 관전 가능한 대상 목록을 새로 구성한다.
    /// - 자기 자신은 제외
    /// - CanBeSpectated()가 true인 플레이어만 포함
    /// </summary>
    private void RefreshTargets()
    {
        _targets.Clear(); // 기존 목록 제거

        // 현재 씬에 존재하는 플레이어를 전부 찾는다.
        PlayerController[] allPlayers =
            Object.FindObjectsByType<PlayerController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        // 관전 가능한 플레이어만 목록에 넣는다.
        foreach (var player in allPlayers)
        {
            if (!IsValidSpectatorTarget(player))
                continue;

            _targets.Add(player);
        }

        // 목록 갱신 후 인덱스를 안전 범위로 다시 맞춘다.
        ClampTargetIndex();
    }

    /// <summary>
    /// 현재 타겟이 사라졌거나 상태가 바뀌었을 때만
    /// 대상 목록을 다시 구성한다.
    /// 매 프레임 전체 검색 비용을 줄이기 위한 최소 갱신용 함수다.
    /// </summary>
    private void RefreshTargetsIfNeeded()
    {
        // 목록이 비었으면 다시 만든다.
        if (_targets.Count == 0)
        {
            RefreshTargets();
            return;
        }

        // 현재 인덱스가 범위를 벗어났으면 다시 만든다.
        if (_targetIndex < 0 || _targetIndex >= _targets.Count)
        {
            RefreshTargets();
            return;
        }

        // 현재 타겟이 더 이상 관전 불가 상태면 다시 만든다.
        if (!IsValidSpectatorTarget(_targets[_targetIndex]))
            RefreshTargets();
    }

    /// <summary>
    /// 특정 플레이어가 관전 대상이 될 수 있는지 검사한다.
    /// - null 제외
    /// - 자기 자신 제외
    /// - PlayerController.CanBeSpectated()가 true여야 함
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
    /// 현재 선택된 플레이어의 관전 anchor를 찾아
    /// Cinemachine Follow / LookAt 대상으로 적용한다.
    /// </summary>
    private void ApplyCurrentTarget()
    {
        // 관전 카메라가 없거나 타겟이 없으면 종료.
        if (spectatorOrbitCamera == null || _targets.Count == 0)
            return;

        // 현재 인덱스의 플레이어를 가져온다.
        PlayerController target = _targets[_targetIndex];
        if (target == null)
            return;

        // 관전 anchor를 찾는다.
        Transform anchor = GetSpectatorAnchor(target);
        if (anchor == null)
            return;

        // Follow와 LookAt을 동일 anchor로 맞춘다.
        spectatorOrbitCamera.Follow = anchor;
        spectatorOrbitCamera.LookAt = anchor;
    }

    /// <summary>
    /// 마우스 이동으로 Orbital Follow 축값을 직접 구동한다.
    /// - X는 수평 회전
    /// - Y는 수직 회전
    /// </summary>
    private void ApplyOrbitInput()
    {
        // 필수 참조가 없으면 종료.
        if (_orbitalFollow == null || lookAction == null || lookAction.action == null)
            return;

        // 현재 마우스 이동량을 읽는다.
        Vector2 look = lookAction.action.ReadValue<Vector2>();
        if (look.sqrMagnitude <= 0.000001f)
            return;

        // 수평 회전값 갱신 후 Cinemachine 범위에 맞게 clamp.
        _orbitalFollow.HorizontalAxis.Value += look.x * horizontalSpeed;
        _orbitalFollow.HorizontalAxis.Value =
            _orbitalFollow.HorizontalAxis.ClampValue(_orbitalFollow.HorizontalAxis.Value);

        // 수직 회전값 갱신 후 직접 최소/최대 각도로 clamp.
        _orbitalFollow.VerticalAxis.Value -= look.y * verticalSpeed;
        _orbitalFollow.VerticalAxis.Value = Mathf.Clamp(
            _orbitalFollow.VerticalAxis.Value,
            minVerticalAngle,
            maxVerticalAngle);
    }

    /// <summary>
    /// 마우스 휠 입력으로 관전 거리(Radius)를 조절한다.
    /// </summary>
    private void ApplyZoomInput()
    {
        // 필수 참조가 없으면 종료.
        if (_orbitalFollow == null || zoomAction == null || zoomAction.action == null)
            return;

        // 현재 휠 입력값을 읽는다.
        float zoom = zoomAction.action.ReadValue<float>();
        if (Mathf.Approximately(zoom, 0f))
            return;

        // 입력값을 반영한 다음 Radius를 최소/최대 범위로 제한한다.
        float nextRadius = _orbitalFollow.Radius - zoom * zoomSpeed;
        _orbitalFollow.Radius = Mathf.Clamp(nextRadius, minDistance, maxDistance);
    }

    /// <summary>
    /// 다음 관전 대상으로 순환한다.
    /// </summary>
    private void SelectNextTarget()
    {
        // 대상이 없으면 아무 일도 하지 않는다.
        if (_targets.Count == 0)
            return;

        _targetIndex++; // 다음 인덱스로 이동
        if (_targetIndex >= _targets.Count)
            _targetIndex = 0; // 마지막이면 처음으로 순환

        ApplyCurrentTarget(); // 새 대상에 바로 카메라 적용
    }

    /// <summary>
    /// 이전 관전 대상으로 순환한다.
    /// </summary>
    private void SelectPreviousTarget()
    {
        // 대상이 없으면 아무 일도 하지 않는다.
        if (_targets.Count == 0)
            return;

        _targetIndex--; // 이전 인덱스로 이동
        if (_targetIndex < 0)
            _targetIndex = _targets.Count - 1; // 처음이면 마지막으로 순환

        ApplyCurrentTarget(); // 새 대상에 바로 카메라 적용
    }

    /// <summary>
    /// spectator rig 자체의 활성 상태를 토글한다.
    /// - 실제 출력 카메라
    /// - 관전 가상 카메라
    /// 두 오브젝트를 함께 켜고 끈다.
    /// </summary>
    private void SetSpectatorRigActive(bool active)
    {
        if (spectatorCamera != null)
            spectatorCamera.gameObject.SetActive(active);

        if (spectatorOrbitCamera != null)
            spectatorOrbitCamera.gameObject.SetActive(active);
    }

    /// <summary>
    /// 로컬 플레이어의 평상시 1인칭 카메라 활성 상태를 토글한다.
    /// 관전 모드에서는 false, 일반 모드에서는 true가 된다.
    /// </summary>
    private void SetOwnerGameplayCameraActive(bool active)
    {
        // 평상시 카메라 GameObject 자체를 켜고 끈다.
        if (_ownerGameplayCamera != null)
            _ownerGameplayCamera.gameObject.SetActive(active);

        // AudioListener도 같이 맞춰준다.
        if (_ownerGameplayAudioListener != null)
            _ownerGameplayAudioListener.enabled = active;
    }

    /// <summary>
    /// 관전 카메라 출력 활성 상태를 토글한다.
    /// 관전 모드에서는 true, 일반 모드에서는 false가 된다.
    /// </summary>
    private void SetSpectatorOutputActive(bool active)
    {
        // spectator rig 전체를 켜고 끈다.
        SetSpectatorRigActive(active);

        // 관전 카메라의 AudioListener도 같이 맞춰준다.
        if (_spectatorAudioListener != null)
            _spectatorAudioListener.enabled = active;
    }

    /// <summary>
    /// 현재 카메라 모드를 일괄 적용한다.
    /// - spectating == true  : 평상시 카메라 OFF, 관전 카메라 ON
    /// - spectating == false : 평상시 카메라 ON,  관전 카메라 OFF
    /// </summary>
    private void ApplyCameraMode(bool spectating)
    {
        if (spectating)
        {
            SetOwnerGameplayCameraActive(false); // 1인칭 카메라 끄기
            SetSpectatorOutputActive(true);      // 관전 카메라 켜기
        }
        else
        {
            SetSpectatorOutputActive(false);     // 관전 카메라 끄기
            SetOwnerGameplayCameraActive(true);  // 1인칭 카메라 켜기
        }
    }

    /// <summary>
    /// 관전 진입 시 Orbital Follow의 기본 상태를 초기화한다.
    /// - 수평 각도
    /// - 수직 각도
    /// - 거리
    /// 를 모두 기본값으로 되돌린다.
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
    /// 현재 대상 인덱스를 안전 범위로 보정한다.
    /// 대상이 없으면 0으로 초기화한다.
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
    /// anchor가 없으면 플레이어 루트를 fallback으로 사용한다.
    /// </summary>
    private Transform GetSpectatorAnchor(PlayerController target)
    {
        SpectatorTargetAnchor anchor = target.GetComponentInChildren<SpectatorTargetAnchor>(true);
        if (anchor != null)
            return anchor.GetAnchor();

        return target.transform;
    }

    /// <summary>
    /// 현재 관전 중인 대상의 이름을 반환한다.
    /// HUD에서 "현재 누구를 보고 있는지" 표시할 때 사용한다.
    /// </summary>
    public string GetCurrentTargetName()
    {
        if (!_isSpectating || _targets.Count == 0 || _targetIndex < 0 || _targetIndex >= _targets.Count)
            return "대상 없음";

        return _targets[_targetIndex].gameObject.name;
    }
}