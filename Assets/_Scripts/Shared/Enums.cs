using UnityEngine;



// ── 플레이어 ──────────────────────────────────────────────

// 플레이어 역할 아이템
public enum PlayerRole
{
    None,           
    WalkieTalkie,       // 무전기
    Flashlight          // 손전등
}

// 플레이어 생존 상태
public enum PlayerState
{
    Alive,              // 생존 상태
    Captured,           // 크리처에게 납치된 (게이지 진행 중)
    Dead,               // 후유증 게이지 100% 도달 -> 사망
    Escaped,            // 탈출 성공
    Spectating          // 관전 모드 (사망 또는 탈출 후)
}

// ── 구역 ──────────────────────────────────────────────────

public enum  Zone
{
    ZoneA,
    ZoneB
}

// ── 크리처 ────────────────────────────────────────────────

// 추후 추가

// ── 소리 ──────────────────────────────────────────────────

// 크리쳐 감지용 소리 종류
public enum SoundType
{
    Footstep,           // 발소리
    Voice,              // 말소라 (마이크 입력)
    WalkieTalkie,       // 무전기 소리 + 전자음
    Items,              // 그 외 아이템에서 나는 소리
}

// ── 아이템 ────────────────────────────────────────────────

// 게임 내 아이템 종류
// 추가할 아이템 있으면 그 때 추가
public enum ItemType
{
    None,
    WalkieTalkie,       // 무전기
    Flashlight,         // 손전등
    FrontDoorKey,       // 정문 열쇠
    MasterKey,          // 마스터 키
}

// ── 탈출 ──────────────────────────────────────────────────

// 탈출 위치
public enum EscapeRoute
{
    FrontDoor,          // 정문
    Rooftop,            // 옥상
}

// ── 게임 전체 ────────────────────────────────────────────

// 게임 진행 상태 (클리어/실패는 별도 처리)
public enum GameState
{
    Loading = 0,        // 모든 클라이언트 로딩 완료 대기
    Playing = 1         // 게임 진행 중
}