using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>
/// 구제구역 퍼즐 본체.
/// Zone당 1개만 존재하며,
/// 문 2개 옆의 키패드 2개가 모두 이 퍼즐을 공유한다.
/// 
/// 정답 생성 규칙:
/// - 이 퍼즐은 맵에 고정 배치되어 있으므로 PuzzleSpawnManager / PuzzleSeedSync를 사용하지 않는다.
/// - StageManager가 라운드 seed 기반으로 만든 base seed를 ServerApplyBaseAnswerSeed()로 직접 주입한다.
/// - 퍼즐은 받은 base seed와 NetPuzzleVersion을 조합해 최종 정답 seed를 만든다.
/// - 같은 라운드 안에서 RegeneratePuzzle()이 호출되면 version이 증가하므로 새 정답이 나온다.
/// </summary>
public class RescueZonePuzzle : NetworkBehaviour
{
    [Header("기본 정보")]
    [SerializeField] private Zone puzzleZone;                       // 이 퍼즐이 속한 Zone
    [SerializeField] private StageManager stageManager;             // 성공 보고 대상

    [Header("표시 참조")]
    [SerializeField] private RescueZoneHintDisplay hintDisplay;     // 내부 힌트 표시
    [SerializeField] private List<RescueZoneKeypadView> keypadViews = new(); // 키패드 화면 2개

    [Header("설정")]
    [SerializeField] private int digitCount = 4;                    // 정답 자릿수
    [SerializeField] private bool lockInputDuringFailEffect = true; // 오답 연출 중 입력 잠금 여부

    [Header("디버그")]
    [SerializeField] private bool enableDebugLog = false;           // 디버그 로그 출력 여부

    [Networked] private int NetBaseAnswerSeed { get; set; }         // StageManager가 주입한 라운드/Zone 기반 base seed
    [Networked] private NetworkBool NetHasBaseAnswerSeed { get; set; } // base seed 적용 여부

    [Networked] private int NetAnswer0 { get; set; }                // 첫 번째 정답 숫자
    [Networked] private int NetAnswer1 { get; set; }                // 두 번째 정답 숫자
    [Networked] private int NetAnswer2 { get; set; }                // 세 번째 정답 숫자
    [Networked] private int NetAnswer3 { get; set; }                // 네 번째 정답 숫자

    [Networked] private int NetInput0 { get; set; }                 // 첫 번째 입력 숫자
    [Networked] private int NetInput1 { get; set; }                 // 두 번째 입력 숫자
    [Networked] private int NetInput2 { get; set; }                 // 세 번째 입력 숫자
    [Networked] private int NetInput3 { get; set; }                 // 네 번째 입력 숫자

    [Networked] private int NetInputCount { get; set; }             // 현재 입력 개수
    [Networked] private NetworkBool NetSolved { get; set; }         // 퍼즐 성공 여부
    [Networked] private int NetFailFlashSerial { get; set; }        // 오답 연출 트리거값
    [Networked] private int NetPuzzleVersion { get; set; }          // 재생성 버전
    [Networked] private NetworkBool NetFailEffectPlaying { get; set; } // 실패 연출 중 여부

    private int _cachedFailFlashSerial = -1;                        // 로컬 실패 연출 감지용 캐시
    private int _cachedPuzzleVersion = -1;                          // 로컬 퍼즐 버전 감지용 캐시
    private int _cachedInputCount = -1;                             // 로컬 입력 개수 감지용 캐시
    private bool _cachedSolved;                                     // 로컬 solved 감지용 캐시

    public Zone PuzzleZone => puzzleZone;                           // 외부 읽기용 Zone

    public override void Spawned()
    {
        RefreshAllVisuals();
    }

    public override void Render()
    {
        bool needRefresh = false;

        if (_cachedPuzzleVersion != NetPuzzleVersion)
        {
            _cachedPuzzleVersion = NetPuzzleVersion;
            needRefresh = true;
        }

        if (_cachedInputCount != NetInputCount)
        {
            _cachedInputCount = NetInputCount;
            needRefresh = true;
        }

        if (_cachedSolved != NetSolved)
        {
            _cachedSolved = NetSolved;
            needRefresh = true;
        }

        if (_cachedFailFlashSerial != NetFailFlashSerial)
        {
            _cachedFailFlashSerial = NetFailFlashSerial;
            PlayFailFlashOnAllViews();
            needRefresh = true;
        }

        if (needRefresh)
            RefreshAllVisuals();
    }

    /// <summary>
    /// StageManager가 라운드 seed 기반으로 만든 구제구역 base seed를 주입한다.
    /// 서버 권한에서만 호출되어야 하며, 호출되면 첫 정답을 생성한다.
    /// </summary>
    public void ServerApplyBaseAnswerSeed(int seed)
    {
        if (!Object.HasStateAuthority)
            return;

        if (seed == 0)
            seed = 1;

        NetBaseAnswerSeed = seed;
        NetHasBaseAnswerSeed = true;
        NetPuzzleVersion = 0;

        GenerateNewPuzzle();

        Log($"구제구역 base seed 적용 | Zone={puzzleZone} | BaseSeed={NetBaseAnswerSeed}");
    }

    /// <summary>
    /// 숫자 1개 입력.
    /// </summary>
    public void SubmitDigit(int digit)
    {
        if (!Object.HasStateAuthority)
            return;

        if (NetSolved)
            return;

        if (lockInputDuringFailEffect && NetFailEffectPlaying)
            return;

        if (digit < 0 || digit > 9)
            return;

        if (NetInputCount >= digitCount)
            return;

        switch (NetInputCount)
        {
            case 0:
                NetInput0 = digit;
                break;
            case 1:
                NetInput1 = digit;
                break;
            case 2:
                NetInput2 = digit;
                break;
            case 3:
                NetInput3 = digit;
                break;
        }

        NetInputCount++;
    }

    /// <summary>
    /// Confirm 입력.
    /// 4자리가 모두 입력되었을 때만 정답 판정한다.
    /// </summary>
    public void ConfirmInput()
    {
        if (!Object.HasStateAuthority)
            return;

        if (NetSolved)
            return;

        if (lockInputDuringFailEffect && NetFailEffectPlaying)
            return;

        if (NetInputCount < digitCount)
        {
            Log("입력 숫자가 4자리가 아니므로 Confirm 무시");
            return;
        }

        EvaluateInput();
    }

    /// <summary>
    /// 입력만 초기화한다.
    /// </summary>
    public void ResetInputOnly()
    {
        if (!Object.HasStateAuthority)
            return;

        NetInput0 = 0;
        NetInput1 = 0;
        NetInput2 = 0;
        NetInput3 = 0;
        NetInputCount = 0;
        NetFailEffectPlaying = false;
    }

    /// <summary>
    /// 새 퍼즐 생성 + 힌트 갱신 + 입력 초기화.
    /// StageManager 또는 문 닫힘 로직이 호출한다.
    /// </summary>
    public void RegeneratePuzzle()
    {
        if (!Object.HasStateAuthority)
            return;

        if (!NetHasBaseAnswerSeed)
        {
            LogWarning($"base seed가 없어 구제구역 퍼즐을 재생성할 수 없습니다. Zone={puzzleZone}");
            return;
        }

        GenerateNewPuzzle();
    }

    /// <summary>
    /// base seed와 다음 version을 조합해 새 정답을 생성한다.
    /// </summary>
    private void GenerateNewPuzzle()
    {
        if (!Object.HasStateAuthority)
            return;

        if (!NetHasBaseAnswerSeed)
        {
            LogWarning($"base seed가 적용되지 않아 구제구역 퍼즐을 생성하지 않습니다. Zone={puzzleZone}");
            return;
        }

        int nextVersion = NetPuzzleVersion + 1;
        int seed = BuildPuzzleSeed(NetBaseAnswerSeed, nextVersion);

        RescueZoneAnswerGenerator.RescueZoneAnswerData data = RescueZoneAnswerGenerator.Generate(seed);

        ApplyGeneratedPuzzle(data);

        Log($"구제구역 퍼즐 seed 생성 | Zone={puzzleZone} | BaseSeed={NetBaseAnswerSeed} | Version={nextVersion} | FinalSeed={seed}");
    }

    /// <summary>
    /// 생성된 정답 데이터를 Networked 상태에 반영한다.
    /// </summary>
    private void ApplyGeneratedPuzzle(RescueZoneAnswerGenerator.RescueZoneAnswerData data)
    {
        NetAnswer0 = data.digit0;
        NetAnswer1 = data.digit1;
        NetAnswer2 = data.digit2;
        NetAnswer3 = data.digit3;

        NetInput0 = 0;
        NetInput1 = 0;
        NetInput2 = 0;
        NetInput3 = 0;
        NetInputCount = 0;
        NetSolved = false;
        NetFailEffectPlaying = false;
        NetPuzzleVersion++;

        RefreshHintDisplay();
        RefreshAllVisuals();

        Log($"새 구제구역 퍼즐 생성 | Zone={puzzleZone} | Answer={NetAnswer0}{NetAnswer1}{NetAnswer2}{NetAnswer3} | Version={NetPuzzleVersion}");
    }

    /// <summary>
    /// 현재 입력 정답 판정.
    /// </summary>
    private void EvaluateInput()
    {
        if (IsCorrectInput())
            HandleSolved();
        else
            HandleFailed();
    }

    /// <summary>
    /// 현재 입력이 정답과 일치하는지 검사한다.
    /// </summary>
    private bool IsCorrectInput()
    {
        return NetInput0 == NetAnswer0 &&
               NetInput1 == NetAnswer1 &&
               NetInput2 == NetAnswer2 &&
               NetInput3 == NetAnswer3;
    }

    /// <summary>
    /// 성공 처리.
    /// </summary>
    private void HandleSolved()
    {
        if (NetSolved)
            return;

        NetSolved = true;
        NetFailEffectPlaying = false;

        RefreshAllVisuals();

        RescueZoneDoor.OpenAllDoorsInZone(puzzleZone);

        Log($"구제구역 퍼즐 성공 | Zone={puzzleZone}");
    }

    /// <summary>
    /// 실패 처리.
    /// </summary>
    private void HandleFailed()
    {
        NetFailEffectPlaying = true;
        NetFailFlashSerial++;

        NetInput0 = 0;
        NetInput1 = 0;
        NetInput2 = 0;
        NetInput3 = 0;
        NetInputCount = 0;

        NetFailEffectPlaying = false;

        RescueZoneDoor.EmitFailNoise(puzzleZone, transform.position);

        Log($"구제구역 퍼즐 실패 | Zone={puzzleZone}");
    }

    /// <summary>
    /// 힌트 표시 갱신.
    /// </summary>
    private void RefreshHintDisplay()
    {
        if (hintDisplay == null)
            return;

        hintDisplay.SetHintDigits(NetAnswer0, NetAnswer1, NetAnswer2, NetAnswer3);
    }

    /// <summary>
    /// 키패드 화면 2개 전부 갱신.
    /// </summary>
    private void RefreshAllVisuals()
    {
        int[] inputDigits = GetCurrentInputDigits();

        for (int i = 0; i < keypadViews.Count; i++)
        {
            RescueZoneKeypadView view = keypadViews[i];
            if (view == null)
                continue;

            if (NetSolved)
            {
                view.ShowSolved();
                view.RefreshInput(inputDigits, NetInputCount);
            }
            else
            {
                view.ShowDefault();
                view.RefreshInput(inputDigits, NetInputCount);
            }
        }

        RefreshHintDisplay();
    }

    /// <summary>
    /// 현재 입력 배열 반환.
    /// </summary>
    private int[] GetCurrentInputDigits()
    {
        return new[]
        {
            NetInput0,
            NetInput1,
            NetInput2,
            NetInput3
        };
    }

    /// <summary>
    /// 키패드 화면 전체에 실패 연출 재생.
    /// </summary>
    private void PlayFailFlashOnAllViews()
    {
        for (int i = 0; i < keypadViews.Count; i++)
        {
            RescueZoneKeypadView view = keypadViews[i];
            if (view == null)
                continue;

            view.PlayFailFlash();
        }
    }

    /// <summary>
    /// StageManager가 준 base seed와 현재 version을 조합해 최종 정답 seed를 만든다.
    /// StageManager 쪽에서 이미 round seed와 Zone을 섞어 base seed를 만들기 때문에,
    /// 여기서는 같은 라운드 안의 재생성 구분용 version을 중심으로 섞는다.
    /// </summary>
    private int BuildPuzzleSeed(int baseSeed, int version)
    {
        unchecked
        {
            int seed = baseSeed;
            seed = seed * 31 + 4177; // 구제구역 재생성용 salt
            seed = seed * 31 + version;

            if (seed == 0)
                seed = 1;

            return seed;
        }
    }

    /// <summary>
    /// 숫자 입력을 받을 수 있는지 반환한다.
    /// </summary>
    public bool CanAcceptDigitInput()
    {
        if (!NetHasBaseAnswerSeed)
            return false;

        if (NetSolved)
            return false;

        if (lockInputDuringFailEffect && NetFailEffectPlaying)
            return false;

        if (NetInputCount >= digitCount)
            return false;

        return true;
    }

    /// <summary>
    /// Confirm 입력을 받을 수 있는지 반환한다.
    /// </summary>
    public bool CanAcceptConfirmInput()
    {
        if (!NetHasBaseAnswerSeed)
            return false;

        if (NetSolved)
            return false;

        if (lockInputDuringFailEffect && NetFailEffectPlaying)
            return false;

        if (NetInputCount < digitCount)
            return false;

        return true;
    }

    private void Log(string message)
    {
        if (!enableDebugLog)
            return;

        Debug.Log($"[RescueZonePuzzle] {message}", this);
    }

    private void LogWarning(string message)
    {
        if (!enableDebugLog)
            return;

        Debug.LogWarning($"[RescueZonePuzzle] {message}", this);
    }

    #region 디버그

    /// <summary>
    /// 플레이 모드에서 인스펙터 컨텍스트 메뉴로 새 퍼즐을 강제 생성한다.
    /// base seed가 적용된 상태에서만 동작한다.
    /// </summary>
    [ContextMenu("Debug/Generate Puzzle")]
    private void DebugGeneratePuzzle()
    {
        if (!Application.isPlaying)
        {
            LogWarning("플레이 모드에서만 실행 가능합니다.");
            return;
        }

        if (!Object.HasStateAuthority)
        {
            LogWarning("StateAuthority가 없는 객체는 디버그 퍼즐 생성을 실행할 수 없습니다.");
            return;
        }

        GenerateNewPuzzle();
        Log("디버그 | 새 구제구역 퍼즐 생성");
    }

    /// <summary>
    /// 플레이 모드에서 인스펙터 컨텍스트 메뉴로 퍼즐을 강제 재생성한다.
    /// 내부적으로 RegeneratePuzzle()을 호출한다.
    /// </summary>
    [ContextMenu("Debug/Regenerate Puzzle")]
    private void DebugRegeneratePuzzle()
    {
        if (!Application.isPlaying)
        {
            LogWarning("플레이 모드에서만 실행 가능합니다.");
            return;
        }

        if (!Object.HasStateAuthority)
        {
            LogWarning("StateAuthority가 없는 객체는 디버그 퍼즐 재생성을 실행할 수 없습니다.");
            return;
        }

        RegeneratePuzzle();
        Log("디버그 | 구제구역 퍼즐 재생성");
    }

    #endregion
}