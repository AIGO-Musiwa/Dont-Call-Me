using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 점등 패턴 퍼즐의 힌트 반복 재생 담당
/// 같은 seed로 정답 패턴을 재구성해서 시작 직후부터 반복 재생한다.
/// 9개 재생이 끝나면 전체 라이트가 한 번 깜빡이고 다음 루프로 넘어간다.
/// 클리어되면 꺼진 상태로 정지한다.
/// </summary>
public class LightPatternHint : MonoBehaviour, IPuzzleSeedReceiver
{
    [Header("설정")]
    [SerializeField] private int gridCount = 9;         // 3x3 전체 칸 수
    [SerializeField] private int patternLength = 9;     // 정답 패턴 길이

    [Header("힌트 라이트")]
    [SerializeField] private List<Light> hintLights = new(); // 힌트 라이트 9개

    [Header("개별 재생 시간")]
    [SerializeField] private float hintOnTime = 0.35f;  // 개별 라이트 켜짐 시간
    [SerializeField] private float hintOffTime = 0.15f; // 다음 패턴 전 짧은 간격

    [Header("루프 종료 후 전체 깜빡임")]
    [SerializeField] private float fullBlinkOnTime = 0.35f; // 전체 깜빡임 켜짐 시간
    [SerializeField] private float loopDelay = 3f;          // 다음 루프 전 대기 시간

    [Header("정지 조건")]
    [SerializeField] private LightPatternPuzzle observedPuzzle; // 클리어 여부를 확인할 퍼즐 본체
    [SerializeField] private bool stopOnSolved = true;          // 클리어 시 힌트 정지 여부

    [Header("디버그")]
    [SerializeField] private bool enableDebugLog = true;

    private readonly List<int> _answerSequence = new(); // 재생할 정답 패턴
    private bool _hasAnswerSeed;                        // 시드 적용 완료 여부
    private Coroutine _loopRoutine;                     // 반복 재생 코루틴

    private void Awake()
    {
        TurnAllLights(false); // 시작 시 전체 라이트 OFF
    }

    private void OnEnable()
    {
        TryStartLoop(); // 활성화되면 재생 가능 여부 확인
    }

    private void OnDisable()
    {
        if (_loopRoutine != null)
        {
            StopCoroutine(_loopRoutine);
            _loopRoutine = null;
        }

        TurnAllLights(false); // 비활성화 시 라이트 정리
    }

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
            int index = rng.NextInt(0, gridCount); // 0~8 중복 허용
            _answerSequence.Add(index);
        }

        _hasAnswerSeed = true;
        Log($"힌트 시드 적용 완료 | seed = {seed}");

        TryStartLoop();
    }

    /// <summary>
    /// 시드가 준비됐고 아직 루프가 없으면 반복 재생 시작
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
    /// 패턴 반복 재생 코루틴
    /// 9개 순서 재생 -> 전체 한 번 깜빡임 -> 대기 -> 반복
    /// </summary>
    private IEnumerator CoLoopPattern()
    {
        while (true)
        {
            if (ShouldStopLoop())
            {
                TurnAllLights(false);
                _loopRoutine = null;
                yield break;
            }

            // 정답 패턴 순서대로 재생
            for (int i = 0; i < _answerSequence.Count; i++)
            {
                int index = _answerSequence[i];

                if (index >= 0 && index < hintLights.Count && hintLights[index] != null)
                {
                    hintLights[index].enabled = true;
                    yield return new WaitForSeconds(hintOnTime);
                    hintLights[index].enabled = false;
                }

                yield return new WaitForSeconds(hintOffTime);
            }

            // 한 바퀴 끝나면 전체 9개 라이트 한 번 깜빡임
            TurnAllLights(true);
            yield return new WaitForSeconds(fullBlinkOnTime);
            TurnAllLights(false);

            yield return new WaitForSeconds(loopDelay);
        }
    }

    /// <summary>
    /// 퍼즐이 풀렸으면 힌트 반복 재생을 멈출지 결정
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
    /// 힌트 라이트 전체 ON/OFF
    /// </summary>
    private void TurnAllLights(bool isOn)
    {
        for (int i = 0; i < hintLights.Count; i++)
        {
            if (hintLights[i] == null)
                continue;

            hintLights[i].enabled = isOn;
        }
    }

    /// <summary>
    /// 디버그 로그 출력
    /// </summary>
    private void Log(string message)
    {
        if (!enableDebugLog)
            return;

        Debug.Log($"[LightPatternHint] {message}", this);
    }
}