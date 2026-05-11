using UnityEngine;

/// <summary>
/// 인게임 씬 진입 시 중앙 BGMManager에게
/// "이 카트리지를 틀어라!" 하고 명령만 전달하는 단방향 통신 장치.
/// </summary>
public class InGameBGMManager : MonoBehaviour
{
    [Header("인게임 BGM 카트리지 (비워두면 메인 BGM 꺼짐)")]
    [SerializeField] private AudioEventSO inGameBGM;

    private void Awake()
    {
        // 중앙 방송국(BGMManager)이 살아서 돌아가고 있는지 확인
        if (BGMManager.Instance != null)
        {
            // 중앙 방송국에 카트리지 투입! (무한 반복 모드)
            // 💡 만약 inGameBGM이 비어있다면, BGMManager가 알아서 전원을 차단함!
            BGMManager.Instance.PlayBGM(inGameBGM, true);
        }
        else
        {
            Debug.LogWarning("[InGameBGMManager] 중앙 BGMManager를 찾을 수 없습니다.");
        }
    }
}