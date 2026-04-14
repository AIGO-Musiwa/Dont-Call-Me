using UnityEngine;

/// <summary>
/// 스폰된 퍼즐 프리팹 루트에 붙는 보조 스크립트
/// 
/// 역할
/// - 1단계 진행도 등록 시 대표 퍼즐이 누구인지 알려줌
/// - 2단계 화면 On/Off 대상으로 어떤 오브젝트를 써야 하는지 알려줌
/// </summary>
public class PuzzleSpawnEntry : MonoBehaviour
{
    [Header("진행도 대표 퍼즐")]
    [SerializeField] private PuzzleInteractableBase progressTarget;     // 1단계 집계용 대표 퍼즐

    [Header("2단계 퍼즐 화면 루트")]
    [SerializeField] private GameObject stage2ScreenRoot;               // 2단계 On/Off 대상화면 루트

    public PuzzleInteractableBase ProgressTarget => progressTarget;
    public GameObject Stage2ScreenRoot => stage2ScreenRoot != null ? stage2ScreenRoot : gameObject;
}
