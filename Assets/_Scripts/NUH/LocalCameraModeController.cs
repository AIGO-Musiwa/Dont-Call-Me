using UnityEngine;
using Unity.Cinemachine;

/// <summary>
/// 씬에 하나만 존재하는 로컬 카메라 리그 관리자.
/// 실제 화면 출력 Camera는 계속 켜두고, Cinemachine Camera의 Priority로 Normal / Captured / Spectator 시점을 전환한다.
/// </summary>
public class LocalCameraModeController : MonoBehaviour
{
    [Header("Output Camera")]
    [SerializeField] private Camera localMainCamera;                  // 실제 화면을 출력하는 씬 카메라
    [SerializeField] private AudioListener localMainAudioListener;    // 실제 소리를 듣는 씬 AudioListener

    [Header("Cinemachine Cameras")]
    [SerializeField] private CinemachineCamera playerCamera;          // Normal 상태에서 로컬 플레이어를 따라가는 가상 카메라
    [SerializeField] private CinemachineCamera capturedCamera;        // Captured 상태에서 CapturedCameraTarget을 따라가는 가상 카메라
    [SerializeField] private CinemachineCamera spectatorCamera;       // Dead/Escaped 상태에서 관전 대상을 따라가는 가상 카메라

    [Header("Priority")]
    [SerializeField] private int inactivePriority = 0;                // 사용하지 않는 가상 카메라 우선순위
    [SerializeField] private int normalPriority = 30;                 // Normal 상태 가상 카메라 우선순위
    [SerializeField] private int capturedPriority = 40;               // Captured 상태 가상 카메라 우선순위
    [SerializeField] private int spectatorPriority = 50;              // 관전 상태 가상 카메라 우선순위

    private PlayerController _localPlayer;                            // 현재 클라이언트가 조종하는 로컬 플레이어
    private PlayerState _lastState;                                    // 마지막으로 적용한 플레이어 상태
    private CapturePhase _lastCapturePhase;                            // 마지막으로 적용한 포획 페이즈
    private bool _hasAppliedOnce;                                      // 최초 적용 여부

    private void Awake()
    {
        ResolveReferences();
        EnsureOutputCameraEnabled();
        SetAllVirtualCamerasInactive();
    }

    private void LateUpdate()
    {
        if (_localPlayer == null)
            TryFindLocalPlayer();

        if (_localPlayer == null)
            return;

        ApplyTargets();
        ApplyCameraModeIfNeeded();
        EnsureOutputCameraEnabled();
    }

    /// <summary>
    /// PlayerController.Spawned()에서 로컬 플레이어가 생성됐을 때 직접 등록한다.
    /// </summary>
    public void RegisterLocalPlayer(PlayerController player)
    {
        if (player == null || !player.HasInputAuthority)
            return;

        _localPlayer = player;
        _hasAppliedOnce = false;

        ApplyTargets();
        ApplyCameraModeIfNeeded();
        EnsureOutputCameraEnabled();
    }

    private void ResolveReferences()
    {
        if (localMainCamera == null)
            localMainCamera = GetComponentInChildren<Camera>(true);

        if (localMainAudioListener == null && localMainCamera != null)
            localMainAudioListener = localMainCamera.GetComponent<AudioListener>();
    }

    private void EnsureOutputCameraEnabled()
    {
        if (localMainCamera != null)
        {
            if (!localMainCamera.gameObject.activeSelf)
                localMainCamera.gameObject.SetActive(true);

            if (!localMainCamera.enabled)
                localMainCamera.enabled = true;
        }

        if (localMainAudioListener != null && !localMainAudioListener.enabled)
            localMainAudioListener.enabled = true;
    }

    private void TryFindLocalPlayer()
    {
        for (int i = 0; i < PlayerController.AllPlayers.Count; i++)
        {
            PlayerController player = PlayerController.AllPlayers[i];
            if (player == null)
                continue;

            if (!player.HasInputAuthority)
                continue;

            RegisterLocalPlayer(player);
            return;
        }
    }

    /// <summary>
    /// 로컬 플레이어가 가진 카메라 Target을 각 Cinemachine Camera에 연결한다.
    /// </summary>
    private void ApplyTargets()
    {
        if (_localPlayer == null)
            return;

        if (playerCamera != null)
        {
            Transform normalTarget = _localPlayer.NormalCameraTarget;
            playerCamera.Follow = normalTarget;
            playerCamera.LookAt = normalTarget;
        }

        if (capturedCamera != null)
        {
            Transform capturedTarget = _localPlayer.CapturedCameraTarget;
            capturedCamera.Follow = capturedTarget;
            capturedCamera.LookAt = capturedTarget;
        }
    }

    private void ApplyCameraModeIfNeeded()
    {
        if (_localPlayer == null)
            return;

        PlayerState currentState = _localPlayer.NetPlayerState;
        CapturePhase currentCapturePhase = _localPlayer.NetCapturePhase;

        if (_hasAppliedOnce && _lastState == currentState && _lastCapturePhase == currentCapturePhase)
            return;

        _hasAppliedOnce = true;
        _lastState = currentState;
        _lastCapturePhase = currentCapturePhase;

        if (_localPlayer.IsSpectatorState())
        {
            SetPriority(playerCamera, inactivePriority);
            SetPriority(capturedCamera, inactivePriority);
            SetPriority(spectatorCamera, spectatorPriority);
            return;
        }

        if (currentState == PlayerState.Captured && currentCapturePhase == CapturePhase.Active)
        {
            if (capturedCamera != null)
            {
                SetPriority(playerCamera, inactivePriority);
                SetPriority(capturedCamera, capturedPriority);
                SetPriority(spectatorCamera, inactivePriority);
                return;
            }

            SetPriority(playerCamera, normalPriority);
            SetPriority(capturedCamera, inactivePriority);
            SetPriority(spectatorCamera, inactivePriority);
            return;
        }

        SetPriority(playerCamera, normalPriority);
        SetPriority(capturedCamera, inactivePriority);
        SetPriority(spectatorCamera, inactivePriority);
    }

    private void SetAllVirtualCamerasInactive()
    {
        SetPriority(playerCamera, inactivePriority);
        SetPriority(capturedCamera, inactivePriority);
        SetPriority(spectatorCamera, inactivePriority);
    }

    private void SetPriority(CinemachineCamera targetCamera, int priority)
    {
        if (targetCamera == null)
            return;

        if (!targetCamera.gameObject.activeSelf)
            targetCamera.gameObject.SetActive(true);

        targetCamera.Priority = priority;
    }
}
