using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 5초간 1초 간격으로 입력을 성공해야 하는 심박계(ECG) 미니게임.
/// </summary>
public class CaptureMinigameUI : MonoBehaviour
{
    [Header("연결된 UI 부품")]
    [SerializeField] private RawImage ecgGraph;      // UV 스크롤될 그래프
    [SerializeField] private Slider traumaSlider;    // 후유증 게이지 (HUDController와 별개로 실시간 반영용)
    [SerializeField] private float scrollSpeed = 0.5f;

    [Header("시각 피드백")]
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
        ResetSession();
    }

    private void ResetSession()
    {
        _sessionTimer = 0f;
        _currentBeatIndex = 0;
        _hasClickedThisBeat = false;
        ecgGraph.color = normalColor;
        Debug.Log("[ECG] 세션 리셋 - 처음부터 다시 시작 (1/5)");
    }

    /// <summary>
    /// 투명 버튼(Input_Catcher) 클릭 시 호출
    /// </summary>
    public void OnClickInput()
    {
        if (!_isActive) return;

        // 한 비트(1초) 구간 내에서 중복 클릭은 무시하거나 피드백만 줌
        if (!_hasClickedThisBeat)
        {
            _hasClickedThisBeat = true;
            // 성공 시 그래프를 순간적으로 녹색으로 번쩍이게 함
            StopAllCoroutines();
            StartCoroutine(FlashColor(successColor));
        }
    }

    private void Update()
    {
        if (!_isActive || _owner == null) return;

        // 1. 그래프 무한 스크롤
        Rect uv = ecgGraph.uvRect;
        uv.x += scrollSpeed * Time.deltaTime;
        ecgGraph.uvRect = uv;

        // 2. 게이지 실시간 동기화
        traumaSlider.value = _owner.NetAftereffectPercent / 100f;

        // 3. 콤보 판정 로직
        _sessionTimer += Time.deltaTime;
        int checkIndex = Mathf.FloorToInt(_sessionTimer);

        // 1초 구간이 경과했을 때
        if (checkIndex > _currentBeatIndex)
        {
            // 방금 지난 1초 동안 한 번이라도 클릭했는가?
            if (!_hasClickedThisBeat)
            {
                // 실패: 1회라도 미입력 시 처음부터 다시 (기획 1-4)
                StartCoroutine(FlashColor(failColor));
                ResetSession();
                return;
            }

            // 성공: 다음 구간으로 진행
            _currentBeatIndex = checkIndex;
            _hasClickedThisBeat = false;

            // 최종 5초 도달 (5번의 클릭 콤보 달성)
            if (_currentBeatIndex >= 5)
            {
                _owner.RPC_ProcessMinigameSuccess(); // 서버에 3% 감소 요청
                ResetSession(); // 다시 다음 5초 세션 시작
            }
        }
    }

    private System.Collections.IEnumerator FlashColor(Color targetColor)
    {
        ecgGraph.color = targetColor;
        yield return new WaitForSeconds(0.1f);
        ecgGraph.color = normalColor;
    }

    public void CloseUI()
    {
        _isActive = false;
        gameObject.SetActive(false);
    }
}