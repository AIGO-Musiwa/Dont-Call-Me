using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 2-1 숫자 입력 퍼즐의 모니터 상단 힌트 표시 담당.
/// 
/// 역할
/// - 같은 answer seed를 받아 정답 데이터를 재구성한다.
/// - 힌트 순서 4개를 sprite로 표시한다.
/// - 책 힌트는 공통 흰색 책 sprite 1장을 사용하고 color만 바꾼다.
/// </summary>
public class NumericCodeHintMonitor : MonoBehaviour, IPuzzleSeedReceiver
{
    [Header("표시 슬롯")]
    [SerializeField] private List<SpriteRenderer> hintSlots = new(); // 상단 힌트 4칸

    [Header("기본 힌트 Sprite")]
    [SerializeField] private Sprite clockHintSprite;   // 시계 힌트 sprite
    [SerializeField] private Sprite drawerHintSprite;  // 서랍 힌트 sprite
    [SerializeField] private Sprite frameHintSprite;   // 액자 힌트 sprite

    [Header("책 힌트 Sprite")]
    [SerializeField] private Sprite bookHintBaseSprite; // 흰색 책 기본 sprite

    [Header("책 힌트 색상")]
    [SerializeField] private Color redBookColor = Color.red;
    [SerializeField] private Color greenBookColor = Color.green;
    [SerializeField] private Color blueBookColor = Color.blue;
    [SerializeField] private Color yellowBookColor = Color.yellow;

    [Header("기본 색")]
    [SerializeField] private Color defaultIconColor = Color.white; // 시계/서랍/액자 기본 색

    [Header("디버그")]
    [SerializeField] private bool enableDebugLog = true;

    private NumericCodeAnswerGenerator.NumericCodeAnswerData _answerData;
    private bool _hasAnswerSeed;

    /// <summary>
    /// 같은 answer seed를 받아 힌트 순서를 재구성하고 sprite를 반영한다.
    /// </summary>
    public void ApplyAnswerSeed(int seed)
    {
        _answerData = NumericCodeAnswerGenerator.Generate(seed);
        _hasAnswerSeed = true;

        ApplyHintVisuals();

        Log($"모니터 힌트 시드 적용 완료 | seed = {seed}");
        LogHintOrderDebug();
    }

    /// <summary>
    /// 현재 정답 데이터 기준으로 상단 힌트 sprite를 슬롯에 배치한다.
    /// </summary>
    private void ApplyHintVisuals()
    {
        if (!_hasAnswerSeed || _answerData == null)
            return;

        int count = Mathf.Min(hintSlots.Count, _answerData.HintOrder.Count);

        for (int i = 0; i < hintSlots.Count; i++)
        {
            SpriteRenderer slot = hintSlots[i];
            if (slot == null)
                continue;

            if (i >= count)
            {
                slot.sprite = null;
                slot.enabled = false;
                slot.color = defaultIconColor;
                continue;
            }

            NumericHintType hintType = _answerData.HintOrder[i];
            slot.sprite = GetHintSprite(hintType);
            slot.color = GetHintColor(hintType, _answerData.TargetBookColor);
            slot.enabled = slot.sprite != null;
        }
    }

    /// <summary>
    /// 힌트 종류에 맞는 sprite를 반환한다.
    /// 책 힌트는 공통 흰색 책 sprite를 사용한다.
    /// </summary>
    private Sprite GetHintSprite(NumericHintType hintType)
    {
        return hintType switch
        {
            NumericHintType.Clock => clockHintSprite,
            NumericHintType.Drawer => drawerHintSprite,
            NumericHintType.Frame => frameHintSprite,
            NumericHintType.Book => bookHintBaseSprite,
            _ => null
        };
    }

    /// <summary>
    /// 힌트 종류에 맞는 색을 반환한다.
    /// 책 힌트만 targetBookColor에 따라 색이 달라진다.
    /// </summary>
    private Color GetHintColor(NumericHintType hintType, NumericBookColor targetBookColor)
    {
        if (hintType != NumericHintType.Book)
            return defaultIconColor;

        return targetBookColor switch
        {
            NumericBookColor.Red => redBookColor,
            NumericBookColor.Green => greenBookColor,
            NumericBookColor.Blue => blueBookColor,
            NumericBookColor.Yellow => yellowBookColor,
            _ => defaultIconColor
        };
    }

    /// <summary>
    /// 현재 힌트 표시를 모두 비운다.
    /// </summary>
    public void ResetToDefault()
    {
        for (int i = 0; i < hintSlots.Count; i++)
        {
            if (hintSlots[i] == null)
                continue;

            hintSlots[i].sprite = null;
            hintSlots[i].enabled = false;
            hintSlots[i].color = defaultIconColor;
        }

        Log("모니터 힌트 기본 상태로 초기화");
    }

    /// <summary>
    /// 현재 재구성된 정답 데이터를 외부에서 읽을 수 있게 반환한다.
    /// </summary>
    public NumericCodeAnswerGenerator.NumericCodeAnswerData GetAnswerData()
    {
        return _answerData;
    }

    /// <summary>
    /// 디버그용 힌트 순서 로그 출력.
    /// </summary>
    private void LogHintOrderDebug()
    {
        if (!enableDebugLog || _answerData == null)
            return;

        string hintOrder = string.Join(", ", _answerData.HintOrder);
        Debug.Log(
            $"[NumericCodeHintMonitor] HintOrder=[{hintOrder}] | TargetBookColor={_answerData.TargetBookColor}",
            this);
    }

    /// <summary>
    /// 일반 디버그 로그 출력.
    /// </summary>
    private void Log(string message)
    {
        if (!enableDebugLog)
            return;

        Debug.Log($"[NumericCodeHintMonitor] {message}", this);
    }
}