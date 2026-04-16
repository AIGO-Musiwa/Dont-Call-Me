using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// 인게임 HUD 통합 관리 모듈.
/// 일반(Normal), 포획(Captured), 관전(Spectator) 상태에 따른 
/// 계기판 전환 및 데이터 출력을 담당하며, 미니게임 모듈을 제어한다.
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

    [Header("Capture Minigame 모듈")]
    [SerializeField] private CaptureMinigameUI minigameUI; // ECG 보드 제어부

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

    private ItemObject _lastItem;     // 아이템 이름 갱신 최적화용
    private PlayerState _lastState = PlayerState.Normal; // 상태 변화 감지 스위치

    private void Awake()
    {
        // [자동 배선 공정] 인스펙터에서 할당되지 않은 부품들을 자식 오브젝트에서 이름으로 검색
        if (itemNameText == null) itemNameText = FindInChild<TextMeshProUGUI>("ItemNameText");
        if (traumaPercentText == null) traumaPercentText = FindInChild<TextMeshProUGUI>("TraumaText");
        if (traumaTimeText == null) traumaTimeText = FindInChild<TextMeshProUGUI>("TraumaTimeText");

        // [관전 부품 자동 검색]
        if (spectatorStatusText == null) spectatorStatusText = FindInChild<TextMeshProUGUI>("SpectatorStatusText");
        if (spectatorTargetText == null) spectatorTargetText = FindInChild<TextMeshProUGUI>("SpectatorTargetText");
        if (spectatorGuideText == null) spectatorGuideText = FindInChild<TextMeshProUGUI>("SpectatorGuideText");

        if (deathOverlay == null)
        {
            Transform t = transform.Find("DeathOverlay");
            if (t != null) deathOverlay = t.gameObject;
        }

        // 이미지 및 슬라이더 검색
        if (crosshairImage == null) crosshairImage = FindInChild<Image>("Crosshair");
        if (traumaGauge == null) traumaGauge = FindInChild<Slider>("TraumaGauge");

        // [신규] 미니게임 모듈 자동 검색 (CapturedGroup 자식 어딘가에 있으면 됨)
        if (minigameUI == null) minigameUI = GetComponentInChildren<CaptureMinigameUI>(true);
    }

    private void LateUpdate()
    {
        // 1. 보안 필터: 내 로컬 기체의 데이터만 수신 (HasInputAuthority 확인)
        if (playerController == null || !playerController.HasInputAuthority) return;

        // 2. 레이아웃 전환 및 상태 변화 감지
        PlayerState currentState = playerController.NetPlayerState;
        if (_lastState != currentState)
        {
            OnStateChanged(currentState);
            _lastState = currentState;
        }

        // 3. 데이터 동기화: 각 상태별 세부 계기판 수치 갱신
        switch (currentState)
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
    /// 플레이어 상태 주파수가 바뀔 때 단 한 번 실행되는 스위칭 로직
    /// </summary>
    private void OnStateChanged(PlayerState newState)
    {
        // 각 그룹 활성화 제어
        if (normalGroup != null) normalGroup.SetActive(newState == PlayerState.Normal);
        if (capturedGroup != null) capturedGroup.SetActive(newState == PlayerState.Captured);
        if (spectatorGroup != null) spectatorGroup.SetActive(newState == PlayerState.Dead || newState == PlayerState.Escaped);

        // [핵심] 포획 상태 진입/이탈에 따른 미니게임 전원 제어
        if (newState == PlayerState.Captured)
        {
            if (minigameUI != null) minigameUI.OpenUI(playerController);
        }
        else
        {
            // Captured가 아닌 다른 상태로 넘어가면 미니게임 즉시 종료
            if (minigameUI != null) minigameUI.CloseUI();
        }
    }

    /// <summary>
    /// 일반 생존 상태의 UI 데이터를 갱신한다.
    /// </summary>
    private void UpdateNormalHUD()
    {
        if (itemNameText == null) return;

        ItemObject currentItem = playerController.GetRightHandItemObject();

        if (_lastItem != currentItem)
        {
            itemNameText.text = (currentItem != null) ? currentItem.ItemName : "맨손";
            _lastItem = currentItem;
        }
    }

    /// <summary>
    /// 포획(Captured) 상태에서의 후유증 수치와 남은 생존 시간을 출력한다.
    /// </summary>
    private void UpdateCapturedHUD()
    {
        float trauma = playerController.NetAftereffectPercent;

        if (traumaPercentText != null) traumaPercentText.text = $"후유증: {trauma:F1}%";
        if (traumaGauge != null) traumaGauge.value = trauma / 100f;

        if (traumaTimeText != null)
        {
            // 사망 타이머(NetCaptureExpireTimer)가 있다면 남은 시간 표시, 없으면 trauma 기반 계산
            float remainingTime = 100f - trauma;
            traumaTimeText.text = $"한계 도달까지: {remainingTime:F0}s";
        }
    }

    /// <summary>
    /// 관전 화면의 데이터들을 갱신한다.
    /// </summary>
    private void UpdateSpectatorHUD()
    {
        if (deathOverlay != null)
        {
            deathOverlay.SetActive(playerController.NetPlayerState == PlayerState.Dead);
        }

        if (playerController.SpectatorController != null)
        {
            string targetName = playerController.SpectatorController.GetCurrentTargetName();

            if (string.IsNullOrEmpty(targetName))
            {
                if (spectatorStatusText != null) spectatorStatusText.text = "<color=red>관전 종료</color>";
                if (spectatorTargetText != null) spectatorTargetText.text = "생존자 없음";
                if (spectatorGuideText != null) spectatorGuideText.text = "세션이 곧 종료됩니다.";
            }
            else
            {
                if (spectatorStatusText != null) spectatorStatusText.text = "관전 중";
                if (spectatorTargetText != null) spectatorTargetText.text = targetName;
                if (spectatorGuideText != null) spectatorGuideText.text = "[좌/우 클릭] 대상 전환";
            }
        }
    }

    /// <summary>
    /// 플레이어 기체 스폰 시 데이터 링크 확립
    /// </summary>
    public void LinkPlayer(PlayerController pc)
    {
        playerController = pc;
        Debug.Log($"[HUD] 유닛 {pc.Object.InputAuthority}번과 시스템 페어링 완료.");
    }

    private T FindInChild<T>(string name) where T : Component
    {
        Transform t = transform.Find(name);
        if (t == null)
        {
            // 직접적인 자식이 아닐 경우를 대비해 깊은 검색 수행
            T component = GetComponentInChildren<T>(true);
            if (component != null && component.name == name) return component;
            return null;
        }
        return t.GetComponent<T>();
    }
}