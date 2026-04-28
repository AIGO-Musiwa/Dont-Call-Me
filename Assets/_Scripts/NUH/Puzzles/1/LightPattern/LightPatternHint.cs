using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 점등 패턴 퍼즐의 힌트 반복 재생 담당.
/// 같은 seed로 정답 패턴을 재구성해서 시작 직후부터 반복 재생한다.
/// 힌트 전구는 PointLight + Material Emission을 함께 On/Off한다.
/// 클리어되면 꺼진 상태로 정지한다.
/// </summary>
public class LightPatternHint : MonoBehaviour, IPuzzleSeedReceiver
{
    [Header("설정")]
    [SerializeField] private int gridCount = 9;                          // 3x3 전체 칸 수
    [SerializeField] private int patternLength = 9;                      // 정답 패턴 길이

    [Header("힌트 전구 View")]
    [SerializeField] private List<LightPatternPuzzleView> hintBulbs = new(); // 힌트 전구 View 9개

    [Header("개별 재생 시간")]
    [SerializeField] private float hintOnTime = 0.8f;                    // 개별 전구 켜짐 시간
    [SerializeField] private float hintOffTime = 0.2f;                   // 다음 패턴 전 짧은 간격

    [Header("루프 종료 후 전체 깜빡임")]
    [SerializeField] private float fullBlinkOnTime = 0.35f;              // 전체 깜빡임 켜짐 시간
    [SerializeField] private float loopDelay = 3f;                       // 다음 루프 전 대기 시간

    [Header("정지 조건")]
    [SerializeField] private LightPatternPuzzle observedPuzzle;          // 클리어 여부를 확인할 퍼즐 본체
    [SerializeField] private bool stopOnSolved = true;                   // 클리어 시 힌트 정지 여부

    [Header("디버그")]
    [SerializeField] private bool enableDebugLog = true;                 // 디버그 로그 여부

    private readonly List<int> _answerSequence = new();                  // 재생할 정답 패턴
    private bool _hasAnswerSeed;                                         // 시드 적용 완료 여부
    private Coroutine _loopRoutine;                                      // 반복 재생 코루틴

    private void Awake()
    {
        TurnAllBulbs(false);
    }

    private void OnEnable()
    {
        TryStartLoop();
    }

    private void OnDisable()
    {
        if (_loopRoutine != null)
        {
            StopCoroutine(_loopRoutine);
            _loopRoutine = null;
        }

        TurnAllBulbs(false);
    }

    /// <summary>
    /// 힌트가 관찰할 퍼즐 본체를 연결한다.
    /// </summary>
    public void SetObservedPuzzle(LightPatternPuzzle puzzle)
    {
        observedPuzzle = puzzle;
        Log($"ObservedPuzzle 연결 완료 : {(puzzle != null ? puzzle.name : "null")}");
    }

    /// <summary>
    /// 같은 seed로 정답 패턴을 재구성한다.
    /// 패턴 길이는 9, 중복 허용.
    /// </summary>
    public void ApplyAnswerSeed(int seed)
    {
        _answerSequence.Clear();

        SeedRandom rng = new SeedRandom(seed);

        for (int i = 0; i < patternLength; i++)
        {
            int index = rng.NextInt(0, gridCount);
            _answerSequence.Add(index);
        }

        _hasAnswerSeed = true;
        Log($"힌트 시드 적용 완료 | seed = {seed}");

        TryStartLoop();
    }

    /// <summary>
    /// 시드가 준비됐고 아직 루프가 없으면 반복 재생을 시작한다.
    /// </summary>
    private void TryStartLoop()
    {
        if (!_hasAnswerSeed)
            return;

        if (_loopRoutine != null)
            return;

        _loopRoutine = StartCoroutine(CoLoopPattern());
    }

    /// <summary>
    /// 패턴 반복 재생 코루틴.
    /// 9개 순서 재생 -> 전체 한 번 깜빡임 -> 대기 -> 반복.
    /// </summary>
    private IEnumerator CoLoopPattern()
    {
        while (true)
        {
            if (ShouldStopLoop())
            {
                TurnAllBulbs(false);
                _loopRoutine = null;
                yield break;
            }

            for (int i = 0; i < _answerSequence.Count; i++)
            {
                int index = _answerSequence[i];

                if (index >= 0 && index < hintBulbs.Count && hintBulbs[index] != null)
                {
                    hintBulbs[index].SetHintActive(true);
                    yield return new WaitForSeconds(hintOnTime);
                    hintBulbs[index].SetHintActive(false);
                }

                yield return new WaitForSeconds(hintOffTime);
            }

            TurnAllBulbs(true);
            yield return new WaitForSeconds(fullBlinkOnTime);
            TurnAllBulbs(false);

            yield return new WaitForSeconds(loopDelay);
        }
    }

    /// <summary>
    /// 퍼즐이 풀렸으면 힌트 반복 재생을 멈출지 결정한다.
    /// </summary>
    private bool ShouldStopLoop()
    {
        if (!stopOnSolved)
            return false;

        if (observedPuzzle == null)
            return false;

        return observedPuzzle.IsSolved;
    }

    /// <summary>
    /// 힌트 전구 전체 On/Off.
    /// 각 전구의 색상은 LightPatternPuzzleView 인스펙터 값으로 결정된다.
    /// </summary>
    private void TurnAllBulbs(bool isOn)
    {
        for (int i = 0; i < hintBulbs.Count; i++)
        {
            if (hintBulbs[i] == null)
                continue;

            hintBulbs[i].SetHintActive(isOn);
        }
    }

    /// <summary>
    /// 디버그 로그를 출력한다.
    /// </summary>
    private void Log(string message)
    {
        if (!enableDebugLog)
            return;

        Debug.Log($"[LightPatternHint] {message}", this);
    }
}