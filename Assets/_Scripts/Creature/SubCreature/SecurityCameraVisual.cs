using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 서브 크리처(감시 카메라)의 클라이언트 측 시각 연출(회전 한계치 적용, 멈춤, 불빛, 사운드) 전담 부품.
/// </summary>
public class SecurityCameraVisual : MonoBehaviour
{
    [Header("시스템 연결")]
    [SerializeField] private SubCreatureController controller;
    [SerializeField] private SubCreatureSensor sensor;

    [Header("카메라 관절 모터")]
    [Tooltip("좌우 회전(Pan)을 담당하는 부모 뼈대")]
    [SerializeField] private Transform horizontalAxis;
    [Tooltip("상하 회전(Tilt)을 담당하는 자식 뼈대")]
    [SerializeField] private Transform verticalAxis;
    [SerializeField] private float trackingSpeed = 4f;

    [Header("상하 회전 한계치 (수직축)")]
    [Tooltip("최대 올려다보는 각도 (마이너스 값)")]
    [SerializeField] private float minTilt = -30f;
    [Tooltip("최대 내려다보는 각도 (플러스 값)")]
    [SerializeField] private float maxTilt = 60f;

    [Header("시각 연출 (불빛)")]
    [SerializeField] private Renderer cameraRenderer;
    [SerializeField] private int lensMaterialIndex = 1;
    [SerializeField] private Material offMaterial;
    [SerializeField] private Material onMaterial;

    [Header("청각 연출 (오디오)")]
    [Tooltip("기공사가 만든 다중 채널 스피커 장치 연결")]
    [SerializeField] private MultiAudioTrigger audioTrigger;
    [Tooltip("재생할 신호 타입 (예: SoundType.CameraTracking 등)")]
    [SerializeField] private SoundType trackingSoundType;

    // ─── 내부 기억 장치 ───
    private bool isLensOn = false;
    private bool isTrackingPlayer = false; // 현재 플레이어를 노려보며 소리를 내고 있는지 여부

    private void Start()
    {
        if (controller == null) controller = GetComponentInParent<SubCreatureController>();
        if (sensor == null) sensor = GetComponentInParent<SubCreatureSensor>();
        if (audioTrigger == null) audioTrigger = GetComponentInChildren<MultiAudioTrigger>();

        SetLensMaterial(false);
    }

    private void Update()
    {
        if (controller == null) return;

        if (controller.Object == null || !controller.Object.IsValid) return;

        // 1. 상태에 따른 렌즈 불빛 제어
        bool isActive = (controller.NetState != SubCreatureState.Inactive);
        SetLensMaterial(isActive);

        // 2. 비활성화 상태면 모터 전원 차단 (보던 방향 그대로 굳어버림!)
        if (!isActive)
        {
            StopTrackingSound();
            return;
        }

        // 3. 활성화 상태면 센서 범위 내 플레이어 추적 시도
        TrackPlayer();
    }

    private void SetLensMaterial(bool turnOn)
    {
        if (cameraRenderer == null || onMaterial == null || offMaterial == null) return;
        if (isLensOn == turnOn) return;

        isLensOn = turnOn;

        Material[] mats = cameraRenderer.materials;
        if (mats.Length > lensMaterialIndex)
        {
            mats[lensMaterialIndex] = turnOn ? onMaterial : offMaterial;
            cameraRenderer.materials = mats;
        }
    }

    private void TrackPlayer()
    {
        if (horizontalAxis == null || verticalAxis == null || sensor == null) return;

        List<PlayerController> targets = sensor.GetPlayersInRange();

        // 범위 내에 플레이어가 없으면 추적 중지 (방향은 유지)
        if (targets.Count == 0)
        {
            StopTrackingSound();
            return;
        }

        // 가장 첫 번째로 감지된 플레이어를 타겟으로 삼음
        Transform target = targets[0].transform;
        PlayTrackingSound(); // 추적 시작 (소리 재생)

        // =========================================================
        // [1단계: 좌우 회전 (Horizontal)] - 무제한 회전
        // =========================================================
        Vector3 dirToTarget = target.position - horizontalAxis.position;
        dirToTarget.y = 0f;
        if (dirToTarget.sqrMagnitude > 0.001f)
        {
            Quaternion targetPan = Quaternion.LookRotation(dirToTarget);
            horizontalAxis.rotation = Quaternion.Slerp(horizontalAxis.rotation, targetPan, Time.deltaTime * trackingSpeed);
        }

        // =========================================================
        // [2단계: 상하 회전 (Vertical) + 한계치 클램프 적용]
        // =========================================================
        // 타겟의 위치를 부모(Horizontal) 기준의 '로컬 좌표계'로 변환
        Vector3 localTargetPos = horizontalAxis.InverseTransformPoint(target.position);

        // 로컬 좌표계에서 Y(높이)와 Z(거리)를 이용해 상하 기울기(Pitch) 각도를 수학적으로 계산
        float targetPitch = -Mathf.Atan2(localTargetPos.y, localTargetPos.z) * Mathf.Rad2Deg;

        // 기공사가 설정한 각도(-30 ~ 60) 안으로 강제 고정!
        float clampedPitch = Mathf.Clamp(targetPitch, minTilt, maxTilt);

        // 클램핑된 각도로 로컬 회전 목표치 생성 (X축만 회전)
        Quaternion targetLocalRot = Quaternion.Euler(clampedPitch, 0f, 0f);

        // 부드럽게 Slerp 적용
        verticalAxis.localRotation = Quaternion.Slerp(verticalAxis.localRotation, targetLocalRot, Time.deltaTime * trackingSpeed);
    }

    // ─── 오디오 제어 모듈 ───
    private void PlayTrackingSound()
    {
        if (isTrackingPlayer) return; // 이미 소리를 내고 있다면 무시

        isTrackingPlayer = true;
        if (audioTrigger != null)
            audioTrigger.PlaySound(trackingSoundType);
    }

    private void StopTrackingSound()
    {
        if (!isTrackingPlayer) return; // 이미 꺼져 있다면 무시

        isTrackingPlayer = false;
        if (audioTrigger != null)
            audioTrigger.StopSound();
    }
}