using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 서브 크리처(감시 카메라)의 클라이언트 측 시각 연출 전담 부품.
/// (상하좌우 통합 단일 모터 적용, Z축 위(Up) 모델 완벽 교정)
/// </summary>
public class SecurityCameraVisual : MonoBehaviour
{
    [Header("시스템 연결")]
    [SerializeField] private SubCreatureController controller;
    [SerializeField] private SubCreatureSensor sensor;

    [Header("카메라 통합 모터 (단일 관절)")]
    [Tooltip("상하좌우 모두 회전할 단일 뼈대 (def_horizontal_axis를 여기에 넣으세요)")]
    [SerializeField] private Transform cameraPivot;
    [SerializeField] private float trackingSpeed = 4f;

    [Header("조준선 영점 및 축 보정")]
    [Tooltip("플레이어의 발(Root)에서 이 수치만큼 위(Up)를 조준합니다.")]
    [SerializeField] private float targetHeightOffset = 1.5f;

    [Tooltip("3D 모델의 꼬인 축을 풀어주는 보정 나사. (Z가 위, Y가 앞이라면 보통 X축 90 또는 -90을 넣으면 렌즈가 정면을 봅니다)")]
    [SerializeField] private Vector3 modelAxisOffset = new Vector3(90f, 0f, 0f);

    [Header("시각 연출 (불빛)")]
    [SerializeField] private Renderer cameraRenderer;
    [SerializeField] private int lensMaterialIndex = 1;
    [SerializeField] private Material offMaterial;
    [SerializeField] private Material onMaterial;

    [Header("청각 연출")]
    [SerializeField] private MultiAudioTrigger audioTrigger;
    [SerializeField] private SoundType trackingSoundType;

    private bool isLensOn = false;
    private bool isTrackingPlayer = false;

    private void Start()
    {
        // 부품 자동 연결망
        if (controller == null) controller = GetComponentInParent<SubCreatureController>();
        if (sensor == null) sensor = GetComponentInParent<SubCreatureSensor>();
        if (audioTrigger == null) audioTrigger = GetComponentInChildren<MultiAudioTrigger>();

        SetLensMaterial(false);
    }

    private void Update()
    {
        if (controller == null || controller.Object == null || !controller.Object.IsValid) return;

        bool isActive = (controller.NetState != SubCreatureState.Inactive);
        SetLensMaterial(isActive);

        if (!isActive)
        {
            StopTrackingSound();
            return;
        }

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

    /// <summary>
    /// 단일 관절(볼 조인트) 추적 구동 엔진
    /// </summary>
    private void TrackPlayer()
    {
        if (cameraPivot == null || sensor == null) return;

        List<PlayerController> targets = sensor.GetPlayersInRange();

        // 1. 센서 밖으로 나가면 얼음(동결)
        if (targets.Count == 0)
        {
            StopTrackingSound();
            return;
        }

        Transform target = targets[0].transform;
        PlayTrackingSound();

        // 2. 가슴 높이 조준선 (절대 좌표)
        Vector3 adjustedTarget = target.position + Vector3.up * targetHeightOffset;

        // 🚨 디버그 레이저 (Scene 뷰에서 확인)
        Debug.DrawLine(cameraPivot.position, adjustedTarget, Color.red);

        // 3. 카메라 관절에서 타겟을 향하는 방향 벡터
        Vector3 dirToTarget = adjustedTarget - cameraPivot.position;

        if (dirToTarget.sqrMagnitude > 0.001f)
        {
            // [1단계] 타겟을 정직하게 바라보는 '표준 유니티 각도'를 구함
            Quaternion standardLookRot = Quaternion.LookRotation(dirToTarget);

            // [2단계] 기공사가 알려준 꼬인 뼈대(Z가 위)를 교정하기 위해 오프셋 보정치를 곱해줌
            Quaternion correctedRot = standardLookRot * Quaternion.Euler(modelAxisOffset);

            // [3단계] 스무스하게 모터 구동!
            cameraPivot.rotation = Quaternion.Slerp(cameraPivot.rotation, correctedRot, Time.deltaTime * trackingSpeed);
        }
    }

    private void PlayTrackingSound()
    {
        if (isTrackingPlayer) return;

        isTrackingPlayer = true;
        if (audioTrigger != null)
            audioTrigger.PlaySound(trackingSoundType);
    }

    private void StopTrackingSound()
    {
        if (!isTrackingPlayer) return;

        isTrackingPlayer = false;
        if (audioTrigger != null)
            audioTrigger.StopSound();
    }
}