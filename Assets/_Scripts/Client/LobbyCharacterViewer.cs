using UnityEngine;

public class LobbyCharacterViewer : MonoBehaviour
{
    public static LobbyCharacterViewer Instance { get; private set; }

    [Header("캐릭터 레지스트리")]
    [SerializeField] private CharacterprefabRegistry characterRegistry;

    [Header("슬롯별 카메라 (인덱스 = SlotIndex)")]
    [SerializeField] private Camera[] slotCameras;

    [Header("슬롯별 스폰 위치 (인덱스 = SlotIndex)")]
    [SerializeField] private Transform[] spawnPoints;

    [Header("슬롯 UI (인덱스 = SlotIndex)")]
    [SerializeField] private PlayerSlotUI[] playerSlots;

    [Header("RenderTexture 해상도")]
    [SerializeField] private int renderTextureWidth = 500;
    [SerializeField] private int renderTextureHeight = 900;

    // 슬롯별 현재 인스턴스화된 캐릭터 오브젝트
    private readonly GameObject[] characterInstances = new GameObject[4];

    // 슬롯별 RenderTexture
    private readonly RenderTexture[] renderTextures = new RenderTexture[4];

    #region Unity LifeCycle

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        // RenderTexture 해제
        for(int i = 0; i< renderTextures.Length; i++)
        {
            if (renderTextures[i] != null)
            {
                renderTextures[i].Release();
                renderTextures[i] = null;
            }
        }
    }

    private void Start()
    {
        // 씬 시작 시 RenderTexutre 생성 및 카메라 연결
        for (int i = 0; i < slotCameras.Length; i++)
        {
            if (slotCameras[i] == null) continue;

            renderTextures[i] = new RenderTexture(renderTextureWidth, renderTextureHeight, 16);
            slotCameras[i].targetTexture = renderTextures[i];
        }

        // 이미 씬에 PlayerData가 존재하는 경우 재스킨
        RebuildFromExistingPlayerData();
    }

    #endregion

    #region 공개 API

    // 해당 슬롯에 캐릭터 생성 및 연결
    public void OnCharacterAssigned(int slotIndex, int characterIndex)
    {
        if (!IsValidSlot(slotIndex)) return;

        // 기존 인스턴스 제거
        ClearSlot(slotIndex);

        // 프리팹 가져오기
        GameObject prefab = characterRegistry?.GetLobbyPrefab(characterIndex);
        if (prefab == null)
        {
            Debug.LogWarning($"[LobbyCharacterViewer] 프리팹 없음 | CharacterIndex={characterIndex}");
            return;
        }

        // 스폰 위치에 생성
        Transform spawnPoint = spawnPoints[slotIndex];
        if (spawnPoint == null)
        {
            Debug.LogWarning($"[LobbyCharacterViewer] spawnPoint 없음 | SlotIndex={slotIndex}");
            return;
        }

        characterInstances[slotIndex] = Instantiate(prefab, spawnPoint.position, spawnPoint.rotation);

        // SlotUI에 RenderTexture 연결
        if (playerSlots != null && slotIndex < playerSlots.Length && playerSlots[slotIndex] != null)
            playerSlots[slotIndex].SetCharacter(renderTextures[slotIndex]);

        Debug.Log($"[LobbyCharacterViewer] 캐릭터 생성 | SlotIndex={slotIndex} | CharacterIndex={characterIndex}");
    }

    // 플레이어 퇴장 시 해당 슬롯 캐릭터 제거
    public void OnPlayerLeft(int slotIndex)
    {
        if (!IsValidSlot(slotIndex)) return;

        ClearSlot(slotIndex);

        // SlotUI Rendertexture 해제
        if (playerSlots != null && slotIndex < playerSlots.Length && playerSlots[slotIndex] != null)
            playerSlots[slotIndex].ClearCharacter();

        Debug.Log($"[LobbyCharacterViewer] 캐릭터 제거 | SlotIndex={slotIndex}");
    }

    #endregion

    #region 내부

    // 씬 복귀 시 이미 존재하는 PlayerData로부터 캐릭터 재생성
    public void RebuildFromExistingPlayerData()
    {
        var allData = FindObjectsByType<PlayerData>(FindObjectsSortMode.None);
        foreach (var data in allData)
        {
            if (data.SlotIndex >= 0 && data.CharacterIndex >= 0)
                OnCharacterAssigned(data.SlotIndex, data.CharacterIndex);
        }
    }

    private void ClearSlot(int slotIndex)
    {
        if (characterInstances[slotIndex] != null)
        {
            Destroy(characterInstances[slotIndex]);
            characterInstances[slotIndex] = null;
        }
    }

    private bool IsValidSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= characterInstances.Length)
        {
            Debug.LogWarning($"[LobbyCharacterViewer] 유효하지 않은 슬롯 인덱스: {slotIndex}");
            return false;
        }
        return true;
    }

    #endregion
}
