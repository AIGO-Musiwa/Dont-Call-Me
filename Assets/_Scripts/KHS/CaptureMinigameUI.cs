using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem; // New Input System 사용

/// <summary>
/// 5초간 1초 간격으로 입력을 성공해야 하는 심박계(ECG) 미니게임.
/// 파형의 피크(양 끝)가 판정 지점에 왔을 때 성공하도록 로직 수정.
/// </summary>
public class CaptureMinigameUI : MonoBehaviour
{
    [Header("연결된 UI 부품")]
    [SerializeField] private RawImage ecgGraph;      // UV 스크롤될 그래프
    [SerializeField] private Slider traumaSlider;    // 후유증 게이지
    [SerializeField] private float scrollSpeed = 1.0f; // 1.0 권장

    [Header("판정 설정")]
    [Range(0f, 0.5f)]
    [SerializeField] private float successThreshold = 0.15f; // 판정 허용 범위 (유예 시간)

    [Header("시각 피드백")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color successColor = Color.green;
    [SerializeField] private Color failColor = Color.red;

    private PlayerController _owner;
    private float _sessionTimer = 0f;
    private int _currentBeatIndex = 0;
    private bool _hasClickedThisBeat = false;
    private bool _isActive = false;

    // 내부 UV 추적용 변수
    private float _currentUvX = 0f;

    public void OpenUI(PlayerController owner)
    {
        _owner = owner;
        _isActive = true;
        gameObject.SetActive(true);

        if (ecgGraph != null)
        {
            _currentUvX = 0f;
            // 가로 5칸 반복 세팅
            ecgGraph.uvRect = new Rect(0, 0, 5, 1);
            ecgGraph.color = normalColor;
        }

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        ResetSession();
        Debug.Log("<color=cyan>[ECG]</color> 시스템 기동 성공. 스파이크 판정 모드.");
    }

    private void ResetSession()
    {
        _sessionTimer = 0f;
        _currentBeatIndex = 0;
        _hasClickedThisBeat = false;
    }

    private void Update()
    {
        if (!_isActive) return;

        // New Input System 방식으로 마우스 좌클릭 감지
        if (Pointer.current != null && Pointer.current.press.wasPressedThisFrame)
        {
            OnClickInput();
        }

        // 1. 그래프 무한 스크롤
        if (ecgGraph != null)
        {
            _currentUvX += scrollSpeed * Time.deltaTime;
            ecgGraph.uvRect = new Rect(_currentUvX, 0, 5, 1);
        }

        // 2. 데이터 동기화
        if (_owner != null && traumaSlider != null)
        {
            traumaSlider.value = _owner.NetAftereffectPercent / 100f;
        }

        UpdateSessionLogic();
    }

    private void UpdateSessionLogic()
    {
        _sessionTimer += Time.deltaTime;
        int checkIndex = Mathf.FloorToInt(_sessionTimer);

        if (checkIndex > _currentBeatIndex)
        {
            if (!_hasClickedThisBeat)
            {
                HandleFailure();
                return;
            }

            _currentBeatIndex = checkIndex;
            _hasClickedThisBeat = false;

            if (_currentBeatIndex >= 5)
            {
                if (_owner != null) _owner.RPC_ProcessMinigameSuccess();
                ResetSession();
            }
        }
    }

    /// <summary>
    /// [개조] 클릭 입력 판정 로직.
    /// 파형의 양 끝(스파이크)이 판정 범위에 왔는지 체크.
    /// </summary>
    public void OnClickInput()
    {
        if (!_isActive || _hasClickedThisBeat) return;

        // 현재 텍스처 타일 내에서의 위치 (0.0 ~ 1.0)
        float currentBeatPos = _currentUvX % 1.0f;

        // [핵심 변경] 스파이크는 양 끝(0 또는 1)에 있음.
        // 시작점(0.0)에 가까운지, 혹은 끝점(1.0)에 가까운지 체크.
        bool isCloseToStart = currentBeatPos <= successThreshold;
        bool isCloseToEnd = currentBeatPos >= (1.0f - successThreshold);

        // 둘 중 하나라도 해당하면 성공
        if (isCloseToStart || isCloseToEnd)
        {
            _hasClickedThisBeat = true;
            StopAllCoroutines();
            StartCoroutine(FlashColor(successColor));

            // 디버그용 오차 계산 (시작점 기반 혹은 끝점 기반)
            float nearestPeak = isCloseToStart ? 0f : 1f;
            float diff = Mathf.Abs(currentBeatPos - nearestPeak);
            // 끝점 기반 오차 계산 보정 (1.0 기준)
            if (isCloseToEnd) diff = Mathf.Abs(1.0f - currentBeatPos);

            Debug.Log($"<color=green>[ECG] 스파이크 성공!</color> 오차: {diff:F3} (위치: {currentBeatPos:F3})");
        }
        else
        {
            // 범위를 벗어난 클릭 (평평한 곳)은 실패 처리
            HandleFailure();
            Debug.Log($"<color=red>[ECG] 실패!</color> 평평한 구간 클릭 (위치: {currentBeatPos:F3})");
        }
    }

    private void HandleFailure()
    {
        if (!_isActive) return; // 이미 종료된 상태면 무시
        StopAllCoroutines();
        if (gameObject.activeInHierarchy) StartCoroutine(FlashColor(failColor));
        ResetSession();
    }

    private System.Collections.IEnumerator FlashColor(Color targetColor)
    {
        if (ecgGraph == null) yield break;
        ecgGraph.color = targetColor;
        yield return new WaitForSeconds(0.15f);
        if (ecgGraph != null) ecgGraph.color = normalColor;
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