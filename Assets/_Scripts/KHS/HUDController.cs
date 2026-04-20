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

    [Header("Captured UI 부품")]
    [SerializeField] private TextMeshProUGUI traumaPercentText; // 후유증 수치 (%)
    [SerializeField] private Slider traumaGauge;                // 후유증 시각화 게이지
    [SerializeField] private TextMeshProUGUI traumaTimeText;    // 한계 도달까지 남은 시간

    [Header("Spectator UI 부품")]
    [SerializeField] private TextMeshProUGUI spectatorStatusText; // "관전 중" 고정 문구
    [SerializeField] private TextMeshProUGUI spectatorTargetText; // "관전 대상 이름"
    [SerializeField] private TextMeshProUGUI spectatorGuideText;  // "조작 가이드"
    [SerializeField] private GameObject deathOverlay;            // 사망 직후 안내 UI

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

        if (deathOverlay == null)
        {
            Transform t = transform.Find("DeathOverlay");
            if (t != null) deathOverlay = t.gameObject;
        }

        if (crosshairImage == null) crosshairImage = FindInChild<Image>("Crosshair");
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

    /// <summary>
    /// 플레이어 상태 전환 시 단 한 번 실행되는 레이아웃 스위칭 로직
    /// </summary>
    private void OnStateChanged(PlayerState newState)
    {
        // 각 그룹 활성화 제어
        if (normalGroup != null) normalGroup.SetActive(newState == PlayerState.Normal);
        if (capturedGroup != null) capturedGroup.SetActive(newState == PlayerState.Captured);
        if (spectatorGroup != null) spectatorGroup.SetActive(newState == PlayerState.Dead || newState == PlayerState.Escaped);

        // 미니게임 제어
        if (newState == PlayerState.Captured)
        {
            if (minigameUI != null) minigameUI.OpenUI(playerController);
        }
        else
        {
            if (minigameUI != null) minigameUI.CloseUI();
        }

        Debug.Log($"[HUD] 시스템 상태 전환 완료: {newState}");
    }

    /// <summary>
    /// 일반 생존 상태: 장착 아이템 정보 및 조준점 동기화
    /// </summary>
    private void UpdateNormalHUD()
    {
        if (itemNameText == null) return;

        ItemObject currentItem = playerController.GetRightHandItemObject();

        // 최적화: 아이템이 바뀔 때만 텍스트 갱신
        if (_lastItem != currentItem)
        {
            itemNameText.text = (currentItem != null) ? currentItem.ItemName : "맨손";
            _lastItem = currentItem;
        }
    }

    /// <summary>
    /// 포획 상태: 후유증 수치 및 사망 한계 시간 실시간 출력
    /// </summary>
    private void UpdateCapturedHUD()
    {
        float trauma = playerController.NetAftereffectPercent;

        // 1. 퍼센트 및 슬라이더 갱신
        if (traumaPercentText != null) traumaPercentText.text = $"후유증: {trauma:F1}%";
        if (traumaGauge != null) traumaGauge.value = trauma / 100f;

        // 2. 남은 생존 시간 계산 (기획상 100% 도달 시 사망)
        if (traumaTimeText != null)
        {
            float remainingTime = Mathf.Max(0, 100f - trauma);
            traumaTimeText.text = $"한계 도달까지: {remainingTime:F0}s";
        }
    }

    /// <summary>
    /// 사망/관전 상태: 관전 대상 및 상태 메시지 갱신
    /// </summary>
    private void UpdateSpectatorHUD()
    {
        // 1. 사망 오버레이 (죽은 직후 안내 텍스트)
        if (deathOverlay != null)
        {
            deathOverlay.SetActive(playerController.NetPlayerState == PlayerState.Dead);
        }

        // 2. 관전 컨트롤러로부터 현재 대상 정보 수신
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
    /// 로컬 플레이어 스폰 시 시스템 페어링
    /// </summary>
    public void LinkPlayer(PlayerController pc)
    {
        playerController = pc;
        _isInitialized = false; // 새로 연결 시 초기화 시퀀스 강제 가동
        Debug.Log($"[HUD] 유닛 {pc.Object.InputAuthority}번과 시스템 페어링 완료.");
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