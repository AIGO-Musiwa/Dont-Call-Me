using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem; // New Input System 사용

/// <summary>
/// 5초간 1초 간격으로 입력을 성공해야 하는 심박계(ECG) 미니게임.
/// 고정된 5개의 노드 위를 스캐너가 지나가며 판정하는 리듬게임 방식.
/// </summary>
public class CaptureMinigameUI : MonoBehaviour
{
    [Header("연결된 UI 부품")]
    [SerializeField] private RectTransform scannerBar;       // 좌우로 이동할 판정 바
    [SerializeField] private RectTransform graphContainer;   // 스캐너가 움직일 전체 영역 (폭)
    [SerializeField] private Slider traumaSlider;            // 후유증 게이지

    [Header("게임 설정")]
    [SerializeField] private float totalDuration = 5.0f;     // 스캐너가 끝까지 가는 데 걸리는 총 시간
    [SerializeField] private int totalBeats = 5;             // 눌러야 할 총 노드 개수
    [Range(0f, 0.5f)]
    [SerializeField] private float successThreshold = 0.15f; // 판정 허용 범위 (유예 시간, 초 단위)

    [Header("시각 피드백")]
    [SerializeField] private Image scannerImage; // 스캐너 색상 변경용
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

        ResetSession();
        Debug.Log("<color=cyan>[ECG]</color> 시스템 기동 성공. 스캐너 이동 모드.");
    }

    private void ResetSession()
    {
        _sessionTimer = 0f;
        _currentBeatIndex = 0;
        _hasClickedThisBeat = false;

        if (scannerImage != null) scannerImage.color = normalColor;
        UpdateScannerPosition();
    }

    private void Update()
    {
        if (!_isActive) return;

        // 마우스 좌클릭 감지
        if (Pointer.current != null && Pointer.current.press.wasPressedThisFrame)
        {
            OnClickInput();
        }

        // 1. 스캐너 이동 로직
        _sessionTimer += Time.deltaTime;
        UpdateScannerPosition();

        // 2. 후유증 동기화
        if (_owner != null && traumaSlider != null)
        {
            traumaSlider.value = _owner.NetAftereffectPercent / 100f;
        }

        UpdateSessionLogic();
    }

    private void UpdateScannerPosition()
    {
        if (scannerBar == null || graphContainer == null) return;

        // 0.0 ~ 1.0 사이의 진행률 (5초 기준)
        float progress = Mathf.Clamp01(_sessionTimer / totalDuration);

        // 그래프 컨테이너의 가로 길이를 기준으로 스캐너 X 좌표 이동
        float targetX = progress * graphContainer.rect.width;
        scannerBar.anchoredPosition = new Vector2(targetX, scannerBar.anchoredPosition.y);
    }

    private void UpdateSessionLogic()
    {
        // 현재 몇 번째 비트를 지나고 있는지 계산 (0, 1, 2, 3, 4)
        // 5초에 5비트면 1초마다 1비트씩 지나감.
        float timePerBeat = totalDuration / totalBeats;
        int checkIndex = Mathf.FloorToInt(_sessionTimer / timePerBeat);

        // 스캐너가 다음 비트 구간으로 넘어갔을 때
        if (checkIndex > _currentBeatIndex)
        {
            // 이전 구간에서 클릭을 안 하고 넘어왔다면 실패!
            if (!_hasClickedThisBeat && _currentBeatIndex < totalBeats)
            {
                HandleFailure();
                return;
            }

            _currentBeatIndex = checkIndex;
            _hasClickedThisBeat = false; // 새 비트 구간이므로 클릭 초기화

            // 끝까지 무사히 도달했다면 미니게임 성공!
            if (_currentBeatIndex >= totalBeats)
            {
                // 🛠️ [서버 통신 수리 완료] 서버로 성공 패킷 확실하게 발송!
                if (_owner != null && _owner.Object.HasInputAuthority)
                {
                    _owner.RPC_ProcessMinigameSuccess();
                }

                // 루프를 돌고 싶다면 ResetSession(), 한 번 성공하고 끝내고 싶다면 CloseUI()
                ResetSession();
            }
        }
    }

    public void OnClickInput()
    {
        if (!_isActive || _hasClickedThisBeat) return;

        // 각 비트가 위치한 정확한 타겟 시간 (예: 0.5초, 1.5초, 2.5초...)
        // 스캐너가 이 시간을 지나갈 때 클릭해야 함!
        float timePerBeat = totalDuration / totalBeats;

        // 현재 내가 도전 중인 비트의 정중앙 시간
        float targetBeatTime = (_currentBeatIndex * timePerBeat) + (timePerBeat / 2f);

        // 현재 스캐너의 시간과 타겟 시간의 오차 계산
        float diff = Mathf.Abs(_sessionTimer - targetBeatTime);

        // 오차가 허용 범위(successThreshold) 안이면 성공!
        if (diff <= successThreshold)
        {
            _hasClickedThisBeat = true;
            StopAllCoroutines();
            StartCoroutine(FlashColor(successColor));

            Debug.Log($"<color=green>[ECG] 노드 {_currentBeatIndex + 1} 격파 성공!</color> 오차: {diff:F3}초");
        }
        else
        {
            // 엇박자로 클릭하면 실패!
            Debug.Log($"<color=red>[ECG] 실패!</color> 엇박자 클릭! 오차: {diff:F3}초");
            HandleFailure();
        }
    }

    private void HandleFailure()
    {
        if (!_isActive) return;

        StopAllCoroutines();
        if (gameObject.activeInHierarchy) StartCoroutine(FlashColor(failColor));
        ResetSession(); // 처음부터 다시
    }

    private System.Collections.IEnumerator FlashColor(Color targetColor)
    {
        if (scannerImage == null) yield break;
        scannerImage.color = targetColor;
        yield return new WaitForSeconds(0.15f);
        if (scannerImage != null) scannerImage.color = normalColor;
    }

    public void CloseUI()
    {
        _isActive = false;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        gameObject.SetActive(false);
        Debug.Log("<color=yellow>[ECG]</color> 시스템 종료.");
    }
}