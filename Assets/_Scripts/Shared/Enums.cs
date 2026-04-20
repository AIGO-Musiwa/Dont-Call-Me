using UnityEngine;



// ── 플레이어 ──────────────────────────────────────────────

// 플레이어 역할 아이템
public enum PlayerRole
{           
    WalkieTalkie,       //무전기
    Flashlight          //손전등
}

// 플레이어 생존 상태
public enum PlayerState
{
    Normal,             // 생존 상태
    Captured,           // 크리처에게 납치된 (게이지 진행 중)
    Dead,               // 후유증 게이지 100% 도달 -> 사망
    Escaped,            // 탈출 성공
    //Spectating          // 관전 모드 (사망 또는 탈출 후)
}

// 플레이어 숨은 상태
public enum HideState
{
    None,               // 안 숨음
    Cabinet,            
    Desk,               
}

// Capture 내부 상태
public enum CapturePhase
{
    None,
    Transition,         // 연출 / 이동 단계
    Active,             // 실제 포획 당한 후 플레이 단계 (이때부터 1퍼씩 게이지 오름)
}

// ── 구역 ──────────────────────────────────────────────────

public enum  Zone
{
    ZoneA,
    ZoneB
}

public enum PuzzleStage
{
    Stage1,
    Stage2,
    Stage3,
}

// ── 크리처 ────────────────────────────────────────────────

public enum CreatureState
{
    Patrol,             // 순찰
    AlerMove,           // 경계 이동
    Search,             // 수색(탐색)
    Chaser,             // 추척
    Capture             // 포획
}

public enum SearchPhase
{
    None,                   // 수색 아님
    InitialLookAround,      // 최초 제자리 두리번거리기
    MovingToRandomPoint,    // 주변 랜덤 지점으로 1회 이동
    SecondaryLookAround     // 이동한 지점에서 추가 두리번거리기
}

// ── 소리 ──────────────────────────────────────────────────

// 크리쳐 감지용 소리 종류
public enum SoundChannel
{
    Natural,    // 플레이어 음성, 이동음
    Walkie,     // PTT 음성, 화이트 노이즈, 라디오 유인음, 퍼즐 실패음
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

    BasicCube,          // 테스트용 큐브
}

// ── 무전기 ────────────────────────────────────────────────

public enum  WalkieState
{
    Idle,
    TX,
    RX
}

// ── 퍼즐 ──────────────────────────────────────────────────

public enum RotationDirection
{
    Left,
    Right
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