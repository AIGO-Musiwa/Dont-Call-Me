using Fusion;
using UnityEngine;

public class EscapeInteractable : NetworkBehaviour, IInteractable
{
    [Header("탈출 상호작용 설정")]
    public EscapeInteractType interactType;
    public Zone myZone;

    [Header("시각 연출 (버튼용)")]
    [Tooltip("버튼이 노출되었을 때 켤 불빛이나 머티리얼 오브젝트")]
    public GameObject buttonActiveVisual;

    [Header("문 개방 설정 (탈출구 전용)")]
    [Tooltip("문이 열릴 때 회전할 목표 각도 (예: Y축 90도)")]
    public Vector3 openRotation = new Vector3(0, 90, 0);
    [Tooltip("문이 열리는 속도")]
    public float openSpeed = 5f;

    [Networked] public NetworkBool IsOpen { get; set; }

    private Quaternion closedRotation;
    private Quaternion targetOpenRotation;

    public override void Spawned()
    {
        //문 타입일 경우, 시작 시 회전값과 목표 열림 회전값을 저장
        if (interactType == EscapeInteractType.FrontDoor || interactType == EscapeInteractType.RooftopDoor)
        {
            closedRotation = transform.localRotation;
            targetOpenRotation = closedRotation * Quaternion.Euler(openRotation);
        }
    }

    public override void Render()
    {
        //버튼 타입 연출
        if (interactType == EscapeInteractType.EscapeButton && buttonActiveVisual != null)
        {
            bool isExposed = StageManager.Instance != null &&
                             ((myZone == Zone.ZoneA && StageManager.Instance.IsZoneAEscapeButtonExposed ||
                             myZone == Zone.ZoneB && StageManager.Instance.IsZoneBEscapeButtonExposed));

            if (buttonActiveVisual.activeSelf != isExposed) buttonActiveVisual.SetActive(isExposed);
        }

        //문 타입 연출
        if (interactType == EscapeInteractType.FrontDoor || interactType == EscapeInteractType.RooftopDoor)
        {
            if (Object != null && Object.IsValid)
            {
                Quaternion targetRotation = IsOpen ? targetOpenRotation : closedRotation;
                transform.localRotation = Quaternion.Slerp(transform.localRotation, targetRotation, Time.deltaTime * openSpeed);
            }
        }
    }

    public bool CanInteract(PlayerController actor)
    {
        //이미 열린 문은 상호작용 불가능
        if (IsOpen) return false;
        return true;
    }

    //플레이어가 상호작용 키를 눌렀을 때 PlayerController에서 호출
    public void Interact(PlayerController actor)
    {
        if (StageManager.Instance == null) return;

        switch (interactType)
        {
            case EscapeInteractType.EscapeButton:
                HandleEscapeButtonInteract();
                break;

            case EscapeInteractType.FrontDoor:
                HandleFrontDoorInteract(actor);
                break;

            case EscapeInteractType.RooftopDoor:
                HandleRooftopDoorInteract(actor);
                break;
        }
    }

    private void HandleEscapeButtonInteract()
    {
        bool isMyZoneExposed = (myZone == Zone.ZoneA && StageManager.Instance.IsZoneAEscapeButtonExposed) ||
                               (myZone == Zone.ZoneB && StageManager.Instance.IsZoneBEscapeButtonExposed);

        //3단계 퍼즐이 모두 풀려 버튼이 노출된 상태에서만 작동
        if (StageManager.Instance.IsZoneAEscapeButtonExposed || StageManager.Instance.IsZoneBEscapeButtonExposed)
        {
            StageManager.Instance.TryPressEscapeButton(myZone);
            Debug.Log($"[{myZone}] 탈출 버튼 입력 완료! 반대편 입력을 기다립니다.");
        }

        else Debug.LogWarning("아직 3단계 퍼즐이 완료되지 않아 버튼을 누를 수 없습니다.");
    }

    private void HandleFrontDoorInteract(PlayerController player)
    {
        // 아직은 호스트만 통과하는 차단기 (테스트용)
        if (!HasStateAuthority) return;

        // 1. 3막 체크
        if (!StageManager.Instance.IsAct3Active)
        {
            // 호스트의 HUD에 출력
            player.HUD.ShowAlert("정문 잠김: 아직 탈출 페이즈가 아닙니다.");
            return;
        }

        // 2. 키 체크
        ItemType heldItem = GetPlayerRightHandItemType(player);
        if (heldItem == ItemType.FrontDoorKey || heldItem == ItemType.MasterKey)
        {
            IsOpen = true;
        }
        else
        {
            // 호스트의 HUD에 출력
            player.HUD.ShowAlert("정문 잠김: 전용 키가 필요합니다.");
        }
    }

    private void HandleRooftopDoorInteract(PlayerController player)
    {
        if (!HasStateAuthority) return;

        //3막 발동 체크
        if (!StageManager.Instance.IsAct3Active)
        {
            Debug.LogWarning("옥상 잠김: 아직 3막(탈출 페이즈)이 시작되지 않았습니다.");
            return;
        }

        //옥상 문은 상호작용으로 열리지 않음
        if (IsOpen) return;

        //플레이어의 오른손 아이템 타입 추출
        ItemType heldItem = GetPlayerRightHandItemType(player);

        //키 종류 체크 (옥상 탈출 조건: 마스터키)
        if (heldItem == ItemType.MasterKey)
        {
            IsOpen = true;
            Debug.Log($"옥상 문이 개방되었습니다! (사용 키: {heldItem}) 밖으로 나가 최종 탈출 구역에 도달하세요!");
        }
        else
        {
            Debug.LogWarning("옥상 잠김: 옥상 탈출을 위해서는 반드시 마스터 키(MasterKey)가 필요합니다.");
        }
    }

    //플레이어의 오른손 아이템 타입을 가져오는 함수
    private ItemType GetPlayerRightHandItemType(PlayerController player)
    {
        //PlayerController에 이미 구현된 함수를 통해 ItemObject 컴포넌트 획득        
        var rightItem = player.GetRightHandItemObject();

        //아이템을 들고 있으면 해당 아이템의 타입을, 빈손이면 None을 반환
        return rightItem != null ? rightItem.NetItemType : ItemType.None;
    }

    //UI 문구
    public string GetPromptText(PlayerController actor)
    {
        if (StageManager.Instance == null) return string.Empty;

        switch (interactType)
        {
            case EscapeInteractType.EscapeButton:
                bool isMyZoneExposed = (myZone == Zone.ZoneA && StageManager.Instance.IsZoneAEscapeButtonExposed) ||
                                       (myZone == Zone.ZoneB && StageManager.Instance.IsZoneBEscapeButtonExposed);
                if (StageManager.Instance.IsZoneAEscapeButtonExposed || StageManager.Instance.IsZoneBEscapeButtonExposed) return "탈출 버튼 누르기";
                else return string.Empty;

            case EscapeInteractType.FrontDoor:
                if (StageManager.Instance.IsAct3Active) return "정문 탈출하기 (일반키/마스터키 필요)";
                else return "정문 (잠김: 버튼 동시 입력 필요)";

            case EscapeInteractType.RooftopDoor:
                if (StageManager.Instance.IsAct3Active) return "옥상으로 탈출하기 (마스터키 필요)";
                else return "옥상 (잠김: 버튼 동시 입력 필요)";
        }

        return string.Empty;
    }
}
