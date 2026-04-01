
// 팀 구역 enum
public enum ZoneType { ZoneA, ZoneB }

// 크리쳐 상태
// 추후 추가


// 게임 상태
public enum GamePhase
{
    Loading,    // 로딩화면
    Playing,    // 게임 중
    GameClear,  // 클리어
    GameOver    // 패배
}

// 게임 진행 중 플레이어 상태
public enum PlayerState
{
    Normal,     // 평소 상태
    Captured,   // 잡혔을 때
    Dead,       // 사망
    Escaped     // 탈출
}

// 왼손 역할 아이템
public enum RoleItemType
{
    None,           // 없음 (크리처에게 끌려가서 아이템을 떨어뜨렸을 때)
    WalkieTalkie,   // 무전기
    Flashlight      // 손전등
}

// 열쇠 종류
public enum KeyType
{
    FrontDoorKey,   // 정문 열쇠
    MasterKey,      // 마스터 키
}

// 탈출구 종류
public enum EscapeRouteType
{
    FrontDoor,      // 정문
    Rooftop,        // 옥상
}

// 크리처 소리 감지 종류
public enum SoundType
{
    electronic,     // 전자음 (무전기 등)
    Voice,          // 말소리
    Footstep,       // 발소리
    items           // 그 외 오브젝트에서 나는 소리
}