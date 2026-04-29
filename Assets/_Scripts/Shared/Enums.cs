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

// 결과 확인용 플레이어 이벤트 종류
public enum GameEventType
{
    Captured,
    Rescued,
    Dead,
    Escaped
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

public enum PlacementSlotUsageType
{
    HintOnly = 0,       // 힌트만 배치 가능
    PuzzleOrHint = 1    // 퍼즐 / 힌트 모두 배치 가능
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

    Reagent,            // 시약 퍼즐
}

/// <summary>
/// 게임 내 모든 오브젝트가 공유하는 범용 사운드 신호 규격
/// </summary>
public enum SoundType
{
    // [퍼즐 공통]
    Success,        // 정답/클리어
    Fail,           // 오답/실패

    // [조작계]
    InteractLight,  // 가벼운 터치, 다이얼 1칸
    InteractHeavy,  // 묵직한 레버 당김
    Connect,        // 전선 꽂음
    Disconnect,     // 전선 뽑음

    // [기계 작동]
    MechanicalMove, // 문 열림, 기어 돌아감
    BeepSmall,      // 힌트 전구 개별 점등
    BeepLarge,      // 힌트 전구 전체 점등

    // [라디오 전용]
    RadioRepair,    // 수리 중 나는 소리 (드라이버 돌리는 소리 등)
    RadioActive     // 라디오 작동 소리 (음악, 방송 소리 등)
}

// ── 무전기 ────────────────────────────────────────────────

public enum  WalkieState
{
    Idle,
    TX,
    RX
}

// ── 라디오 ────────────────────────────────────────────────
public enum RadioState
{
    Broken,
    InProgress,
    Ready,
    Active,
    Disabled
}

// ── 퍼즐 ──────────────────────────────────────────────────

public enum LightPatternPanelVisualState : byte
{
    Off = 0,
    InputYellow = 1,
    FailRed = 2,
    SolvedGreen = 3
}

public enum ViewRole
{
    HintBulb = 0,    // 힌트 전구
    PuzzleLamp = 1,  // 퍼즐 상태 램프
    PanelPad = 2     // 입력 패드
}

public enum RotationDirection
{
    Left,
    Right
}

public enum WireSocketColor
{
    Red = 0,
    DarkOrange = 1,
    Yellow = 2,
    Green = 3,
    Blue = 4,
    DarkGray = 5,
    Purple = 6,
    White = 7,
    Black = 8
}

public enum NumericHintType
{
    Clock = 0,
    Drawer = 1,
    Book = 2,
    Frame = 3
}

public enum NumericBookColor
{
    Red = 0,
    Green = 1,
    Blue = 2,
    Yellow = 3
}

public enum MazeMoveDirection 
{
    Up,
    Down,
    Left,
    Right
}

public enum ReagentType
{
    None = 0,
    ReagentA = 1,
    ReagentB = 2,
    ReagentC = 3,
    ReagentD = 4,
    ReagentE = 5,
    ReagentF = 6,
}

public enum ReagentActionType
{
    None = 0,
    Heat = 1,
    Cool = 2,
}

public enum ReagentButtonType
{
    Previous = 0,
    Next = 1,
    Confirm = 2,
    StartCraft = 3,
    Heat = 4,
    Cool = 5,
}
// ── 탈출 ──────────────────────────────────────────────────

// 탈출 위치
public enum EscapeRoute
{
    FrontDoor,          // 정문
    Rooftop,            // 옥상
}

// 탈출 상호작용 종류
public enum EscapeInteractType
{
    EscapeButton,       // 3단계 퍼즐 방에 있는 3막 진입용 동시 입력 버튼
    FrontDoor,          // 1층 정문 탈출구
    RooftopDoor         // 3층 옥상 탈출구
}

// ── 게임 전체 ────────────────────────────────────────────

// 게임 진행 상태 (클리어/실패는 별도 처리)
public enum GameState
{
    Loading = 0,        // 모든 클라이언트 로딩 완료 대기
    Playing = 1         // 게임 진행 중
}

// ── UI ──────────────────────────────────────────────────

// 마이크 레벨 게이지
public enum GaugeDirection
{
    Horizontal,
    Vertical
}