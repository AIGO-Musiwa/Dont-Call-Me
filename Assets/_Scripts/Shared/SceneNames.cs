using UnityEngine;

// Build Settings 씬 순서와 반드시 일치해야 함
// 씬 추가/변경 시 팀 전체 공지 필수
public static class SceneNames
{
    public const string Title = "Title";
    public const string Lobby = "Lobby";
    public const string Game  = "Game";

    // Build Settings 인덱스 (File > Build Settings 순서와 동일하게 유지)
    public const int TitleIndex = 0;
    public const int LobbyIndex = 1;
    public const int GameIndex  = 2;
}
