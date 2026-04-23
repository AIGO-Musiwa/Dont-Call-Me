using UnityEngine;

public static class Constants
{
    // ── 방 / 네트워크 ─────────────────────────────────────
    public const int MAX_PLAYERS = 4;
    public const int ROOM_CODE_LENGTH = 6;

    // ── 닉네임 제한 ───────────────────────────────────────
    public const int NICKNAME_MAX_LENGTH = 16;

    // ── 납치 게이지 ───────────────────────────────────────
    public const float CAPTURE_GAUGE_MAX = 100f;

    // 납치 시 즉시 증가량 1, 2, 3회차 이후
    public const float CAPTURE_GAUGE_INSTANT_1ST = 10f;
    public const float CAPTURE_GAUGE_INSTANT_2ND = 20f;
    public const float CAPTURE_GAUGE_INSTANT_3RD_PLUS = 30f;

    // ── 음성 그룹 ───────────────────────────────────────── 
    public const byte GROUP_LOBBY = 0;
    public const byte GROUP_ZONE_A = 1;
    public const byte GROUP_ZONE_B = 2;
    public const byte GROUP_WALKIE = 3;
    public const byte GROUP_SPECTATOR = 4;

    // ── 무전기 ────────────────────────────────────────────
    public const float WALKIE_RANGE = 8f;       // 무전기 소리 범위
}

// ── 씬 이름 (Build Settings의 씬 이름과 반드시 일치) ────
public static class SceneNames
{
    public const string TITLE = "Title";
    public const string LOBBY = "Lobby";
    public const string GAME = "Game";

    public const int TITLE_INDEX = 0;
    public const int LOBBY_INDEX = 1;
    public const int GAME_INDEX = 2;
}