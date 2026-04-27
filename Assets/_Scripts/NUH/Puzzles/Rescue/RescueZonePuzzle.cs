using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>
/// 구제구역 퍼즐 본체
/// Zone당 1개만 존재하며,
/// 문 2개 옆의 키패드 2개가 모두 이 퍼즐을 공유한다.
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
        if (Object.HasStateAuthority)
        {
            if (NetPuzzleVersion == 0)
                GenerateNewPuzzle(); // 첫 스폰 시 퍼즐 1회 생성
        }

        RefreshAllVisuals(); // 현재 상태 화면 반영
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
            PlayFailFlashOnAllViews(); // 오답 연출 재생
            needRefresh = true;
        }

        if (needRefresh)
            RefreshAllVisuals();
    }

    /// <summary>
    /// 숫자 1개 입력
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
    /// Confirm 입력
    /// 4자리가 모두 입력되었을 때만 정답 판정
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
    /// 입력만 초기화
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
    /// 새 퍼즐 생성 + 힌트 갱신 + 입력 초기화
    /// StageManager가 문 닫을 때 호출
    /// </summary>
    public void RegeneratePuzzle()
    {
        if (!Object.HasStateAuthority)
            return;

        GenerateNewPuzzle();
    }

    /// <summary>
    /// 새 정답 생성
    /// </summary>
    private void GenerateNewPuzzle()
    {
        int seed = BuildPuzzleSeed(NetPuzzleVersion + 1); // 다음 버전 기준 시드 생성
        RescueZoneAnswerGenerator.RescueZoneAnswerData data = RescueZoneAnswerGenerator.Generate(seed);

        ApplyGeneratedPuzzle(data);
    }

    /// <summary>
    /// 생성된 정답 데이터 반영
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
    /// 현재 입력 정답 판정
    /// </summary>
    private void EvaluateInput()
    {
        if (IsCorrectInput())
            HandleSolved();
        else
            HandleFailed();
    }

    /// <summary>
    /// 현재 입력이 정답과 일치하는지 검사
    /// </summary>
    private bool IsCorrectInput()
    {
        return NetInput0 == NetAnswer0 &&
               NetInput1 == NetAnswer1 &&
               NetInput2 == NetAnswer2 &&
               NetInput3 == NetAnswer3;
    }

    /// <summary>
    /// 성공 처리
    /// </summary>
    private void HandleSolved()
    {
        if (NetSolved)
            return;

        NetSolved = true;
        NetFailEffectPlaying = false;

        RefreshAllVisuals();

        //if (stageManager != null)
        //    stageManager.ReportRescuePuzzleSolved(puzzleZone); // StageManager에 성공 보고

        Log($"구제구역 퍼즐 성공 | Zone={puzzleZone}");
    }

    /// <summary>
    /// 실패 처리
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

        Log($"구제구역 퍼즐 실패 | Zone={puzzleZone}");
    }

    /// <summary>
    /// 힌트 표시 갱신
    /// </summary>
    private void RefreshHintDisplay()
    {
        if (hintDisplay == null)
            return;

        hintDisplay.SetHintDigits(NetAnswer0, NetAnswer1, NetAnswer2, NetAnswer3);
    }

    /// <summary>
    /// 키패드 화면 2개 전부 갱신
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
    /// 현재 입력 배열 반환
    /// </summary>
    private int[] GetCurrentInputDigits()
    {
        return new int[]
        {
            NetInput0,
            NetInput1,
            NetInput2,
            NetInput3
        };
    }

    /// <summary>
    /// 키패드 화면 전체에 실패 연출 재생
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
    /// 퍼즐 버전 기반 시드 생성
    /// </summary>
    private int BuildPuzzleSeed(int version)
    {
        int seed = 17;
        seed = seed * 31 + (int)puzzleZone;
        seed = seed * 31 + version;

        if (seed == 0)
            seed = 1;

        return seed;
    }

    /// <summary>
    /// 숫자 입력을 받을 수 있는지 반환한다.
    /// </summary>
    public bool CanAcceptDigitInput()
    {
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

        GenerateNewPuzzle(); // 새 정답/힌트 생성
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

        RegeneratePuzzle(); // 재생성 함수 호출
        Log("디버그 | 구제구역 퍼즐 재생성");
    }

    #endregion
}