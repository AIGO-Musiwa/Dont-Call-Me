using Fusion;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 1단계 전선 연결 퍼즐 본체
/// - 좌측/우측 색 배치
/// - 좌우 선택
/// - 좌우 연결
/// - 연결 해제
/// - 확인 버튼 판정
/// - 현재 연결 상태를 다른 플레이어에게 네트워크로 공유
/// </summary>
public class WireConnectionPuzzle : PuzzleInteractableBase, IPuzzleSeedReceiver
{
    [Header("설정")]
    [SerializeField] private int socketCount = 6;
    [SerializeField] private float maxInteractDistance = 10f;

    [Header("뷰")]
    [SerializeField] private WireConnectionView wireView;
    [SerializeField] private Transform puzzleCenter;

    [Header("디버그")]
    [SerializeField] private bool enableDebugLog = true;

    private readonly List<WireSocketColor> _leftColors = new();
    private readonly List<WireSocketColor> _rightColors = new();
    private readonly List<int> _correctRightIndexByLeft = new();
    private bool _hasAnswerSeed;

    [Networked, OnChangedRender(nameof(OnSelectionChanged))]
    private NetworkBool NetSelectedLeftActive { get; set; }

    [Networked, OnChangedRender(nameof(OnSelectionChanged))]
    private int NetSelectedLeftIndex { get; set; }

    [Networked]
    private NetworkId NetSelectingPlayerObjectId { get; set; }


    [Networked, OnChangedRender(nameof(OnConnectionsChanged))] private int NetConnectedRight0 { get; set; }
    [Networked, OnChangedRender(nameof(OnConnectionsChanged))] private int NetConnectedRight1 { get; set; }
    [Networked, OnChangedRender(nameof(OnConnectionsChanged))] private int NetConnectedRight2 { get; set; }
    [Networked, OnChangedRender(nameof(OnConnectionsChanged))] private int NetConnectedRight3 { get; set; }
    [Networked, OnChangedRender(nameof(OnConnectionsChanged))] private int NetConnectedRight4 { get; set; }
    [Networked, OnChangedRender(nameof(OnConnectionsChanged))] private int NetConnectedRight5 { get; set; }

    private readonly List<int> _cachedConnections = new();

    private void Awake()
    {
        EnsureCachedConnectionSize();
        ResetViewToDefault();
    }

    public override void Spawned()
    {
        SyncCachedConnectionsFromNetwork();
        RefreshAllViews();
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
        }

        RefreshAllViews();
        Log("전선 연결 퍼즐 시드 적용 완료");
        LogAnswerDebug();
    }

    /// <summary>
    /// 좌측 소켓 입력 처리
    /// </summary>
    public void OnLeftSocketPressed(int leftIndex, PlayerController actor)
    {
        if (!HasStateAuthority)
            return;

        if (IsSolved)
            return;

        if (!_hasAnswerSeed)
            return;

        if (!IsValidSocketIndex(leftIndex))
            return;

        // 같은 좌측 다시 누르면 선택 해제
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
    /// 우측 소켓 입력 처리
    /// </summary>
    public void OnRightSocketPressed(int rightIndex, PlayerController actor)
    {
        if (!HasStateAuthority)
            return;

        if (IsSolved)
            return;

        if (!_hasAnswerSeed)
            return;

        if (!NetSelectedLeftActive)
            return;

        if (!IsValidSocketIndex(rightIndex))
            return;

        // 이미 다른 left가 이 right를 쓰고 있으면 무시
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
    /// 연결선 클릭 시 해당 left의 연결 해제
    /// </summary>
    public void OnConnectionLinePressed(int leftIndex, PlayerController actor)
    {
        if (!HasStateAuthority)
            return;

        if (IsSolved)
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
    /// 확인 버튼 판정
    /// </summary>
    public void OnJudgePressed(PlayerController actor)
    {
        if (!HasStateAuthority)
            return;

        if (IsSolved)
            return;

        if (!_hasAnswerSeed)
            return;

        for (int leftIndex = 0; leftIndex < socketCount; leftIndex++)
        {
            int currentRight = GetConnectedRightByLeft(leftIndex);
            int correctRight = _correctRightIndexByLeft[leftIndex];

            if (currentRight != correctRight)
            {
                MarkFailed();
                Log($"판정 실패 | left={leftIndex} | current={currentRight} | correct={correctRight}");
                return;
            }
        }

        MarkSolved();
        ClearCurrentSelection();
        Log("전선 연결 퍼즐 성공");
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
        //
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
