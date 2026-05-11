using UnityEngine;

[CreateAssetMenu(fileName = "SounddBSetting", menuName = "Don't Call Me/SounddBSetting")]
public class SounddBSetting : ScriptableObject
{
    // ── 측정 기본값 ──────────────────────────────────────────

    [Header("마이크 측정 기본값")]
    [Tooltip("측정 주기 (초). 낮을수록 반응이 빠르지만 CPU 부하 증가.")]
    public float measureInterval = 0.05f;

    [Tooltip("이 값(RMS 진폭) 미만이면 무음으로 판정.")]
    public float silenceThreshold = 0.001f;

    // ── PTT 무음 시 화이트노이즈 ─────────────────────────────

    [Header("PTT 화이트 노이즈")]
    [Tooltip("PTT 활성 중 무음일 때 발행하는 무전 dB")]
    public float whiteNoisedB = 30f;

    // ── dBFS → 자연음 dB 변환 테이블 ─────────────────────────

    [Header("dBFS → 자연음 dB 변환 테이블")]
    [Tooltip("이 dBFS 이하이면 Whisper 판정.")]
    public float naturalThreshold_Whisper = -45f;
    [Tooltip("Whisper 판정 시 출력 자연음 dB.")]
    public float naturalDB_Whisper = 30f;

    [Tooltip("이 dBFS 이하이면 Normal 판정.")]
    public float naturalThreshold_Normal = -30f;
    [Tooltip("Normal 판정 시 출력 자연음 dB.")]
    public float naturalDB_Normal = 38f;

    [Tooltip("이 dBFS 이하이면 Loud 판정.")]
    public float naturalThreshold_Loud = -20f;
    [Tooltip("Loud 판정 시 출력 자연음 dB.")]
    public float naturalDB_Loud = 41f;

    [Tooltip("위 임계값 초과 시 (고함) 출력 자연음 dB.")]
    public float naturalDB_Shout = 43f;

    // ── dBFS → 무전음 dB 변환 테이블 ─────────────────────────

    [Header("dBFS → 무전음 dB 변환 테이블")]
    [Tooltip("이 dBFS 이하이면 Whisper 판정.")]
    public float walkieThreshold_Whisper = -45f;
    [Tooltip("Whisper 판정 시 출력 무전음 dB.")]
    public float walkieDB_Whisper = 36f;

    [Tooltip("이 dBFS 이하이면 Normal 판정.")]
    public float walkieThreshold_Normal = -30f;
    [Tooltip("Normal 판정 시 출력 무전음 dB.")]
    public float walkieDB_Normal = 44f;

    [Tooltip("이 dBFS 이하이면 Loud 판정.")]
    public float walkieThreshold_Loud = -20f;
    [Tooltip("Loud 판정 시 출력 무전음 dB.")]
    public float walkieDB_Loud = 47f;

    [Tooltip("위 임계값 초과 시 (고함) 출력 무전음 dB.")]
    public float walkieDB_Shout = 49f;

    // ── 발소리 dB ─────────────────────────────────────────────

    [Header("발소리 dB")]
    public float footstepDB_Crouch = 26f;
    public float footstepDB_Walk = 31f;
    public float footstepDB_Run = 34f;

    // ── 퍼즐 ──────────────────────────────────────────────────

    [Header("퍼즐 실패음")]
    [Tooltip("퍼즐 오답 시 발행하는 무전음 dB. 기획서 기준 '무전 큰 소리 1초'에 준하는 수준.")]
    public float puzzleFailSounddB = 49f;

    // ── 라디오 ────────────────────────────────────────────────

    [Header("라디오 수리 / 유인")]
    [Tooltip("수리 Hold 중 매 틱 발행하는 자연음 dB.")]
    public float radioRepairSounddB = 41f;

    [Tooltip("라디오 작동 중 주기적으로 발행하는 유인 무전음 dB.")]
    public float radioLureSounddB = 47f;

    [Tooltip("유인 소리 발행 주기 (초).")]
    public float radioSoundInterval = 0.5f;
}
