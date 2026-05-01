using Fusion;
using UnityEngine;

[CreateAssetMenu(fileName = "CharacterprefabRegistry", menuName = "Don't Call Me/CharacterPrefabRegistry")]
public class CharacterprefabRegistry : ScriptableObject
{
    [Header("대기실 씬 전용 플레이어 캐릭터 프리팹")]
    [SerializeField] private GameObject[] lobbyCharacterPrefabs;

    [Header("인게임 전용 플리에이어 캐릭터 프리팹 (NetworkObject)")]
    [SerializeField] private NetworkObject[] ingameCharacterPrefabs;

    [Header("결과 화면용 얼굴 스프라이트 (Image)")]
    [SerializeField] private Sprite[] characterFaceSprites;

    // 등록된 캐릭터 수
    public int Count => lobbyCharacterPrefabs != null ? lobbyCharacterPrefabs.Length : 0;

    // 대기실에서 로컬 생성할 프리팹 반환
    public GameObject GetLobbyPrefab(int index)
    {
        if (!IsValidIndex(index, lobbyCharacterPrefabs))
        {
            Debug.LogWarning($"[CharacterPrefabRegistry] GetLobbyPrefab: 잘못된 인덱스({index})");
            return null;
        }
        return lobbyCharacterPrefabs[index];
    }

    // 인게임에서 소환할 플레이어 프리팹 반환
    public NetworkObject GetIngamePrefab(int index)
    {
        if (!IsValidIndex(index, ingameCharacterPrefabs))
        {
            Debug.LogWarning($"[CharacterPrefabRegistry] GetIngamePrefab: 잘못된 인덱스({index})");
            return null;
        }
        return ingameCharacterPrefabs[index];
    }

    // 결과화면 Image에 연결할 얼굴 스프라이트 반환
    public Sprite GetFaceSprite(int index)
    {
        if (!IsValidIndex(index, characterFaceSprites))
        {
            Debug.LogWarning($"[CharacterPrefabRegistry] GetFaceSprite: 잘못된 인덱스({index})");
            return null;
        }
        return characterFaceSprites[index];
    }

    private bool IsValidIndex<T>(int index, T[] array)
        => array != null && index >= 0 && index < array.Length;
}
