using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// UGUI(Canvas) 기반 인게임 HUD 관리 모듈.
/// 부품의 유무에 상관없이 시스템이 가동되도록 예외 처리가 강화되었다.
/// </summary>
public class HUDController : MonoBehaviour
{
    [Header("연결된 시스템 (자동 할당)")]
    [SerializeField] private PlayerController playerController;

    [Header("UI 요소 (있으면 자동 연결, 없어도 무관)")]
    [SerializeField] private TextMeshProUGUI itemNameText;
    [SerializeField] private Image crosshairImage;
    [SerializeField] private GameObject capturedOverlay;

    private ItemObject _lastItem;

    private void Awake()
    {
        // [공정 1] 내부 UI 부품 스캔 및 예외 처리
        // 텍스트 검색
        if (itemNameText == null) itemNameText = GetComponentInChildren<TextMeshProUGUI>(true);

        // 조준점 검색 (Find 결과가 null일 경우를 대비해 안전하게 배선)
        if (crosshairImage == null)
        {
            Transform t = transform.Find("Crosshair");
            if (t != null) crosshairImage = t.GetComponent<Image>();
        }

        // 포획 오버레이 검색
        if (capturedOverlay == null)
        {
            Transform t = transform.Find("CapturedOverlay");
            if (t != null) capturedOverlay = t.gameObject;
        }
    }

    private void Start()
    {
        // 부품이 있을 때만 초기화 신호 송신
        if (itemNameText != null) itemNameText.text = "대기 중...";
        if (capturedOverlay != null) capturedOverlay.SetActive(false);
        if (crosshairImage != null) crosshairImage.enabled = true;
    }

    public void LinkPlayer(PlayerController pc)
    {
        playerController = pc;
        if (itemNameText != null) itemNameText.text = "맨손";

        if (pc != null)
            Debug.Log($"[HUD] {pc.Object.InputAuthority}번 기체와 배선 연결 성공.");
    }

    private void LateUpdate()
    {
        // 제어권 확인
        if (playerController == null || !playerController.HasInputAuthority) return;

        UpdateItemName();
        UpdatePlayerStatusUI();
    }

    private void UpdateItemName()
    {
        // 부품이 없으면 공정 건너뜀
        if (itemNameText == null) return;

        ItemObject currentItem = null;
        if (playerController.NetRightHandItem != null)
        {
            currentItem = playerController.NetRightHandItem.GetComponent<ItemObject>();
        }

        if (_lastItem != currentItem)
        {
            itemNameText.text = (currentItem != null) ? currentItem.ItemName : "맨손";
            _lastItem = currentItem;
        }
    }

    private void UpdatePlayerStatusUI()
    {
        // 상태 감지 (플레이어 데이터는 필수)
        if (playerController == null) return;

        bool isCaptured = (playerController.NetPlayerState == PlayerState.Captured);

        // 오버레이 부품이 있을 때만 작동
        if (capturedOverlay != null)
        {
            if (capturedOverlay.activeSelf != isCaptured)
                capturedOverlay.SetActive(isCaptured);
        }

        // 조준점 부품이 있을 때만 작동
        if (crosshairImage != null)
        {
            if (crosshairImage.enabled == isCaptured) // 논리 연산 최적화
                crosshairImage.enabled = !isCaptured;
        }
    }
}