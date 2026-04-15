using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// 인게임 HUD 통합 관리 모듈.
/// 일반(Normal), 포획(Captured), 관전(Spectator) 상태에 따른 
/// 계기판 전환 및 데이터 출력을 담당한다.
/// </summary>
public class HUDController : MonoBehaviour
{
    [Header("연결된 시스템")]
    [SerializeField] private PlayerController playerController;

    [Header("UI 그룹 (상태별 부모 오브젝트)")]
    [Tooltip("일반 생존 상태에서 활성화될 UI 묶음")]
    [SerializeField] private GameObject normalGroup;

    [Tooltip("포획(Captured) 상태에서 활성화될 UI 묶음")]
    [SerializeField] private GameObject capturedGroup;

    [Tooltip("사망 및 탈출 후 관전 상태에서 활성화될 UI 묶음")]
    [SerializeField] private GameObject spectatorGroup;

    [Header("Normal UI 부품")]
    [SerializeField] private TextMeshProUGUI itemNameText;  // 장착 중인 아이템 이름
    [SerializeField] private Image crosshairImage;          // 중앙 조준점 점(Dot)

    [Header("Captured UI 부품")]
    [SerializeField] private TextMeshProUGUI traumaPercentText; // 후유증 수치 (%)
    [SerializeField] private Slider traumaGauge;                // 후유증 시각화 게이지
    [SerializeField] private TextMeshProUGUI traumaTimeText;    // 한계 도달까지 남은 시간

    [Header("Spectator UI 부품 (3분할 제어)")]
    [SerializeField] private TextMeshProUGUI spectatorStatusText; // "관전 중" (고정 문구)
    [SerializeField] private TextMeshProUGUI spectatorTargetText; // "플레이어 이름" (가장 크게 표시)
    [SerializeField] private TextMeshProUGUI spectatorGuideText;  // "[좌/우 클릭] 대상 전환" (하단 가이드)
    [SerializeField] private GameObject deathOverlay;            // 사망 직후 중앙 안내

    private ItemObject _lastItem; // 아이템 이름 갱신 최적화를 위한 이전 아이템 저장용

    private void Awake()
    {
        // [자동 배선 공정] 인스펙터에서 할당되지 않은 부품들을 자식 오브젝트에서 이름으로 검색한다.
        // 텍스트 부품 검색
        if (itemNameText == null) itemNameText = FindInChild<TextMeshProUGUI>("ItemNameText");
        if (traumaPercentText == null) traumaPercentText = FindInChild<TextMeshProUGUI>("TraumaText");
        if (traumaTimeText == null) traumaTimeText = FindInChild<TextMeshProUGUI>("TraumaTimeText");
        // [관전 부품 3분할 자동 검색]
        if (spectatorStatusText == null) spectatorStatusText = FindInChild<TextMeshProUGUI>("SpectatorStatusText");
        if (spectatorTargetText == null) spectatorTargetText = FindInChild<TextMeshProUGUI>("SpectatorTargetText");
        if (spectatorGuideText == null) spectatorGuideText = FindInChild<TextMeshProUGUI>("SpectatorGuideText");

        if (deathOverlay == null)
        {
            Transform t = transform.Find("DeathOverlay");
            if (t != null) deathOverlay = t.gameObject;
        }

        // 이미지 및 슬라이더 부품 검색
        if (crosshairImage == null) crosshairImage = FindInChild<Image>("Crosshair");
        if (traumaGauge == null) traumaGauge = FindInChild<Slider>("TraumaGauge");

        // 오브젝트 그룹 검색
        if (deathOverlay == null)
        {
            Transform t = transform.Find("DeathOverlay");
            if (t != null) deathOverlay = t.gameObject;
        }
    }

    private void LateUpdate()
    {
        // 1. 보안 필터: 내 로컬 기체의 데이터만 수신해야 함 (HasInputAuthority 확인)
        if (playerController == null || !playerController.HasInputAuthority) return;

        // 2. 레이아웃 전환: 현재 플레이어 상태(NetPlayerState)에 따라 활성화할 UI 그룹을 결정
        UpdateHUDLayout(playerController.NetPlayerState);

        // 3. 데이터 동기화: 각 상태별로 필요한 세부 계기판 수치를 갱신
        switch (playerController.NetPlayerState)
        {
            case PlayerState.Normal:
                UpdateNormalHUD();
                break;
            case PlayerState.Captured:
                UpdateCapturedHUD();
                break;
            case PlayerState.Dead:
            case PlayerState.Escaped:
                UpdateSpectatorHUD();
                break;
        }
    }

    /// <summary>
    /// 플레이어 상태 주파수에 맞춰 UI 그룹의 가시성을 스위칭한다.
    /// </summary>
    private void UpdateHUDLayout(PlayerState state)
    {
        // 각 그룹의 존재 여부를 확인한 뒤, 상태가 일치할 때만 활성화(SetActive)
        if (normalGroup != null) normalGroup.SetActive(state == PlayerState.Normal);
        if (capturedGroup != null) capturedGroup.SetActive(state == PlayerState.Captured);

        // 사망(Dead)과 탈출(Escaped)은 공통적으로 관전 레이아웃을 사용한다.
        if (spectatorGroup != null) spectatorGroup.SetActive(state == PlayerState.Dead || state == PlayerState.Escaped);
    }

    /// <summary>
    /// 일반 생존 상태의 UI 데이터를 갱신한다.
    /// </summary>
    private void UpdateNormalHUD()
    {
        if (itemNameText == null) return;

        // 플레이어가 오른손에 들고 있는 아이템 오브젝트 정보를 가져옴
        ItemObject currentItem = playerController.GetRightHandItemObject();

        // [최적화] 이전 아이템과 다를 때만 텍스트를 변경하여 UI 갱신 부하를 줄임
        if (_lastItem != currentItem)
        {
            itemNameText.text = (currentItem != null) ? currentItem.ItemName : "맨손";
            _lastItem = currentItem;
        }
    }

    /// <summary>
    /// 포획(Captured) 상태에서의 후유증 수치와 남은 생존 시간을 계산하여 출력한다.
    /// </summary>
    private void UpdateCapturedHUD()
    {
        // 후유증 수치(Trauma) 데이터 수신
        float trauma = playerController.NetAftereffectPercent;

        // 1. 퍼센트 텍스트 갱신 (소수점 첫째 자리까지 표시)
        if (traumaPercentText != null) traumaPercentText.text = $"후유증: {trauma:F1}%";

        // 2. 시각 게이지 갱신 (Slider는 0~1 범위를 사용하므로 100으로 나눔)
        if (traumaGauge != null) traumaGauge.value = trauma / 100f;

        // 3. 한계 도달 시간 계산: 기획서에 따라 (100 - 현재 후유증)을 남은 시간으로 처리
        if (traumaTimeText != null)
        {
            float remainingTime = Mathf.Max(0, 100f - trauma);
            traumaTimeText.text = $"한계 도달까지: {remainingTime:F0}s";
        }
    }

    /// <summary>
    /// 관전 화면의 3가지 텍스트 요소를 각각 갱신한다.
    /// </summary>
    private void UpdateSpectatorHUD()
    {
        // 1. 사망 오버레이 제어 (사망 상태일 때만 출력)
        if (deathOverlay != null)
        {
            deathOverlay.SetActive(playerController.NetPlayerState == PlayerState.Dead);
        }

        // 2. 관전 데이터 수신 및 출력
        if (playerController.SpectatorController != null)
        {
            string targetName = playerController.SpectatorController.GetCurrentTargetName();

            // 관전 가능한 대상이 없는 경우 (전원 사망/탈출)
            if (string.IsNullOrEmpty(targetName))
            {
                if (spectatorStatusText != null) spectatorStatusText.text = "<color=red>관전 종료</color>";
                if (spectatorTargetText != null) spectatorTargetText.text = "생존자 없음";
                if (spectatorGuideText != null) spectatorGuideText.text = "세션이 곧 종료됩니다.";
            }
            else
            {
                // 정상 관전 중: 각 텍스트에 역할 분담
                if (spectatorStatusText != null) spectatorStatusText.text = "관전 중";
                if (spectatorTargetText != null) spectatorTargetText.text = targetName; // 닉네임만 딱!
                if (spectatorGuideText != null) spectatorGuideText.text = "[좌/우 클릭] 대상 전환";
            }
        }
    }

    /// <summary>
    /// 플레이어 기체가 스폰될 때 HUD 시스템과 데이터 링크를 확립한다.
    /// </summary>
    public void LinkPlayer(PlayerController pc)
    {
        playerController = pc;
        Debug.Log($"[HUD] 유닛 {pc.Object.InputAuthority}번과 시스템 페어링 완료.");
    }

    /// <summary>
    /// 자식 오브젝트에서 특정 타입의 컴포넌트를 이름으로 검색하는 보조 함수.
    /// </summary>
    private T FindInChild<T>(string name) where T : Component
    {
        Transform t = transform.Find(name);
        return t != null ? t.GetComponent<T>() : null;
    }
}