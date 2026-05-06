using UnityEngine;
using UnityEngine.UI;
using System.Collections;
// using UnityEngine.InputSystem; // 🛠️ 이제 InputHandler를 거치므로 직접 InputSystem을 참조할 필요 없음

/// <summary>
/// 1500px 규격 최적화 심박계 미니게임.
/// 스캐너는 고정된 비주얼을 유지하며, 배경 그래프(RawImage)의 색상만으로 피드백을 출력함.
/// 5번의 시도(1사이클)를 진행한 뒤, 성공한 횟수만큼 서버에 일괄 방출(결산)한다.
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
    [SerializeField] private int totalBeats = 5;             // 1사이클당 비트 수
    [SerializeField] private float hitBoxWidth = 100f;       // 히트박스 가로 폭

    [Header("시각 피드백 색상")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color successColor = Color.green;
    [SerializeField] private Color failColor = Color.red;

    private PlayerController _owner;
    private InputHandler _inputHandler; // 🛠️ [신규 부품] 로컬 플레이어의 입력 수집기

    private float _sessionTimer = 0f;
    private int _currentBeatIndex = 0;

    // 사이클 내 로컬 상태 트래킹
    private bool _hasClickedThisBeat = false;
    private int _cycleSuccessCount = 0; // 이번 사이클에서 성공한 횟수

    private bool _isActive = false;

    public void OpenUI(PlayerController owner)
    {
        _owner = owner;
        _isActive = true;
        gameObject.SetActive(true);

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        // 🛠️ UI가 열릴 때, 플레이어의 InputHandler 배선을 꽂아줌
        if (_owner != null)
        {
            _inputHandler = _owner.GetComponent<InputHandler>();
        }

        ResetVisuals();
        ResetSession();
    }

    private void ResetSession()
    {
        _sessionTimer = 0f;
        _currentBeatIndex = 0;
        _hasClickedThisBeat = false;
        _cycleSuccessCount = 0; // 사이클 초기화 시 성공 스택도 0으로 포맷

        UpdateScannerPosition();
    }

    private void Update()
    {
        if (!_isActive) return;

        // 🛠️ [개조 포인트] 휘발성 변수 대신, '버퍼 소모형 함수'를 호출해서 스페이스바 입력을 100% 안전하게 낚아챔!
        if (_inputHandler != null && _inputHandler.ConsumeMinigameInput())
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
            // 클릭하지 않고 비트가 지나가 버린 경우 (무위험 실패)
            if (!_hasClickedThisBeat && _currentBeatIndex < totalBeats)
            {
                TriggerVisualFeedback(failColor);
            }

            _currentBeatIndex = checkIndex;
            _hasClickedThisBeat = false; // 다음 비트를 위해 클릭 권한 장전

            // 1사이클(5번) 스캔이 완전히 끝남! (결산 타이밍)
            if (_currentBeatIndex >= totalBeats)
            {
                if (_cycleSuccessCount > 0 && _owner != null && _owner.Object.HasInputAuthority)
                {
                    // 모아둔 성공 스택을 서버로 발송
                    _owner.RPC_ProcessMinigameSuccess(_cycleSuccessCount);
                }

                // 사이클 무한 반복
                ResetSession();
            }
        }
    }

    public void OnClickInput()
    {
        // 이미 이번 비트에서 스위치를 눌렀다면 무시
        if (!_isActive || _hasClickedThisBeat) return;

        _hasClickedThisBeat = true; // 스위치 락 온 (1비트 1클릭 제한)

        float widthPerBeat = graphContainer.rect.width / totalBeats;
        float targetCenterX = (_currentBeatIndex * widthPerBeat) + (widthPerBeat / 2f);
        float hitBoxStart = targetCenterX - (hitBoxWidth / 2f);
        float hitBoxEnd = targetCenterX + (hitBoxWidth / 2f);
        float currentScannerX = scannerBar.anchoredPosition.x;

        if (currentScannerX >= hitBoxStart && currentScannerX <= hitBoxEnd)
        {
            // 성공: 스택 적립 및 녹색등
            _cycleSuccessCount++;
            TriggerVisualFeedback(successColor);
        }
        else
        {
            // 실패: 리스크 없이 빨간등만 점등하고 지나감
            TriggerVisualFeedback(failColor);
        }
    }

    private void TriggerVisualFeedback(Color color)
    {
        StopAllCoroutines();
        if (gameObject.activeInHierarchy) StartCoroutine(FlashVisuals(color));
    }

    private void ResetVisuals()
    {
        if (graphImage != null) graphImage.color = normalColor;
    }

    private IEnumerator FlashVisuals(Color targetColor)
    {
        if (graphImage != null) graphImage.color = targetColor;
        yield return new WaitForSeconds(0.15f);

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