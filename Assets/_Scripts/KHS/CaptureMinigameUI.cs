using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using System.Collections;

/// <summary>
/// 1500px 규격 최적화 심박계 미니게임.
/// 스캐너는 고정된 비주얼을 유지하며, 배경 그래프(RawImage)의 색상만으로 피드백을 출력함.
/// </summary>
public class CaptureMinigameUI : MonoBehaviour
{
    [Header("연결된 UI 부품")]
    [SerializeField] private RectTransform scannerBar;       // 이동을 담당하는 트랜스폼
    [SerializeField] private RectTransform graphContainer;   // 1500px 기준 틀
    [SerializeField] private Slider traumaSlider;            // 후유증 게이지

    [Header("배경 피드백")]
    [SerializeField] private RawImage graphImage;            // 배경 심전도 라인 (색상 변경 대상)

    [Header("게임 설정")]
    [SerializeField] private float totalDuration = 5.0f;     // 총 이동 시간
    [SerializeField] private int totalBeats = 5;             // 비트 수
    [SerializeField] private float hitBoxWidth = 100f;       // 히트박스 가로 폭

    [Header("시각 피드백 색상")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color successColor = Color.green;
    [SerializeField] private Color failColor = Color.red;

    private PlayerController _owner;
    private float _sessionTimer = 0f;
    private int _currentBeatIndex = 0;
    private bool _hasClickedThisBeat = false;
    private bool _isActive = false;

    public void OpenUI(PlayerController owner)
    {
        _owner = owner;
        _isActive = true;
        gameObject.SetActive(true);

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        ResetVisuals();
        ResetSession();
    }

    private void ResetSession()
    {
        _sessionTimer = 0f;
        _currentBeatIndex = 0;
        _hasClickedThisBeat = false;
        UpdateScannerPosition();
    }

    private void Update()
    {
        if (!_isActive) return;

        if (Pointer.current != null && Pointer.current.press.wasPressedThisFrame)
        {
            OnClickInput();
        }

        _sessionTimer += Time.deltaTime;
        UpdateScannerPosition();

        if (_owner != null && traumaSlider != null)
        {
            traumaSlider.value = _owner.NetAftereffectPercent / 100f;
        }

        UpdateSessionLogic();
    }

    private void UpdateScannerPosition()
    {
        if (scannerBar == null || graphContainer == null) return;

        // 진행도에 따른 단순 위치 이동 (1500px 트랙)
        float progress = Mathf.Clamp01(_sessionTimer / totalDuration);
        float targetX = progress * graphContainer.rect.width;
        scannerBar.anchoredPosition = new Vector2(targetX, scannerBar.anchoredPosition.y);
    }

    private void UpdateSessionLogic()
    {
        float timePerBeat = totalDuration / totalBeats;
        int checkIndex = Mathf.FloorToInt(_sessionTimer / timePerBeat);

        if (checkIndex > _currentBeatIndex)
        {
            if (!_hasClickedThisBeat && _currentBeatIndex < totalBeats)
            {
                HandleFailure();
                return;
            }

            _currentBeatIndex = checkIndex;
            _hasClickedThisBeat = false;

            if (_currentBeatIndex >= totalBeats)
            {
                if (_owner != null && _owner.Object.HasInputAuthority)
                {
                    _owner.RPC_ProcessMinigameSuccess();
                }
                ResetSession();
            }
        }
    }

    public void OnClickInput()
    {
        if (!_isActive || _hasClickedThisBeat) return;

        float widthPerBeat = graphContainer.rect.width / totalBeats;
        float targetCenterX = (_currentBeatIndex * widthPerBeat) + (widthPerBeat / 2f);
        float hitBoxStart = targetCenterX - (hitBoxWidth / 2f);
        float hitBoxEnd = targetCenterX + (hitBoxWidth / 2f);
        float currentScannerX = scannerBar.anchoredPosition.x;

        if (currentScannerX >= hitBoxStart && currentScannerX <= hitBoxEnd)
        {
            _hasClickedThisBeat = true;

            // 🛠️ 배경 그래프만 녹색으로 점등
            StopAllCoroutines();
            StartCoroutine(FlashVisuals(successColor));
        }
        else
        {
            HandleFailure();
        }
    }

    private void HandleFailure()
    {
        if (!_isActive) return;

        // 🛠️ 배경 그래프만 빨간색으로 점등
        StopAllCoroutines();
        if (gameObject.activeInHierarchy) StartCoroutine(FlashVisuals(failColor));

        ResetSession();
    }

    private void ResetVisuals()
    {
        if (graphImage != null) graphImage.color = normalColor;
    }

    private IEnumerator FlashVisuals(Color targetColor)
    {
        // 1. 색상 즉시 변경
        if (graphImage != null) graphImage.color = targetColor;

        yield return new WaitForSeconds(0.15f);

        // 2. 부드러운 복구 (Lerp)
        float elapsed = 0f;
        float duration = 0.2f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            if (graphImage != null)
                graphImage.color = Color.Lerp(targetColor, normalColor, t);

            yield return null;
        }

        ResetVisuals();
    }

    public void CloseUI()
    {
        _isActive = false;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        gameObject.SetActive(false);
    }
}