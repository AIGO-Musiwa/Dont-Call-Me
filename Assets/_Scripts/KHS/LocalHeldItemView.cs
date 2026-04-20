using UnityEngine;

/// <summary>
/// 로컬 플레이어의 양손(오른손/왼손) 1인칭 뷰모델을 생성하고 관리하는 모듈.
/// </summary>
public class LocalHeldItemView : MonoBehaviour
{
    [Header("연결된 시스템")]
    [SerializeField] private PlayerController playerController;

    [Header("앵커 포인트")]
    [SerializeField] private Transform rightHandAnchor; // 오른손용 카메라 하위 앵커
    [SerializeField] private Transform leftHandAnchor;  // 왼손용 카메라 하위 앵커

    // [회로 저장소] 오른손 데이터
    private ItemObject _lastRightItem;
    private GameObject _currentRightViewModel;

    // [회로 저장소] 왼손(직업) 데이터
    private ItemObject _lastLeftItem;
    private GameObject _currentLeftViewModel;

    // 로컬 전용 레이어 이름
    private const string ViewModelLayer = "ViewModel";

    private void LateUpdate()
    {
        // 로컬 플레이어가 아니면 연산 중지
        if (playerController == null || !playerController.HasInputAuthority) return;

        // 1. 오른손(장착 아이템) 상태 체크 및 갱신
        HandleHandUpdate(
            playerController.NetRightHandItem,
            ref _lastRightItem,
            ref _currentRightViewModel,
            rightHandAnchor
        );

        // 2. 왼손(직업/역할 아이템) 상태 체크 및 갱신
        HandleHandUpdate(
            playerController.NetLeftHandItem,
            ref _lastLeftItem,
            ref _currentLeftViewModel,
            leftHandAnchor
        );

        // [추가된 회로] 3. 생성된 1인칭 뷰모델과 네트워크 본체의 실시간 상태 동기화
        SyncViewModelState(_lastLeftItem, _currentLeftViewModel);
        SyncViewModelState(_lastRightItem, _currentRightViewModel);
    }

    /// <summary>
    /// 특정 손의 네트워크 상태를 확인하고 필요 시 뷰모델을 교체하는 통합 제어 함수
    /// </summary>
    private void HandleHandUpdate(Fusion.NetworkObject networkedObj, ref ItemObject lastItem, ref GameObject currentViewModel, Transform anchor)
    {
        ItemObject currentItem = null;

        // 네트워크 오브젝트에서 아이템 컴포넌트 추출
        if (networkedObj != null)
        {
            currentItem = networkedObj.GetComponent<ItemObject>();
        }

        // 상태가 변했을 때(아이템 교체, 버리기, 최초 장착)만 가동
        if (lastItem != currentItem)
        {
            UpdateHandViewModel(currentItem, ref currentViewModel, anchor);
            lastItem = currentItem;
        }
    }

    private void UpdateHandViewModel(ItemObject newItem, ref GameObject currentViewModel, Transform anchor)
    {
        // 1. 기존의 노후된 뷰모델 제거
        if (currentViewModel != null)
        {
            Destroy(currentViewModel);
            currentViewModel = null;
        }

        // 2. 새 아이템이 없거나 프리팹 누락 시 가동 중단
        if (newItem == null || newItem.LocalViewPrefab == null || anchor == null) return;

        // 3. 뷰모델 프리팹 소환
        currentViewModel = Instantiate(newItem.LocalViewPrefab, anchor);

        // 4. 위치/회전값 튜닝 (ItemObject의 오프셋 적용)
        currentViewModel.transform.localPosition = Vector3.zero;
        currentViewModel.transform.localRotation = Quaternion.Euler(newItem.ViewRotationOffset);

        // 5. 모든 부속 부품을 'ViewModel' 레이어로 변경하여 오버레이 카메라에만 노출
        SetLayerRecursively(currentViewModel, LayerMask.NameToLayer(ViewModelLayer));
    }

    /// <summary>
    /// [추가된 모듈] 네트워크 아이템 본체의 상태를 1인칭 뷰모델(복제품)에 실시간으로 복사한다.
    /// </summary>
    private void SyncViewModelState(ItemObject networkedItem, GameObject viewModel)
    {
        if (networkedItem == null || viewModel == null) return;

        // 무전기(WalkieTalkieItem)일 경우 LED 상태 동기화
        if (networkedItem.TryGetComponent(out WalkieTalkieItem walkieNet))
        {
            WalkieMeterialController viewMaterial = viewModel.GetComponentInChildren<WalkieMeterialController>();

            if (viewMaterial != null)
            {
                // 기공사 참고: WalkieTalkieItem 스크립트 안에 있는 무전기 상태 변수 이름(NetWalkieState)에 맞게 연결해 줘!
                viewMaterial.SetWalkieState(walkieNet.NetWalkieState);
            }
        }
    }

    private void SetLayerRecursively(GameObject obj, int newLayer)
    {
        if (obj == null) return;
        obj.layer = newLayer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, newLayer);
        }
    }
}