using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using UnityEngine.InputSystem; // 🛠️ 이중 입력 회로를 위해 추가

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
    [SerializeField] private float hitBoxWidth = 100f;       // 히트박스 가로 폭 (노란색 영역 크기)

    [Header("시각 피드백 색상")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color successColor = Color.green;
    [SerializeField] private Color failColor = Color.red;

    private PlayerController _owner;
    private InputHandler _inputHandler;

    private float _sessionTimer = 0f;

    // [신규 판정 시스템] 각 피크(심박)별 타격 성공 여부 기록
    private bool[] _beatHitStatus = new bool[5];
    private int _lastPassedBeatIndex = -1; // 지나쳐버린 마지막 비트 인덱스
    private int _cycleSuccessCount = 0;    // 이번 사이클에서 성공한 횟수

    // 🛠️ [신규 개조] 유저가 마지막으로 스위치를 누른 '구역(Interval)'을 기억하는 메모리 칩
    private int _lastAttemptedIntervalIndex = -1;

    private bool _isActive = false;

    public void OpenUI(PlayerController owner)
    {
        _owner = owner;
        _isActive = true;
        gameObject.SetActive(true);

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        if (_owner != null)
        {
            _inputHandler = _owner.GetComponent<InputHandler>();
            // 배선 체크용 경고등
            if (_inputHandler == null) Debug.LogWarning("[MinigameUI] InputHandler 부품을 찾을 수 없습니다!");
        }

        ResetVisuals();
        ResetSession();
    }

    private void ResetSession()
    {
        _sessionTimer = 0f;
        _cycleSuccessCount = 0;
        _lastPassedBeatIndex = -1;

        // 🛠️ 추가: 사이클이 새로 돌 때 시도권 기록도 초기화!
        _lastAttemptedIntervalIndex = -1;

        // 타격 기록 초기화
        for (int i = 0; i < totalBeats; i++)
        {
            _beatHitStatus[i] = false;
        }

        UpdateScannerPosition();
    }

    private void Update()
    {
        if (!_isActive) return;

        // 이중 입력 회로: InputHandler를 거치거나, 다이렉트로 스페이스바를 누르거나 둘 다 허용!
        bool isSpacePressed = (_inputHandler != null && _inputHandler.ConsumeMinigameInput())
                           || Keyboard.current.spaceKey.wasPressedThisFrame;

        if (isSpacePressed)
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
        if (graphContainer == null) return;

        float widthPerBeat = graphContainer.rect.width / totalBeats;
        float currentScannerX = scannerBar.anchoredPosition.x;

        // 1. 클릭하지 않고 지나쳐버린 비트(Peak)가 있는지 검사 (무위험 실패 처리)
        int checkingBeat = _lastPassedBeatIndex + 1;
        if (checkingBeat < totalBeats)
        {
            float targetCenterX = (checkingBeat * widthPerBeat) + (widthPerBeat / 2f);
            float hitBoxEnd = targetCenterX + (hitBoxWidth / 2f);

            // 스캐너가 판정 구역을 완전히 벗어났다면
            if (currentScannerX > hitBoxEnd)
            {
                // 근데 타격 기록이 없다면? -> 놓침!
                if (!_beatHitStatus[checkingBeat])
                {
                    TriggerVisualFeedback(failColor);
                }
                _lastPassedBeatIndex = checkingBeat; // 다음 비트로 검사 대상 이동
            }
        }

        // 2. 1사이클(5.0초)이 끝난 경우의 결산
        if (_sessionTimer >= totalDuration)
        {
            if (_cycleSuccessCount > 0 && _owner != null && _owner.Object.HasInputAuthority)
            {
                _owner.RPC_ProcessMinigameSuccess(_cycleSuccessCount);
            }
            // 무한 반복
            ResetSession();
        }
    }

    public void OnClickInput()
    {
        if (!_isActive || graphContainer == null) return;

        // 🛠️ [신규 개조] 1구역 당 1번의 시도권만 부여 (연타 방지 시스템)
        float timePerBeat = totalDuration / totalBeats;
        int currentIntervalIndex = Mathf.FloorToInt(_sessionTimer / timePerBeat);

        // 이번 구역에서 이미 버튼을 누른 적이 있다면? (연타 적발)
        if (currentIntervalIndex == _lastAttemptedIntervalIndex)
        {
            TriggerVisualFeedback(failColor); // 즉시 실패 판정 피드백
            return;                           // 아래 거리 계산 로직을 타지 않고 전원 차단
        }

        // 이번 구역의 1회 시도권 소모 기록
        _lastAttemptedIntervalIndex = currentIntervalIndex;

        // -------------------------------------------------------------

        float widthPerBeat = graphContainer.rect.width / totalBeats;
        float currentScannerX = scannerBar.anchoredPosition.x;

        int closestBeatIndex = -1;
        float minDistance = float.MaxValue;

        // 1. 현재 스캐너 위치에서 가장 가까운 심박 피크(목표) 찾기
        for (int i = 0; i < totalBeats; i++)
        {
            float targetCenterX = (i * widthPerBeat) + (widthPerBeat / 2f);
            float dist = Mathf.Abs(currentScannerX - targetCenterX);

            if (dist < minDistance)
            {
                minDistance = dist;
                closestBeatIndex = i;
            }
        }

        // 2. 타격 판정 진행
        if (closestBeatIndex != -1)
        {
            float targetCenterX = (closestBeatIndex * widthPerBeat) + (widthPerBeat / 2f);
            float hitBoxStart = targetCenterX - (hitBoxWidth / 2f);
            float hitBoxEnd = targetCenterX + (hitBoxWidth / 2f);

            // 스캐너가 판정 범위 안에 있을 때! (이미지의 노란색 범위)
            if (currentScannerX >= hitBoxStart && currentScannerX <= hitBoxEnd)
            {
                // 아직 맞추지 않은 피크라면 성공!
                if (!_beatHitStatus[closestBeatIndex])
                {
                    _beatHitStatus[closestBeatIndex] = true; // 타격 성공 도장 쾅!
                    _cycleSuccessCount++;
                    TriggerVisualFeedback(successColor);
                }
                else
                {
                    // 이미 맞춘 피크인데 또 누른 경우 (연타 패널티)
                    TriggerVisualFeedback(failColor);
                }
            }
            else
            {
                // 노란색 범위 밖, 허공(흰색 평면)에서 눌렀을 경우 (실패)
                TriggerVisualFeedback(failColor);
            }
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