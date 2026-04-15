using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// UGUI(Canvas) 기반 인게임 HUD 관리 모듈.
/// 내부 부품 자동 검색 및 플레이어 자동 연결 기능을 포함한다.
/// </summary>
public class HUDController : MonoBehaviour
{
    [Header("연결된 시스템 (자동 할당 예정)")]
    [SerializeField] private PlayerController playerController;

    [Header("UI 요소 (이름으로 자동 검색)")]
    [SerializeField] private TextMeshProUGUI itemNameText;  // 아이템 이름 표시용
    [SerializeField] private Image crosshairImage;          // 중앙 조준점
    [SerializeField] private GameObject capturedOverlay;    // 포획 시 붉은 화면

    private ItemObject _lastItem;

    private void Awake()
    {
        // [공정 1] 내부 UI 부품 자동 스캔
        // 텍스트는 타입으로, 나머지는 정해진 이름으로 검색한다.
        if (itemNameText == null) itemNameText = GetComponentInChildren<TextMeshProUGUI>(true);

        if (crosshairImage == null)
        {
            Transform t = transform.Find("Crosshair");
            if (t != null) crosshairImage = t.GetComponent<Image>();
        }

        if (capturedOverlay == null)
        {
            Transform t = transform.Find("CapturedOverlay");
            if (t != null) capturedOverlay = t.gameObject;
        }
    }

    private void Start()
    {
        // 초기 상태 설정
        if (itemNameText != null) itemNameText.text = "대기 중...";
        if (capturedOverlay != null) capturedOverlay.SetActive(false);
        if (crosshairImage != null) crosshairImage.enabled = true;
    }

    /// <summary>
    /// [신규] 플레이어 기체가 스폰될 때 이 HUD와 주파수를 맞추기 위한 함수
    /// </summary>
    public void LinkPlayer(PlayerController pc)
    {
        playerController = pc;
        if (itemNameText != null) itemNameText.text = "맨손";
        Debug.Log($"[HUD] {pc.Object.InputAuthority}번 기체와 배선 연결 성공.");
    }

    private void LateUpdate()
    {
        // 내 기체 정보만 출력 (주파수 혼선 방지)
        if (playerController == null || !playerController.HasInputAuthority) return;

        UpdateItemName();
        UpdatePlayerStatusUI();
    }

    private void UpdateItemName()
    {
        if (itemNameText == null) return;

        ItemObject currentItem = null;
        if (playerController.NetRightHandItem != null)
        {
            currentItem = playerController.NetRightHandItem.GetComponent<ItemObject>();
        }

        // 아이템이 변경되었을 때만 출력 갱신
        if (_lastItem != currentItem)
        {
            itemNameText.text = (currentItem != null) ? currentItem.ItemName : "맨손";
            _lastItem = currentItem;
        }
    }

    private void UpdatePlayerStatusUI()
    {
        if (playerController == null) return;

        // 플레이어 상태 감지
        bool isCaptured = (playerController.NetPlayerState == PlayerState.Captured);

        // 포획 상태라면 오버레이를 켜고 조준점을 숨김
        if (capturedOverlay != null && capturedOverlay.activeSelf != isCaptured)
        {
            capturedOverlay.SetActive(isCaptured);
        }

        // 조준점 가시성 제어 (포획 시 비활성화)
        if (crosshairImage != null)
        {
            crosshairImage.enabled = !isCaptured;
        }
    }
}