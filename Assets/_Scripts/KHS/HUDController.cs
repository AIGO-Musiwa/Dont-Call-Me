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
    [SerializeField] private GameObject normalGroup;
    [SerializeField] private GameObject capturedGroup;
    [SerializeField] private GameObject spectatorGroup;

    [Header("Capture Minigame 모듈")]
    [SerializeField] private CaptureMinigameUI minigameUI;

    [Header("Normal UI 부품")]
    [SerializeField] private TextMeshProUGUI itemNameText;  // 장착 중인 아이템 이름
    [SerializeField] private Image crosshairImage;          // 중앙 조준점
    // 🛠️ [신규 부품] 중앙 조준점 주변을 감싸는 원형 진행도 게이지
    [SerializeField] private Image interactionGaugeImage;

    [Header("Captured UI 부품")]
    [SerializeField] private TextMeshProUGUI traumaPercentText; // 후유증 수치 (%)
    [SerializeField] private Slider traumaGauge;                // 후유증 시각화 게이지
    [SerializeField] private TextMeshProUGUI traumaTimeText;    // 한계 도달까지 남은 시간

    [Header("Spectator UI 부품")]
    [SerializeField] private TextMeshProUGUI spectatorStatusText; // "관전 중" 고정 문구
    [SerializeField] private TextMeshProUGUI spectatorTargetText; // "관전 대상 이름"
    [SerializeField] private TextMeshProUGUI spectatorGuideText;  // "조작 가이드"
    [SerializeField] private GameObject deathOverlay;             // 사망 직후 안내 UI

    private ItemObject _lastItem;
    // 초기 상태를 알 수 없는 상태(-1)로 설정하여 첫 프레임에 무조건 초기화 실행
    private PlayerState _lastState = (PlayerState)(-1);
    private bool _isInitialized = false;

    private void Awake()
    {
        // [자동 배선 공정] 인스펙터 비할당 시 이름으로 자동 검색
        if (itemNameText == null) itemNameText = FindInChild<TextMeshProUGUI>("ItemNameText");
        if (traumaPercentText == null) traumaPercentText = FindInChild<TextMeshProUGUI>("TraumaText");
        if (traumaTimeText == null) traumaTimeText = FindInChild<TextMeshProUGUI>("TraumaTimeText");
        if (spectatorStatusText == null) spectatorStatusText = FindInChild<TextMeshProUGUI>("SpectatorStatusText");
        if (spectatorTargetText == null) spectatorTargetText = FindInChild<TextMeshProUGUI>("SpectatorTargetText");
        if (spectatorGuideText == null) spectatorGuideText = FindInChild<TextMeshProUGUI>("SpectatorGuideText");

        // 🛠️ 인스펙터에 빈 카트리지 변수를 뚫을 필요 없이, 그냥 전원만 바로 내린다!
        if (BGMManager.Instance != null)
        {
            BGMManager.Instance.StopBGM();
        }

        if (deathOverlay == null)
        {
            Transform t = transform.Find("DeathOverlay");
            if (t != null) deathOverlay = t.gameObject;
        }

        if (crosshairImage == null) crosshairImage = FindInChild<Image>("Crosshair");

        // 🛠️ 게이지 자동 검색 및 초기화 시 전원 차단
        if (interactionGaugeImage == null) interactionGaugeImage = FindInChild<Image>("InteractionGauge");
        if (interactionGaugeImage != null) interactionGaugeImage.gameObject.SetActive(false);

        if (traumaGauge == null) traumaGauge = FindInChild<Slider>("TraumaGauge");
        if (minigameUI == null) minigameUI = GetComponentInChildren<CaptureMinigameUI>(true);

        // 시작 시 모든 그룹 초기화 (합선 방지)
        CleanUpLayout();
    }

    private void LateUpdate()
    {
        // 1. 보안 필터: 내 로컬 유닛의 데이터만 수신
        if (playerController == null || !playerController.HasInputAuthority) return;

        PlayerState currentState = playerController.NetPlayerState;

        // 2. 상태 변화 감지 및 레이아웃 스위칭
        if (_lastState != currentState || !_isInitialized)
        {
            OnStateChanged(currentState);
            _lastState = currentState;
            _isInitialized = true;
        }

        // 3. 데이터 동기화 (실시간 계기판 갱신)
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

    // ─── [새로 추가된 데이터 주입구] ────────────────────────────────────────

    /// <summary>
    /// 라디오 수리 등 '지속형 상호작용'의 진행도를 원형 게이지로 그린다.
    /// 외부(PlayerController 등)에서 매 프레임 호출해주어야 함.
    /// </summary>
    /// <param name="current">현재 진행도 (예: 3초)</param>
    /// <param name="max">최대 진행도 (예: 10초)</param>
    public void UpdateInteractionGauge(float current, float max)
    {
        if (interactionGaugeImage == null) return;

        // 진행도가 0보다 크고 완전히 끝나지 않았을 때만 게이지 표시
        if (current > 0f && current < max)
        {
            if (!interactionGaugeImage.gameObject.activeSelf)
                interactionGaugeImage.gameObject.SetActive(true);

            interactionGaugeImage.fillAmount = current / max;
        }
        else
        {
            // 진행도가 0이거나 완료되면 게이지 전원 차단
            if (interactionGaugeImage.gameObject.activeSelf)
                interactionGaugeImage.gameObject.SetActive(false);
        }
    }

    // ─── [이하 기존 코드 유지] ────────────────────────────────────────

    private void OnStateChanged(PlayerState newState)
    {
        if (normalGroup != null) normalGroup.SetActive(newState == PlayerState.Normal);
        if (capturedGroup != null) capturedGroup.SetActive(newState == PlayerState.Captured);
        if (spectatorGroup != null) spectatorGroup.SetActive(newState == PlayerState.Dead || newState == PlayerState.Escaped);

        if (newState == PlayerState.Captured)
        {
            if (minigameUI != null) minigameUI.OpenUI(playerController);
        }
        else
        {
            if (minigameUI != null) minigameUI.CloseUI();
        }
    }

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

    private void UpdateCapturedHUD()
    {
        float trauma = playerController.NetAftereffectPercent;
        if (traumaPercentText != null) traumaPercentText.text = $"후유증: {trauma:F1}%";
        if (traumaGauge != null) traumaGauge.value = trauma / 100f;
        if (traumaTimeText != null)
        {
            float remainingTime = Mathf.Max(0, 100f - trauma);
            traumaTimeText.text = $"한계 도달까지: {remainingTime:F0}s";
        }
    }

    private void UpdateSpectatorHUD()
    {
        if (deathOverlay != null)
            deathOverlay.SetActive(playerController.NetPlayerState == PlayerState.Dead);

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

    public void LinkPlayer(PlayerController pc)
    {
        playerController = pc;
        _isInitialized = false;
    }

    private void CleanUpLayout()
    {
        if (normalGroup != null) normalGroup.SetActive(false);
        if (capturedGroup != null) capturedGroup.SetActive(false);
        if (spectatorGroup != null) spectatorGroup.SetActive(false);
    }

    private T FindInChild<T>(string name) where T : Component
    {
        Transform t = transform.Find(name);
        if (t == null)
        {
            T component = GetComponentInChildren<T>(true);
            if (component != null && component.name == name) return component;
            return null;
        }
        return t.GetComponent<T>();
    }
}