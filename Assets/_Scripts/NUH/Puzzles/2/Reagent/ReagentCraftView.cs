using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 2-3 시약 제조 퍼즐의 제조 모니터 화면 표시 담당.
/// 
/// 역할
/// - 상단 슬롯 배경 3칸 표시
/// - 슬롯 위의 시약 스프라이트 3칸 표시
/// - 현재 편집 슬롯의 좌/우/선택 버튼 표시
/// - 제조 시작 버튼 표시
/// - 카운트다운 TMP 표시
/// - 가열/냉각 버튼 표시
/// - 프로그레스 바 10칸 SpriteRenderer 색상 표시
/// - 프로그레스 칸별 가열/냉각 액션 아이콘 표시
/// - 기본 화면 / 성공 화면 / 3단계 힌트 화면 전환
/// - Stage3HintRoot에 3단계 힌트를 표시한다.
/// </summary>
public class ReagentCraftView : MonoBehaviour
{
    [Header("슬롯 배경 SpriteRenderer")]
    [SerializeField] private List<SpriteRenderer> slotBackgroundRenderers = new(); // 상단 슬롯 배경 3칸 SpriteRenderer 목록

    [Header("시약 슬롯 SpriteRenderer")]
    [SerializeField] private List<SpriteRenderer> reagentSlotRenderers = new(); // 슬롯 위에 올라가는 시약 SpriteRenderer 3칸 목록

    [Header("현재 편집 슬롯 좌/우 버튼 오브젝트")]
    [SerializeField] private List<GameObject> leftArrowIndicators = new(); // 각 슬롯 왼쪽 화살표/버튼 오브젝트 목록
    [SerializeField] private List<GameObject> rightArrowIndicators = new(); // 각 슬롯 오른쪽 화살표/버튼 오브젝트 목록

    [Header("현재 편집 슬롯 선택 버튼 오브젝트")]
    [SerializeField] private List<GameObject> slotConfirmButtons = new(); // 각 슬롯 아래 선택 버튼 오브젝트 목록

    [Header("제조 시작 버튼")]
    [SerializeField] private GameObject startCraftButtonRoot; // 제조 시작 버튼 오브젝트

    [Header("카운트다운 TMP")]
    [SerializeField] private GameObject countdownRoot; // 카운트다운 표시 루트
    [SerializeField] private TextMeshPro countdownText; // 3,2,1 표시 TMP

    [Header("가열/냉각 버튼")]
    [SerializeField] private GameObject heatButtonRoot; // 가열 버튼 오브젝트
    [SerializeField] private GameObject coolButtonRoot; // 냉각 버튼 오브젝트

    [Header("슬롯 배경 설정")]
    [SerializeField] private Sprite slotBackgroundSprite; // 슬롯 사각형 배경 스프라이트
    [SerializeField] private Color slotBackgroundColor = Color.white; // 슬롯 배경 색상

    [Header("시약 스프라이트 매핑")]
    [SerializeField] private Sprite emptyReagentSprite; // 비어 있는 슬롯 표시용 스프라이트
    [SerializeField] private Sprite reagentASprite; // 시약 A 스프라이트
    [SerializeField] private Sprite reagentBSprite; // 시약 B 스프라이트
    [SerializeField] private Sprite reagentCSprite; // 시약 C 스프라이트
    [SerializeField] private Sprite reagentDSprite; // 시약 D 스프라이트
    [SerializeField] private Sprite reagentESprite; // 시약 E 스프라이트
    [SerializeField] private Sprite reagentFSprite; // 시약 F 스프라이트

    [Header("프로그레스 바 SpriteRenderer")]
    [SerializeField] private List<SpriteRenderer> progressBarRenderers = new(); // 프로그레스 바 10칸 SpriteRenderer 목록

    [Header("프로그레스 액션 아이콘 SpriteRenderer")]
    [SerializeField] private List<SpriteRenderer> progressActionIconRenderers = new(); // 프로그레스 칸 위 액션 아이콘 10칸

    [Header("프로그레스 액션 스프라이트")]
    [SerializeField] private Sprite heatActionSprite; // 가열 표시 스프라이트
    [SerializeField] private Sprite coolActionSprite; // 냉각 표시 스프라이트

    [Header("프로그레스 색상")]
    [SerializeField] private Color progressInactiveColor = new Color(0.12f, 0.25f, 0.12f); // 비활성 어두운 녹색
    [SerializeField] private Color progressActiveColor = new Color(0.65f, 1f, 0.45f); // 활성 밝은 연두색
    [SerializeField] private Color progressHeatColor = Color.red; // 가열 입력 칸 색상
    [SerializeField] private Color progressCoolColor = Color.blue; // 냉각 입력 칸 색상

    [Header("화면 상태 루트")]
    [SerializeField] private GameObject defaultRoot; // 기본 제조 화면 루트
    [SerializeField] private GameObject solvedRoot; // 성공 화면 루트
    [SerializeField] private GameObject stage3HintRoot; // 3단계 힌트 화면 루트

    [Header("3단계 힌트 표시기")]
    [SerializeField] private FinalCodeHintDisplay stage3HintDisplay; // Stage3HintRoot 내부 최종 힌트 표시기

    [Header("디버그")]
    [SerializeField] private bool enableDebugLog = true; // 디버그 로그 출력 여부

    /// <summary>
    /// 슬롯 배경 3칸에 기본 스프라이트와 색상을 적용한다.
    /// </summary>
    public void ApplySlotBackgrounds()
    {
        for (int i = 0; i < slotBackgroundRenderers.Count; i++)
        {
            SpriteRenderer slotRenderer = slotBackgroundRenderers[i];
            if (slotRenderer == null)
                continue;

            slotRenderer.sprite = slotBackgroundSprite;
            slotRenderer.color = slotBackgroundColor;
        }
    }

    /// <summary>
    /// 슬롯 3칸의 현재 시약 상태를 화면에 반영한다.
    /// </summary>
    public void ApplyRecipeSlotStates(ReagentType[] slotValues)
    {
        if (slotValues == null)
            return;

        int count = Mathf.Min(reagentSlotRenderers.Count, slotValues.Length);

        for (int i = 0; i < count; i++)
            SetSlotSprite(i, slotValues[i]);
    }

    /// <summary>
    /// 현재 편집 중인 슬롯에만 좌/우/선택 버튼을 표시한다.
    /// </summary>
    public void SetEditingSlotControls(int slotIndex)
    {
        ClearEditingSlotControls();

        if (slotIndex < 0)
            return;

        if (slotIndex < leftArrowIndicators.Count && leftArrowIndicators[slotIndex] != null)
            leftArrowIndicators[slotIndex].SetActive(true);

        if (slotIndex < rightArrowIndicators.Count && rightArrowIndicators[slotIndex] != null)
            rightArrowIndicators[slotIndex].SetActive(true);

        if (slotIndex < slotConfirmButtons.Count && slotConfirmButtons[slotIndex] != null)
            slotConfirmButtons[slotIndex].SetActive(true);
    }

    /// <summary>
    /// 기존 함수명 호환용.
    /// 현재 편집 슬롯의 좌/우/선택 버튼을 표시한다.
    /// </summary>
    public void SetEditingSlotIndicator(int slotIndex)
    {
        SetEditingSlotControls(slotIndex);
    }

    /// <summary>
    /// 모든 슬롯 좌/우/선택 버튼을 숨긴다.
    /// </summary>
    public void ClearEditingSlotControls()
    {
        for (int i = 0; i < leftArrowIndicators.Count; i++)
        {
            if (leftArrowIndicators[i] != null)
                leftArrowIndicators[i].SetActive(false);
        }

        for (int i = 0; i < rightArrowIndicators.Count; i++)
        {
            if (rightArrowIndicators[i] != null)
                rightArrowIndicators[i].SetActive(false);
        }

        for (int i = 0; i < slotConfirmButtons.Count; i++)
        {
            if (slotConfirmButtons[i] != null)
                slotConfirmButtons[i].SetActive(false);
        }
    }

    /// <summary>
    /// 기존 함수명 호환용.
    /// 모든 슬롯 조작 버튼을 숨긴다.
    /// </summary>
    public void ClearEditingSlotIndicator()
    {
        ClearEditingSlotControls();
    }

    /// <summary>
    /// 제조 시작 버튼 표시 여부를 설정한다.
    /// </summary>
    public void SetStartCraftButtonVisible(bool visible)
    {
        if (startCraftButtonRoot != null)
            startCraftButtonRoot.SetActive(visible);
    }

    /// <summary>
    /// 카운트다운 숫자를 표시한다.
    /// number가 0 이하이면 비활성화한다.
    /// </summary>
    public void SetCountdownNumber(int number)
    {
        bool visible = number > 0;

        if (countdownRoot != null)
            countdownRoot.SetActive(visible);

        if (countdownText != null)
            countdownText.text = visible ? number.ToString() : string.Empty;
    }

    /// <summary>
    /// 가열/냉각 버튼 표시 여부를 설정한다.
    /// </summary>
    public void SetActionButtonsVisible(bool visible)
    {
        if (heatButtonRoot != null)
            heatButtonRoot.SetActive(visible);

        if (coolButtonRoot != null)
            coolButtonRoot.SetActive(visible);
    }

    /// <summary>
    /// 특정 슬롯의 시약 스프라이트를 갱신한다.
    /// </summary>
    public void SetSlotSprite(int slotIndex, ReagentType reagentType)
    {
        if (slotIndex < 0 || slotIndex >= reagentSlotRenderers.Count)
            return;

        SpriteRenderer slotRenderer = reagentSlotRenderers[slotIndex];
        if (slotRenderer == null)
            return;

        slotRenderer.sprite = GetReagentSprite(reagentType);
        slotRenderer.color = Color.white;
    }

    /// <summary>
    /// 프로그레스 바 10칸을 전부 비활성 색으로 초기화한다.
    /// 액션 아이콘도 전부 숨긴다.
    /// </summary>
    public void ResetProgressBar()
    {
        for (int i = 0; i < progressBarRenderers.Count; i++)
        {
            SpriteRenderer progressRenderer = progressBarRenderers[i];
            if (progressRenderer == null)
                continue;

            progressRenderer.color = progressInactiveColor;
        }

        ClearProgressActionIcons();
    }

    /// <summary>
    /// 0부터 progressIndex까지의 프로그레스 칸을 활성 색으로 바꾼다.
    /// 기존 코드 호환용이다.
    /// </summary>
    public void SetProgressActiveUpTo(int progressIndex)
    {
        ResetProgressBar();

        for (int i = 0; i <= progressIndex && i < progressBarRenderers.Count; i++)
        {
            SpriteRenderer progressRenderer = progressBarRenderers[i];
            if (progressRenderer == null)
                continue;

            progressRenderer.color = progressActiveColor;
        }
    }

    /// <summary>
    /// 현재 진행도와 각 칸의 액션 상태를 함께 반영한다.
    /// actionStates: 0=None, 1=Heat, 2=Cool.
    /// currentProgressIndex는 1~10 기준이며, 0이면 미진행이다.
    /// </summary>
    public void ApplyProgressPresentation(int currentProgressIndex, IReadOnlyList<int> actionStates)
    {
        int activeLastIndex = currentProgressIndex - 1;

        for (int i = 0; i < progressBarRenderers.Count; i++)
        {
            SpriteRenderer progressRenderer = progressBarRenderers[i];
            if (progressRenderer == null)
                continue;

            int actionState = GetActionState(actionStates, i);

            if (actionState == 1)
            {
                progressRenderer.color = progressHeatColor;
                continue;
            }

            if (actionState == 2)
            {
                progressRenderer.color = progressCoolColor;
                continue;
            }

            progressRenderer.color = i <= activeLastIndex
                ? progressActiveColor
                : progressInactiveColor;
        }

        ApplyProgressActionIcons(actionStates);
    }

    /// <summary>
    /// 프로그레스 액션 아이콘 전체를 actionStates 기준으로 반영한다.
    /// </summary>
    private void ApplyProgressActionIcons(IReadOnlyList<int> actionStates)
    {
        for (int i = 0; i < progressActionIconRenderers.Count; i++)
        {
            SpriteRenderer iconRenderer = progressActionIconRenderers[i];
            if (iconRenderer == null)
                continue;

            int actionState = GetActionState(actionStates, i);

            if (actionState == 1)
            {
                iconRenderer.gameObject.SetActive(true);
                iconRenderer.sprite = heatActionSprite;
                iconRenderer.color = Color.white;
                continue;
            }

            if (actionState == 2)
            {
                iconRenderer.gameObject.SetActive(true);
                iconRenderer.sprite = coolActionSprite;
                iconRenderer.color = Color.white;
                continue;
            }

            iconRenderer.sprite = null;
            iconRenderer.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 모든 프로그레스 액션 아이콘을 숨긴다.
    /// </summary>
    private void ClearProgressActionIcons()
    {
        for (int i = 0; i < progressActionIconRenderers.Count; i++)
        {
            if (progressActionIconRenderers[i] == null)
                continue;

            progressActionIconRenderers[i].sprite = null;
            progressActionIconRenderers[i].gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// actionStates에서 특정 인덱스의 액션 상태를 안전하게 가져온다.
    /// </summary>
    private int GetActionState(IReadOnlyList<int> actionStates, int index)
    {
        if (actionStates == null)
            return 0;

        if (index < 0 || index >= actionStates.Count)
            return 0;

        return actionStates[index];
    }

    /// <summary>
    /// 3단계 힌트 데이터를 표시기에 반영한다.
    /// </summary>
    public void ApplyStage3Hint(FinalCodeHintData hintData)
    {
        if (stage3HintDisplay == null)
            return;

        stage3HintDisplay.ApplyHint(hintData);
    }

    /// <summary>
    /// 성공 상태 표시.
    /// </summary>
    public void ShowSolvedState()
    {
        if (defaultRoot != null)
            defaultRoot.SetActive(false);

        if (solvedRoot != null)
            solvedRoot.SetActive(true);

        if (stage3HintRoot != null)
            stage3HintRoot.SetActive(false);

        SetStartCraftButtonVisible(false);
        SetCountdownNumber(0);
        SetActionButtonsVisible(false);
        ClearEditingSlotControls();

        Log("성공 상태 표시");
    }

    /// <summary>
    /// 성공 후 3단계 힌트 표시 상태로 전환.
    /// </summary>
    public void ShowStage3HintState()
    {
        if (defaultRoot != null)
            defaultRoot.SetActive(false);

        if (solvedRoot != null)
            solvedRoot.SetActive(false);

        if (stage3HintRoot != null)
            stage3HintRoot.SetActive(true);

        SetStartCraftButtonVisible(false);
        SetCountdownNumber(0);
        SetActionButtonsVisible(false);
        ClearEditingSlotControls();

        Log("3단계 힌트 표시 상태로 전환");
    }

    /// <summary>
    /// 기본 화면 루트 상태로 초기화한다.
    /// </summary>
    public void ResetToDefault()
    {
        ApplySlotBackgrounds();
        ClearEditingSlotControls();
        SetStartCraftButtonVisible(false);
        SetCountdownNumber(0);
        SetActionButtonsVisible(false);
        ResetProgressBar();

        if (defaultRoot != null)
            defaultRoot.SetActive(true);

        if (solvedRoot != null)
            solvedRoot.SetActive(false);

        if (stage3HintRoot != null)
            stage3HintRoot.SetActive(false);

        if (stage3HintDisplay != null)
            stage3HintDisplay.ResetDisplay();

        for (int i = 0; i < reagentSlotRenderers.Count; i++)
            SetSlotSprite(i, ReagentType.None);

        Log("기본 상태로 초기화");
    }

    /// <summary>
    /// 시약 타입에 맞는 스프라이트를 반환한다.
    /// </summary>
    private Sprite GetReagentSprite(ReagentType reagentType)
    {
        return reagentType switch
        {
            ReagentType.ReagentA => reagentASprite,
            ReagentType.ReagentB => reagentBSprite,
            ReagentType.ReagentC => reagentCSprite,
            ReagentType.ReagentD => reagentDSprite,
            ReagentType.ReagentE => reagentESprite,
            ReagentType.ReagentF => reagentFSprite,
            _ => emptyReagentSprite
        };
    }

    /// <summary>
    /// 일반 디버그 로그 출력.
    /// </summary>
    private void Log(string message)
    {
        if (!enableDebugLog)
            return;

        Debug.Log($"[ReagentCraftView] {message}", this);
    }
}