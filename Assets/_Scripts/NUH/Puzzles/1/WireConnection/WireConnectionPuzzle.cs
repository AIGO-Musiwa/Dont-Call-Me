using Fusion;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 1단계 전선 연결 퍼즐 본체.
/// - 좌측/우측 색 배치
/// - 좌우 선택
/// - 좌우 연결
/// - 연결 해제
/// - Confirm 레버 판정
/// - 실패 시 깜빡임이 끝난 뒤 초기화
/// - 성공 시 Confirm 표시를 초록색으로 유지
/// </summary>
public class WireConnectionPuzzle : PuzzleInteractableBase, IPuzzleSeedReceiver
{
    [Header("설정")]
    [SerializeField] private int socketCount = 6; // 좌우 소켓 개수
    [SerializeField] private float maxInteractDistance = 10f; // 선택 유지 최대 거리

    [Header("뷰")]
    [SerializeField] private WireConnectionView wireView; // 전선 퍼즐 View
    [SerializeField] private Transform puzzleCenter; // 거리 체크 기준점

    [Header("디버그")]
    [SerializeField] private bool enableDebugLog = true; // 디버그 로그 여부

    private readonly List<WireSocketColor> _leftColors = new(); // 좌측 색상 목록
    private readonly List<WireSocketColor> _rightColors = new(); // 우측 색상 목록
    private readonly List<int> _correctRightIndexByLeft = new(); // 정답 매핑
    private bool _hasAnswerSeed; // 시드 적용 여부

    [Networked, OnChangedRender(nameof(OnSelectionChanged))]
    private NetworkBool NetSelectedLeftActive { get; set; } // 좌측 선택 여부

    [Networked, OnChangedRender(nameof(OnSelectionChanged))]
    private int NetSelectedLeftIndex { get; set; } // 선택된 좌측 인덱스

    [Networked]
    private NetworkId NetSelectingPlayerObjectId { get; set; } // 선택 중인 플레이어 NetworkId

    [Networked, OnChangedRender(nameof(OnConnectionsChanged))] private int NetConnectedRight0 { get; set; }
    [Networked, OnChangedRender(nameof(OnConnectionsChanged))] private int NetConnectedRight1 { get; set; }
    [Networked, OnChangedRender(nameof(OnConnectionsChanged))] private int NetConnectedRight2 { get; set; }
    [Networked, OnChangedRender(nameof(OnConnectionsChanged))] private int NetConnectedRight3 { get; set; }
    [Networked, OnChangedRender(nameof(OnConnectionsChanged))] private int NetConnectedRight4 { get; set; }
    [Networked, OnChangedRender(nameof(OnConnectionsChanged))] private int NetConnectedRight5 { get; set; }

    [Networked, OnChangedRender(nameof(OnJudgePressTriggered))]
    private int NetJudgePressSerial { get; set; } // Confirm 레버 입력 이벤트

    [Networked, OnChangedRender(nameof(OnJudgeFailedEffectTriggered))]
    private int NetJudgeFailedEffectSerial { get; set; } // 실패 깜빡임 이벤트

    [Networked, OnChangedRender(nameof(OnJudgeSolvedVisualChanged))]
    private NetworkBool NetJudgeSolvedVisual { get; set; } // 성공 표시 유지 여부

    [Networked]
    private NetworkBool NetJudgeInputLocked { get; set; } // 판정 연출 중 입력 잠금

    private readonly List<int> _cachedConnections = new(); // 네트워크 연결 상태 캐시

    private int _lastHandledJudgePressSerial = -1; // Confirm 이벤트 중복 처리 방지
    private int _lastHandledJudgeFailSerial = -1; // 실패 이벤트 중복 처리 방지
    private Coroutine _judgeFailResetRoutine; // 실패 후 초기화 코루틴

    /// <summary>
    /// Confirm 판정 입력이 잠겨 있는지 반환한다.
    /// Interactable에서 입력 차단할 때 사용한다.
    /// </summary>
    public bool IsJudgeInputLocked => NetJudgeInputLocked;

    private void Awake()
    {
        EnsureCachedConnectionSize();
        ResetViewToDefault();
    }

    public override void Spawned()
    {
        base.Spawned();

        SyncCachedConnectionsFromNetwork();
        RefreshAllViews();
        ApplyJudgeVisualStateImmediate();
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority)
            return;

        if (!NetSelectedLeftActive)
            return;

        PlayerController selectingPlayer = FindPlayerByObjectId(NetSelectingPlayerObjectId);
        if (selectingPlayer == null || puzzleCenter == null)
        {
            ClearCurrentSelection();
            return;
        }

        Vector3 delta = selectingPlayer.transform.position - puzzleCenter.position;
        float sqrDistance = delta.sqrMagnitude;
        float maxDistanceSqr = maxInteractDistance * maxInteractDistance;

        if (sqrDistance > maxDistanceSqr)
        {
            Log($"거리 이탈로 선택 해제 | sqrDistance={sqrDistance}");
            ClearCurrentSelection();
        }
    }

    /// <summary>
    /// 정답 시드를 적용하고 퍼즐 상태를 초기화한다.
    /// </summary>
    public void ApplyAnswerSeed(int seed)
    {
        _leftColors.Clear();
        _rightColors.Clear();
        _correctRightIndexByLeft.Clear();

        WireConnectionAnswerGenerator.Result result = WireConnectionAnswerGenerator.Generate(seed, socketCount);
        _leftColors.AddRange(result.LeftColors);
        _rightColors.AddRange(result.RightColors);
        _correctRightIndexByLeft.AddRange(result.CorrectRightIndexByLeft);

        _hasAnswerSeed = true;

        if (HasStateAuthority)
        {
            ClearCurrentSelection();
            ClearAllConnections();

            NetJudgePressSerial = 0;
            NetJudgeFailedEffectSerial = 0;
            NetJudgeSolvedVisual = false;
            NetJudgeInputLocked = false;
        }

        RefreshAllViews();
        ApplyJudgeNormalView();

        Log("전선 연결 퍼즐 시드 적용 완료");
        LogAnswerDebug();
    }

    /// <summary>
    /// 좌측 소켓 입력 처리.
    /// </summary>
    public void OnLeftSocketPressed(int leftIndex, PlayerController actor)
    {
        if (!HasStateAuthority)
            return;

        if (IsSolved)
            return;

        if (NetJudgeInputLocked)
            return;

        if (!_hasAnswerSeed)
            return;

        if (!IsValidSocketIndex(leftIndex))
            return;

        if (NetSelectedLeftActive && NetSelectedLeftIndex == leftIndex)
        {
            ClearCurrentSelection();
            return;
        }

        NetSelectedLeftActive = true;
        NetSelectedLeftIndex = leftIndex;
        NetSelectingPlayerObjectId = actor != null && actor.Object != null
            ? actor.Object.Id
            : default;

        RefreshSelectionView();
        Log($"좌측 선택 | left = {leftIndex}");
    }

    /// <summary>
    /// 우측 소켓 입력 처리.
    /// </summary>
    public void OnRightSocketPressed(int rightIndex, PlayerController actor)
    {
        if (!HasStateAuthority)
            return;

        if (IsSolved)
            return;

        if (NetJudgeInputLocked)
            return;

        if (!_hasAnswerSeed)
            return;

        if (!NetSelectedLeftActive)
            return;

        if (!IsValidSocketIndex(rightIndex))
            return;

        if (IsRightAlreadyConnected(rightIndex))
        {
            Log($"이미 연결된 우측 입력 무시 | right = {rightIndex}");
            return;
        }

        int selectedLeft = NetSelectedLeftIndex;
        SetConnectedRightByLeft(selectedLeft, rightIndex);
        SyncCachedConnectionsFromNetwork();
        RefreshConnectionsView();

        ClearCurrentSelection();
        Log($"연결 생성 | left = {selectedLeft} -> right = {rightIndex}");
    }

    /// <summary>
    /// 연결선 클릭 시 해당 left의 연결을 해제한다.
    /// </summary>
    public void OnConnectionLinePressed(int leftIndex, PlayerController actor)
    {
        if (!HasStateAuthority)
            return;

        if (IsSolved)
            return;

        if (NetJudgeInputLocked)
            return;

        if (!_hasAnswerSeed)
            return;

        if (!IsValidSocketIndex(leftIndex))
            return;

        if (GetConnectedRightByLeft(leftIndex) < 0)
            return;

        SetConnectedRightByLeft(leftIndex, -1);
        SyncCachedConnectionsFromNetwork();
        RefreshConnectionsView();

        Log($"연결 해제 | left = {leftIndex}");
    }

    /// <summary>
    /// Confirm 레버 판정 처리.
    /// 실패하면 깜빡임이 끝날 때까지 초기화를 미룬다.
    /// </summary>
    public void OnJudgePressed(PlayerController actor)
    {
        if (!HasStateAuthority)
            return;

        if (IsSolved)
            return;

        if (NetJudgeInputLocked)
            return;

        if (!_hasAnswerSeed)
            return;

        NetJudgePressSerial++;
        HandleJudgePressVisual(NetJudgePressSerial);

        for (int leftIndex = 0; leftIndex < socketCount; leftIndex++)
        {
            int currentRight = GetConnectedRightByLeft(leftIndex);
            int correctRight = _correctRightIndexByLeft[leftIndex];

            if (currentRight != correctRight)
            {
                MarkFailed();
                StartJudgeFailSequence();

                Log($"판정 실패 | left={leftIndex} | current={currentRight} | correct={correctRight}");
                return;
            }
        }

        MarkSolved();
        ClearCurrentSelection();

        NetJudgeSolvedVisual = true;
        ApplyJudgeSolvedView();

        Log("전선 연결 퍼즐 성공");
    }

    /// <summary>
    /// 실패 판정 연출을 시작한다.
    /// </summary>
    private void StartJudgeFailSequence()
    {
        if (!HasStateAuthority)
            return;

        NetJudgeInputLocked = true;
        NetJudgeFailedEffectSerial++;

        HandleJudgeFailedVisual(NetJudgeFailedEffectSerial);

        if (_judgeFailResetRoutine != null)
            StopCoroutine(_judgeFailResetRoutine);

        _judgeFailResetRoutine = StartCoroutine(CoResetAfterJudgeFail());
    }

    /// <summary>
    /// 실패 깜빡임이 끝난 뒤 연결 상태를 초기화한다.
    /// Confirm 레버도 다시 위 상태로 복귀시켜 다음 판정을 받을 수 있게 한다.
    /// </summary>
    private IEnumerator CoResetAfterJudgeFail()
    {
        float waitSeconds = wireView != null ? wireView.JudgeFailEffectSeconds : 0.9f;
        yield return new WaitForSeconds(waitSeconds);

        ClearCurrentSelection();   // 선택 상태 초기화
        ClearAllConnections();     // 연결 상태 초기화

        NetJudgeInputLocked = false; // 판정 입력 잠금 해제

        if (!IsSolved)
        {
            NetJudgeSolvedVisual = false;
            ApplyJudgeNormalView(); // Confirm Emission 흰색 복귀

            // 실패 후 Confirm 레버를 다시 올려 다음 입력을 받을 수 있게 한다.
            if (wireView != null)
                wireView.ResetJudgeLever();
        }

        _judgeFailResetRoutine = null;

        Log("전선 연결 퍼즐 실패 후 초기화");
    }

    private void OnSelectionChanged()
    {
        RefreshSelectionView();
    }

    private void OnConnectionsChanged()
    {
        SyncCachedConnectionsFromNetwork();
        RefreshConnectionsView();
    }

    /// <summary>
    /// Confirm 레버 입력 이벤트가 들어오면 모든 클라이언트에서 레버 애니메이션을 재생한다.
    /// </summary>
    private void OnJudgePressTriggered()
    {
        if (NetJudgePressSerial <= 0)
            return;

        HandleJudgePressVisual(NetJudgePressSerial);
    }

    /// <summary>
    /// 실패 깜빡임 이벤트가 들어오면 모든 클라이언트에서 실패 연출을 재생한다.
    /// </summary>
    private void OnJudgeFailedEffectTriggered()
    {
        if (NetJudgeFailedEffectSerial <= 0)
            return;

        HandleJudgeFailedVisual(NetJudgeFailedEffectSerial);
    }

    /// <summary>
    /// 성공 시각 상태가 바뀌면 Confirm 표시를 갱신한다.
    /// </summary>
    private void OnJudgeSolvedVisualChanged()
    {
        if (NetJudgeSolvedVisual)
            ApplyJudgeSolvedView();
        else
            ApplyJudgeNormalView();
    }

    /// <summary>
    /// Confirm 레버 당김 연출 중복 처리를 막고 실행한다.
    /// </summary>
    private void HandleJudgePressVisual(int serial)
    {
        if (serial <= 0)
            return;

        if (_lastHandledJudgePressSerial == serial)
            return;

        _lastHandledJudgePressSerial = serial;

        if (wireView != null)
            wireView.PlayJudgeLeverPulled();
    }

    /// <summary>
    /// Confirm 실패 연출 중복 처리를 막고 실행한다.
    /// </summary>
    private void HandleJudgeFailedVisual(int serial)
    {
        if (serial <= 0)
            return;

        if (_lastHandledJudgeFailSerial == serial)
            return;

        _lastHandledJudgeFailSerial = serial;

        if (wireView != null)
            wireView.PlayJudgeFailBlink();
    }

    private void RefreshAllViews()
    {
        if (wireView == null)
            return;

        wireView.ApplySocketColors(_leftColors, _rightColors);
        RefreshSelectionView();
        SyncCachedConnectionsFromNetwork();
        RefreshConnectionsView();
    }

    private void RefreshSelectionView()
    {
        if (wireView == null)
            return;

        wireView.ApplySelection(NetSelectedLeftIndex, NetSelectedLeftActive);
    }

    private void RefreshConnectionsView()
    {
        if (wireView == null)
            return;

        wireView.ApplyConnections(_cachedConnections);
    }

    private void ResetViewToDefault()
    {
        if (wireView == null)
            return;

        wireView.ClearAllSelections();
        wireView.HideAllLines();
        wireView.SetJudgeNormalImmediate();
        wireView.ResetJudgeLeverImmediate();
    }

    private void ApplyJudgeVisualStateImmediate()
    {
        if (wireView == null)
            return;

        if (NetJudgeSolvedVisual)
        {
            wireView.SetJudgeSolved();
            wireView.SetJudgeLeverPulledImmediate();
        }
        else
        {
            wireView.SetJudgeNormalImmediate();
            wireView.ResetJudgeLeverImmediate();
        }
    }

    private void ApplyJudgeNormalView()
    {
        if (wireView != null)
            wireView.SetJudgeNormal();
    }

    private void ApplyJudgeSolvedView()
    {
        if (wireView != null)
            wireView.SetJudgeSolved();
    }

    private void ClearCurrentSelection()
    {
        NetSelectedLeftActive = false;
        NetSelectedLeftIndex = -1;
        NetSelectingPlayerObjectId = default;
        RefreshSelectionView();
    }

    private void ClearAllConnections()
    {
        NetConnectedRight0 = -1;
        NetConnectedRight1 = -1;
        NetConnectedRight2 = -1;
        NetConnectedRight3 = -1;
        NetConnectedRight4 = -1;
        NetConnectedRight5 = -1;

        SyncCachedConnectionsFromNetwork();
        RefreshConnectionsView();
    }

    private bool IsRightAlreadyConnected(int rightIndex)
    {
        for (int leftIndex = 0; leftIndex < socketCount; leftIndex++)
        {
            if (GetConnectedRightByLeft(leftIndex) == rightIndex)
                return true;
        }

        return false;
    }

    private int GetConnectedRightByLeft(int leftIndex)
    {
        return leftIndex switch
        {
            0 => NetConnectedRight0,
            1 => NetConnectedRight1,
            2 => NetConnectedRight2,
            3 => NetConnectedRight3,
            4 => NetConnectedRight4,
            5 => NetConnectedRight5,
            _ => -1
        };
    }

    private void SetConnectedRightByLeft(int leftIndex, int rightIndex)
    {
        switch (leftIndex)
        {
            case 0: NetConnectedRight0 = rightIndex; break;
            case 1: NetConnectedRight1 = rightIndex; break;
            case 2: NetConnectedRight2 = rightIndex; break;
            case 3: NetConnectedRight3 = rightIndex; break;
            case 4: NetConnectedRight4 = rightIndex; break;
            case 5: NetConnectedRight5 = rightIndex; break;
        }
    }

    private void SyncCachedConnectionsFromNetwork()
    {
        EnsureCachedConnectionSize();

        for (int i = 0; i < socketCount; i++)
            _cachedConnections[i] = GetConnectedRightByLeft(i);
    }

    private void EnsureCachedConnectionSize()
    {
        while (_cachedConnections.Count < socketCount)
            _cachedConnections.Add(-1);

        while (_cachedConnections.Count > socketCount)
            _cachedConnections.RemoveAt(_cachedConnections.Count - 1);
    }

    private bool IsValidSocketIndex(int index)
    {
        return index >= 0 && index < socketCount;
    }

    private PlayerController FindPlayerByObjectId(NetworkId objectId)
    {
        if (objectId == default || Runner == null)
            return null;

        if (!Runner.TryFindObject(objectId, out NetworkObject playerObject))
            return null;

        if (playerObject == null)
            return null;

        return playerObject.GetComponent<PlayerController>();
    }

    protected override void ServerInteract(PlayerController actor)
    {
        // 루트 직접 상호작용 없음.
    }

    private void LogAnswerDebug()
    {
        if (!enableDebugLog)
            return;

        string leftColors = string.Join(", ", _leftColors);
        string rightColors = string.Join(", ", _rightColors);
        string answers = string.Join(", ", _correctRightIndexByLeft);

        Debug.Log($"[WireConnectionPuzzle] LeftColors = [{leftColors}]", this);
        Debug.Log($"[WireConnectionPuzzle] RightColors = [{rightColors}]", this);
        Debug.Log($"[WireConnectionPuzzle] CorrectRightIndexByLeft = [{answers}]", this);
    }

    private void Log(string message)
    {
        if (!enableDebugLog)
            return;

        Debug.Log($"[WireConnectionPuzzle] {message}", this);
    }
}