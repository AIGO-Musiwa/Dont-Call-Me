using Fusion;
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 사망/탈출 시 관전 모드 처리
///
/// 관전 가능 대상:
///   사망한 플레이어, 먼저 탈출한 플레이어
///   같은 건물에 한정되지 않고 모든 플레이어 관전 가능
///
/// 조작:
///   좌클릭 → 이전 플레이어
///   우클릭 → 다음 플레이어
///   (InteractionSystem과 분리 — 관전 모드 전용 입력 처리)
/// </summary>
public class SpectatorSystem : NetworkBehaviour
{
    // ─────────────────────────────────────────
    // Inspector 설정값
    // ─────────────────────────────────────────
    [Header("레퍼런스")]
    [SerializeField] private Camera spectatorCamera;
    [SerializeField] private Camera fpCamera;           // 관전 시 비활성화할 1인칭 카메라

    [Header("관전 카메라 오프셋")]
    [SerializeField] private Vector3 cameraOffset = new Vector3(0f, 1.7f, 0f);  // 대상 어깨 위

    // ─────────────────────────────────────────
    // 로컬 변수
    // ─────────────────────────────────────────
    private bool                    _isSpectating;
    private int                     _currentTargetIndex;
    private List<PlayerController>  _spectateTargets = new();

    // 관전 전환 쿨타임 (연속 전환 방지)
    private float _switchCooldown;
    private const float SWITCH_COOLDOWN = 0.3f;

    // ─────────────────────────────────────────
    // Fusion 생명주기
    // ─────────────────────────────────────────
    public override void Spawned()
    {
        if (spectatorCamera != null)
            spectatorCamera.enabled = false;
    }

    // ─────────────────────────────────────────
    // 관전 모드 진입
    // ─────────────────────────────────────────

    /// <summary>
    /// CaptureSystem.EnterDead() / EnterEscaped()에서 호출
    /// </summary>
    public void EnterSpectatorMode()
    {
        if (!HasInputAuthority) return;
        if (_isSpectating)      return;

        _isSpectating = true;

        // 1인칭 카메라 비활성화
        if (fpCamera != null) fpCamera.enabled = false;

        // 관전 카메라 활성화
        if (spectatorCamera != null) spectatorCamera.enabled = true;

        // 모든 건물 렌더링 활성화
        GetComponent<BuildingInterestManager>()?.EnableAllBuildings();

        // 커서 해제
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;

        // 관전 대상 목록 갱신 후 첫 번째 대상 부착
        UpdateTargetList();

        if (_spectateTargets.Count > 0)
        {
            _currentTargetIndex = 0;
            AttachToTarget(_spectateTargets[0]);
        }

        Debug.Log("[SpectatorSystem] 관전 모드 진입");
    }

    // ─────────────────────────────────────────
    // 관전 입력 처리
    // ─────────────────────────────────────────
    public override void FixedUpdateNetwork()
    {
        if (!HasInputAuthority) return;
        if (!_isSpectating)     return;

        _switchCooldown -= Runner.DeltaTime;
        if (_switchCooldown > 0f) return;

        if (!GetInput(out PlayerNetworkInput input)) return;

        // 좌클릭 → 이전 플레이어
        if (input.Buttons.IsSet(InputButtons.Interact))
        {
            PrevTarget();
            _switchCooldown = SWITCH_COOLDOWN;
        }
        // 우클릭 → 다음 플레이어
        else if (input.Buttons.IsSet(InputButtons.Walkie))
        {
            NextTarget();
            _switchCooldown = SWITCH_COOLDOWN;
        }
    }

    // ─────────────────────────────────────────
    // 관전 대상 전환
    // ─────────────────────────────────────────
    public void NextTarget()
    {
        if (_spectateTargets.Count == 0) return;

        UpdateTargetList();  // 최신 상태 반영

        _currentTargetIndex = (_currentTargetIndex + 1) % _spectateTargets.Count;
        AttachToTarget(_spectateTargets[_currentTargetIndex]);
    }

    public void PrevTarget()
    {
        if (_spectateTargets.Count == 0) return;

        UpdateTargetList();

        _currentTargetIndex = (_currentTargetIndex - 1 + _spectateTargets.Count) % _spectateTargets.Count;
        AttachToTarget(_spectateTargets[_currentTargetIndex]);
    }

    /// <summary>
    /// 관전 카메라를 대상 플레이어의 어깨 위에 부착
    /// </summary>
    public void AttachToTarget(PlayerController target)
    {
        if (target == null || spectatorCamera == null) return;

        spectatorCamera.transform.SetParent(target.transform);
        spectatorCamera.transform.localPosition = cameraOffset;
        spectatorCamera.transform.localRotation = Quaternion.identity;

        Debug.Log($"[SpectatorSystem] 관전 대상 전환: {target.name}");
    }

    // ─────────────────────────────────────────
    // 관전 대상 목록 관리
    // ─────────────────────────────────────────

    /// <summary>
    /// 현재 씬의 모든 플레이어 중 살아있는(Normal/Restrained) 플레이어만 필터링
    /// Dead/Escaped는 관전 가능하지 않음 (이미 관전 중이므로)
    /// 자기 자신 제외
    /// </summary>
    public void UpdateTargetList()
    {
        _spectateTargets.Clear();

        PlayerController[] allPlayers = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);

        foreach (PlayerController player in allPlayers)
        {
            // 자기 자신 제외
            if (player.HasInputAuthority) continue;

            // Normal / Restrained 상태인 플레이어만 관전 가능
            PlayerState state = player.CurrentState;
            if (state == PlayerState.Normal || state == PlayerState.Restrained)
                _spectateTargets.Add(player);
        }

        // 관전 중에 대상이 없어지면 인덱스 보정
        if (_currentTargetIndex >= _spectateTargets.Count)
            _currentTargetIndex = 0;
    }

    // ─────────────────────────────────────────
    // 유틸리티
    // ─────────────────────────────────────────
    public bool IsSpectating => _isSpectating;
}
