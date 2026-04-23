using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 2-3 시약 제조 퍼즐의 제조 모니터 화면 표시 담당.
/// 
/// 역할
/// - 상단 슬롯 배경 3칸 표시
/// - 슬롯 위의 시약 스프라이트 3칸 표시
/// - 현재 편집 슬롯의 좌/우 화살표 오브젝트 표시
/// - 프로그레스 바 10칸 SpriteRenderer 색상 표시
/// - 기본 화면 / 성공 화면 / 3단계 힌트 화면 전환
/// </summary>
public class ReagentCraftView : MonoBehaviour
{
    [Header("슬롯 배경 SpriteRenderer")]
    [SerializeField] private List<SpriteRenderer> slotBackgroundRenderers = new(); // 상단 슬롯 배경 3칸 SpriteRenderer 목록

    [Header("시약 슬롯 SpriteRenderer")]
    [SerializeField] private List<SpriteRenderer> reagentSlotRenderers = new(); // 슬롯 위에 올라가는 시약 SpriteRenderer 3칸 목록

    [Header("현재 편집 슬롯 화살표 오브젝트")]
    [SerializeField] private List<GameObject> leftArrowIndicators = new(); // 각 슬롯 왼쪽 화살표 오브젝트 목록
    [SerializeField] private List<GameObject> rightArrowIndicators = new(); // 각 슬롯 오른쪽 화살표 오브젝트 목록

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

    [Header("프로그레스 색상")]
    [SerializeField] private Color progressInactiveColor = new Color(0.12f, 0.25f, 0.12f); // 비활성 어두운 녹색
    [SerializeField] private Color progressActiveColor = new Color(0.65f, 1f, 0.45f); // 활성 밝은 연두색

    [Header("화면 상태 루트")]
    [SerializeField] private GameObject defaultRoot; // 기본 제조 화면 루트
    [SerializeField] private GameObject solvedRoot; // 성공 화면 루트
    [SerializeField] private GameObject stage3HintRoot; // 3단계 힌트 화면 루트

    [Header("디버그")]
    [SerializeField] private bool enableDebugLog = true; // 디버그 로그 출력 여부

    /// <summary>
    /// 슬롯 배경 3칸에 기본 스프라이트와 색상을 적용한다.
    /// </summary>
    public void ApplySlotBackgrounds()
    {
        for (int i = 0; i < slotBackgroundRenderers.Count; i++)
        {
            SpriteRenderer slotRenderer = slotBackgroundRenderers[i]; // 현재 슬롯 배경 렌더러 참조
            if (slotRenderer == null)
                continue; // 참조 없으면 스킵

            slotRenderer.sprite = slotBackgroundSprite; // 슬롯 배경 스프라이트 적용
            slotRenderer.color = slotBackgroundColor; // 슬롯 배경 색상 적용
        }
    }

    /// <summary>
    /// 슬롯 3칸의 현재 시약 상태를 화면에 반영한다.
    /// 슬롯 배경은 유지하고, 그 위 시약 스프라이트만 갱신한다.
    /// </summary>
    public void ApplyRecipeSlotStates(ReagentType[] slotValues)
    {
        if (slotValues == null)
            return; // 슬롯 데이터가 없으면 종료

        int count = Mathf.Min(reagentSlotRenderers.Count, slotValues.Length); // 실제 반영 가능한 개수 계산

        for (int i = 0; i < count; i++)
            SetSlotSprite(i, slotValues[i]); // 각 슬롯 시약 스프라이트 갱신
    }

    /// <summary>
    /// 현재 편집 중인 슬롯에만 좌/우 화살표 오브젝트를 표시한다.
    /// </summary>
    public void SetEditingSlotIndicator(int slotIndex)
    {
        ClearEditingSlotIndicator(); // 기존 화살표 표시를 먼저 초기화

        if (slotIndex < 0)
            return; // 유효하지 않은 인덱스면 종료

        if (slotIndex < leftArrowIndicators.Count && leftArrowIndicators[slotIndex] != null)
            leftArrowIndicators[slotIndex].SetActive(true); // 해당 슬롯 왼쪽 화살표 표시

        if (slotIndex < rightArrowIndicators.Count && rightArrowIndicators[slotIndex] != null)
            rightArrowIndicators[slotIndex].SetActive(true); // 해당 슬롯 오른쪽 화살표 표시
    }

    /// <summary>
    /// 모든 슬롯의 화살표 오브젝트를 숨긴다.
    /// </summary>
    public void ClearEditingSlotIndicator()
    {
        for (int i = 0; i < leftArrowIndicators.Count; i++)
        {
            if (leftArrowIndicators[i] != null)
                leftArrowIndicators[i].SetActive(false); // 왼쪽 화살표 비활성화
        }

        for (int i = 0; i < rightArrowIndicators.Count; i++)
        {
            if (rightArrowIndicators[i] != null)
                rightArrowIndicators[i].SetActive(false); // 오른쪽 화살표 비활성화
        }
    }

    /// <summary>
    /// 특정 슬롯의 시약 스프라이트를 갱신한다.
    /// </summary>
    public void SetSlotSprite(int slotIndex, ReagentType reagentType)
    {
        if (slotIndex < 0 || slotIndex >= reagentSlotRenderers.Count)
            return; // 슬롯 범위 밖이면 종료

        SpriteRenderer slotRenderer = reagentSlotRenderers[slotIndex]; // 대상 슬롯 시약 렌더러 참조
        if (slotRenderer == null)
            return; // 참조 없으면 종료

        slotRenderer.sprite = GetReagentSprite(reagentType); // 시약 타입에 맞는 스프라이트 적용
        slotRenderer.color = Color.white; // 시약 스프라이트는 기본 흰색 표시
    }

    /// <summary>
    /// 프로그레스 바 10칸을 전부 비활성 색으로 초기화한다.
    /// </summary>
    public void ResetProgressBar()
    {
        for (int i = 0; i < progressBarRenderers.Count; i++)
        {
            SpriteRenderer progressRenderer = progressBarRenderers[i]; // 현재 프로그레스 렌더러 참조
            if (progressRenderer == null)
                continue; // 참조 없으면 스킵

            progressRenderer.color = progressInactiveColor; // 기본 어두운 녹색 적용
        }
    }

    /// <summary>
    /// 0부터 progressIndex까지의 프로그레스 칸을 활성 색으로 바꾼다.
    /// progressIndex는 0 기반 인덱스를 기준으로 사용한다.
    /// </summary>
    public void SetProgressActiveUpTo(int progressIndex)
    {
        ResetProgressBar(); // 먼저 전부 기본색으로 초기화

        for (int i = 0; i <= progressIndex && i < progressBarRenderers.Count; i++)
        {
            SpriteRenderer progressRenderer = progressBarRenderers[i]; // 현재 프로그레스 렌더러 참조
            if (progressRenderer == null)
                continue; // 참조 없으면 스킵

            progressRenderer.color = progressActiveColor; // 진행된 칸은 밝은 연두색 적용
        }
    }

    /// <summary>
    /// 기본 제조 화면 상태로 되돌린다.
    /// </summary>
    public void ResetToDefault()
    {
        if (defaultRoot != null)
            defaultRoot.SetActive(true); // 기본 제조 화면 표시

        if (solvedRoot != null)
            solvedRoot.SetActive(false); // 성공 화면 숨김

        if (stage3HintRoot != null)
            stage3HintRoot.SetActive(false); // 3단계 힌트 화면 숨김

        ApplySlotBackgrounds(); // 슬롯 배경 기본 상태 적용
        ClearEditingSlotIndicator(); // 화살표 UI 초기화
        ResetProgressBar(); // 프로그레스 바 초기화

        Log("기본 상태로 초기화");
    }

    /// <summary>
    /// 성공 화면으로 전환한다.
    /// </summary>
    public void ShowSolvedState()
    {
        if (defaultRoot != null)
            defaultRoot.SetActive(false); // 기본 제조 화면 숨김

        if (solvedRoot != null)
            solvedRoot.SetActive(true); // 성공 화면 표시

        if (stage3HintRoot != null)
            stage3HintRoot.SetActive(false); // 3단계 힌트 화면 숨김

        Log("성공 화면 표시");
    }

    /// <summary>
    /// 성공 후 3단계 힌트 화면으로 전환한다.
    /// </summary>
    public void ShowStage3HintState()
    {
        if (defaultRoot != null)
            defaultRoot.SetActive(false); // 기본 제조 화면 숨김

        if (solvedRoot != null)
            solvedRoot.SetActive(false); // 성공 화면 숨김

        if (stage3HintRoot != null)
            stage3HintRoot.SetActive(true); // 3단계 힌트 화면 표시

        Log("3단계 힌트 화면 표시");
    }

    /// <summary>
    /// 시약 타입에 대응하는 스프라이트를 반환한다.
    /// </summary>
    private Sprite GetReagentSprite(ReagentType reagentType)
    {
        switch (reagentType)
        {
            case ReagentType.ReagentA:
                return reagentASprite; // 시약 A 스프라이트 반환
            case ReagentType.ReagentB:
                return reagentBSprite; // 시약 B 스프라이트 반환
            case ReagentType.ReagentC:
                return reagentCSprite; // 시약 C 스프라이트 반환
            case ReagentType.ReagentD:
                return reagentDSprite; // 시약 D 스프라이트 반환
            case ReagentType.ReagentE:
                return reagentESprite; // 시약 E 스프라이트 반환
            case ReagentType.ReagentF:
                return reagentFSprite; // 시약 F 스프라이트 반환
            default:
                return emptyReagentSprite; // 비어 있는 상태 스프라이트 반환
        }
    }

    /// <summary>
    /// 일반 디버그 로그 출력.
    /// </summary>
    private void Log(string message)
    {
        if (!enableDebugLog)
            return; // 로그 비활성 상태면 종료

        Debug.Log($"[ReagentCraftView] {message}", this); // 디버그 로그 출력
    }
}