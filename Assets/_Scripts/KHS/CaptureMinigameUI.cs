using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

/// <summary>
/// 1500px 규격에 최적화된 심박계 미니게임.
/// 성공/실패 시 배경 그래프 라인의 색상을 변경하여 강력한 시각 피드백을 제공함.
/// </summary>
public class CaptureMinigameUI : MonoBehaviour
{
    [Header("연결된 UI 부품")]
    [SerializeField] private RectTransform scannerBar;
    [SerializeField] private RectTransform graphContainer;
    [SerializeField] private Slider traumaSlider;

    // 🛠️ [신규 단자] 배경 심전도 라인 이미지 (1500px 짜리 그 이미지!)
    [SerializeField] private RawImage graphImage;

    [Header("게임 설정")]
    [SerializeField] private float totalDuration = 5.0f;
    [SerializeField] private int totalBeats = 5;
    [SerializeField] private float hitBoxWidth = 100f;

    [Header("시각 피드백")]
    [SerializeField] private RawImage scannerImage;
    [SerializeField] private Color normalColor = Color.white; // 기본 흰색
    [SerializeField] private Color successColor = Color.green; // 성공 녹색
    [SerializeField] private Color failColor = Color.red;       // 실패 빨간색

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

        // 🛠️ 기동 시 초기 색상 세팅 (흰색)
        if (graphImage != null) graphImage.color = normalColor;
        if (scannerImage != null) scannerImage.color = normalColor;

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
                // 🛠️ 클릭 못 하고 넘어갔을 때 (실패 처리)
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
            // 🛠️ 성공 시 녹색으로 깜빡임!
            StopAllCoroutines();
            StartCoroutine(FlashVisuals(successColor));
        }
        else
        {
            // 🛠️ 엇박자 클릭 시 실패 처리
            HandleFailure();
        }
    }

    private void HandleFailure()
    {
        if (!_isActive) return;

        // 🛠️ 실패 시 빨간색으로 깜빡임!
        StopAllCoroutines();
        if (gameObject.activeInHierarchy) StartCoroutine(FlashVisuals(failColor));

        ResetSession();
    }

    /// <summary>
    /// 🛠️ [개조] 스캐너 바와 배경 그래프 라인을 동시에 깜빡이게 함
    /// </summary>
    private System.Collections.IEnumerator FlashVisuals(Color targetColor)
    {
        // 1. 목표 색상으로 변경
        if (scannerImage != null) scannerImage.color = targetColor;
        if (graphImage != null) graphImage.color = targetColor;

        yield return new WaitForSeconds(0.2f); // 깜빡임 시간

        // 2. 다시 기본 흰색으로 복구
        if (scannerImage != null) scannerImage.color = normalColor;
        if (graphImage != null) graphImage.color = normalColor;
    }

    public void CloseUI()
    {
        _isActive = false;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        gameObject.SetActive(false);
    }
}