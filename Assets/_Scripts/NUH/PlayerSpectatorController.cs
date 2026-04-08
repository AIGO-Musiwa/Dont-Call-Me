using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Dead / Escaped 상태에서 살아있는 플레이어를 기준으로 3인칭 관전을 제공하는 스크립트.
/// 현재 단계에서는 기본 카메라 추적과 타겟 순환 틀만 제공한다.
/// </summary>
public class PlayerSpectatorController : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private Camera spectatorCamera;

    [Header("3인칭 오프셋")]
    [SerializeField] private Vector3 cameraOffset = new Vector3(0f, 2.0f, -3.5f);

    private PlayerController _controller;
    private int _targetIndex;
    private readonly List<PlayerController> _aliveTargets = new();

    /// <summary>
    /// PlayerController에서 스폰 시 호출되어 로컬 플레이어 참조를 연결한다.
    /// </summary>
    public void Initialize(PlayerController controller)
    {
        _controller = controller;

        if (spectatorCamera == null)
            spectatorCamera = GetComponentInChildren<Camera>(true);
    }

    /// <summary>
    /// 로컬 플레이어가 관전 상태일 때만 관전 타겟과 카메라를 갱신한다.
    /// </summary>
    private void LateUpdate()
    {
        if (_controller == null)
            return;

        if (!_controller.HasInputAuthority)
            return;

        if (!_controller.IsSpectatorState())
        {
            SetSpectatorCameraEnabled(false);
            return;
        }

        SetSpectatorCameraEnabled(true);
        RefreshAliveTargets();
        FollowCurrentTarget();
    }

    /// <summary>
    /// 관전 카메라 활성 여부를 상태에 맞게 토글한다.
    /// </summary>
    private void SetSpectatorCameraEnabled(bool enabled)
    {
        if (spectatorCamera == null)
            return;

        if (spectatorCamera.enabled == enabled)
            return;

        spectatorCamera.enabled = enabled;
    }

    /// <summary>
    /// 현재 살아있는 플레이어 목록을 다시 구성한다.
    /// </summary>
    private void RefreshAliveTargets()
    {
        _aliveTargets.Clear();

        PlayerController[] allPlayers = Object.FindObjectsByType<PlayerController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var player in allPlayers)
        {
            if (player == null)
                continue;

            if (player.NetPlayerState != PlayerState.Normal)
                continue;

            _aliveTargets.Add(player);
        }

        if (_aliveTargets.Count == 0)
            _targetIndex = 0;
        else
            _targetIndex = Mathf.Clamp(_targetIndex, 0, _aliveTargets.Count - 1);
    }

    /// <summary>
    /// 네트워크 입력 구조를 받아 관전 타겟 순환 입력을 처리한다.
    /// 좌클릭은 이전 타겟, 우클릭은 다음 타겟으로 사용한다.
    /// </summary>
    public void TickSpectatorInput(PlayerNetworkInput input)
    {
        if (_aliveTargets.Count == 0)
            return;

        if (input.Buttons.IsSet(InputButtons.Interact))
            CycleTarget(-1);

        if (input.Buttons.IsSet(InputButtons.Walkie))
            CycleTarget(1);
    }

    /// <summary>
    /// 현재 선택된 관전 타겟을 기준으로 3인칭 카메라 위치를 따라간다.
    /// </summary>
    private void FollowCurrentTarget()
    {
        if (spectatorCamera == null)
            return;

        if (_aliveTargets.Count == 0)
            return;

        PlayerController target = _aliveTargets[_targetIndex];
        if (target == null)
            return;

        Transform targetTransform = target.transform;
        spectatorCamera.transform.position = targetTransform.position + targetTransform.rotation * cameraOffset;
        spectatorCamera.transform.LookAt(targetTransform.position + Vector3.up * 1.5f);
    }

    /// <summary>
    /// 관전 타겟 인덱스를 좌/우 방향으로 순환시킨다.
    /// </summary>
    private void CycleTarget(int direction)
    {
        if (_aliveTargets.Count == 0)
            return;

        _targetIndex += direction;

        if (_targetIndex < 0)
            _targetIndex = _aliveTargets.Count - 1;
        else if (_targetIndex >= _aliveTargets.Count)
            _targetIndex = 0;
    }
}
