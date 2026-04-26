using Fusion;
using System;
using UnityEngine;

/// <summary>
/// 캐릭터(플레이어/크리처) 특화 오디오 모듈
/// 스마트 발소리 전환 및 액션(포효, 공격 등) 사운드 출력을 담당합니다.
/// </summary>
public class CharacterAudioModule : MonoBehaviour
{
    // 발소리 상태 기어
    public enum FootstepType { Walk, Run, Crouch }

    [Serializable]
    public struct SoundEntry
    {
        public string soundID;      // 애니메이션 이벤트에서 찌를 이름 (예: "Roar", "Attack")
        public AudioEventSO cartridge;
    }

    [Serializable]
    public struct FootstepEntry
    {
        public FootstepType type;
        public AudioEventSO cartridge;
    }

    [Header("스피커 배치 (Audio Sources)")]
    [Tooltip("액션, 포효, 음성 등 주요 사운드 출력")]
    public AudioSource mainSource;
    [Tooltip("발소리, 옷깃 소리 등 중첩 사운드 출력")]
    public AudioSource subSource;

    [Header("3D 물리 엔진 세팅")]
    [SerializeField] private float minDistance = 1f;
    [SerializeField] private float maxDistance = 20f;
    [SerializeField] private AudioRolloffMode rolloffMode = AudioRolloffMode.Logarithmic;

    [Header("사운드 뱅크 (Libraries)")]
    public FootstepEntry[] footstepLibrary; // 발소리 전용 (Walk, Run, Crouch)
    public SoundEntry[] actionLibrary;     // 액션 전용 (포효, 공격 등)

    private NetworkObject netObj;
    private PlayerController controller;

    private void Awake()
    {
        // 1. 소스 자동 연결 및 초기화
        if (mainSource == null) mainSource = GetComponent<AudioSource>();
        if (subSource == null) subSource = gameObject.AddComponent<AudioSource>();

        SetupSource(mainSource);
        SetupSource(subSource);

        // 캐싱
        controller = GetComponentInParent<PlayerController>();
    }

    private void Start()
    {
        netObj = GetComponentInParent<NetworkObject>();
    }

    /// <summary>
    /// 오디오 출력 장치를 연구소 환경(3D)에 맞게 정밀 튜닝합니다.
    /// </summary>
    private void SetupSource(AudioSource source)
    {
        if (source == null) return;
        source.spatialBlend = 1f;         // 100% 3D 공간음 적용
        source.dopplerLevel = 0f;         // 기동 속도에 따른 음 왜곡 방지
        source.rolloffMode = rolloffMode;
        source.minDistance = minDistance;
        source.maxDistance = maxDistance;
        source.playOnAwake = false;
    }

    // ─────────────────────────────────────────────────────────
    // [포트 1] 스마트 발소리 (애니메이션 이벤트용)
    // ─────────────────────────────────────────────────────────

    /// <summary>
    /// 애니메이션 이벤트 함수명: PlayFootstep
    /// 기체의 속도와 앉기 상태를 읽어 자동으로 발소리를 인젝션합니다.
    /// </summary>
    public void PlayFootstep()
    {
        // 부모 오브젝트의 컨트롤러로부터 기체 데이터 수신
        var controller = GetComponentInParent<PlayerController>();

        // 크리처나 단순 오브젝트라 컨트롤러가 없는 경우 기본(Walk) 소리 출력
        if (controller == null)
        {
            PlayFootstepByType(FootstepType.Walk);
            return;
        }

        // 1. 앉기 상태 확인 (네트워크 변수)
        if (controller.NetIsCrouching)
        {
            PlayFootstepByType(FootstepType.Crouch);
        }
        else
        {
            // 2. 이동 속도 확인 (물리 속도)
            float speed = controller.KCCMotor.KCC.RealVelocity.magnitude;

            // 기준 속도(4.5f) 초과 시 뛰기 소리로 자동 변속
            if (speed > 4.5f) PlayFootstepByType(FootstepType.Run);
            else PlayFootstepByType(FootstepType.Walk);
        }
    }

    private void PlayFootstepByType(FootstepType type)
    {
        var entry = Array.Find(footstepLibrary, x => x.type == type);
        // 해당 타입이 비어있다면 0번(기본) 소리로 대체 출력하는 안전 회로
        var cartridge = (entry.cartridge != null) ? entry.cartridge : (footstepLibrary.Length > 0 ? footstepLibrary[0].cartridge : null);
        cartridge?.Play(subSource);

        // 로컬 플레이어일 때만 dB 이벤트 발생
        if (netObj == null || !netObj.HasInputAuthority) return;
        if (controller == null) return;

        SoundEmitter.EmitFootstep(type, transform.position, controller.NetZone, controller);
    }

    // ─────────────────────────────────────────────────────────
    // [포트 2] 액션 및 효과음 (애니메이션 이벤트용)
    // ─────────────────────────────────────────────────────────

    /// <summary>
    /// 애니메이션 이벤트 함수명: PlayAction (String 입력: "Roar", "Attack" 등)
    /// </summary>
    public void PlayAction(string actionID)
    {
        if (string.IsNullOrEmpty(actionID)) return;

        var entry = Array.Find(actionLibrary, s => s.soundID == actionID);
        if (entry.cartridge != null)
        {
            entry.cartridge.Play(mainSource);
        }
        else
        {
            Debug.LogWarning($"[CharacterAudio] '{actionID}' ID를 가진 액션 사운드를 찾을 수 없습니다.");
        }
    }

    /// <summary>
    /// 애니메이션 이벤트 함수명: PlayActionByIndex (Int 입력: 0, 1, 2...)
    /// </summary>
    public void PlayActionByIndex(int index)
    {
        if (index >= 0 && index < actionLibrary.Length)
        {
            actionLibrary[index].cartridge?.Play(mainSource);
        }
    }
}