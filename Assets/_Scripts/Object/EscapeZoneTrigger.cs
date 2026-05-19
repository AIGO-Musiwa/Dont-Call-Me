using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(BoxCollider))]
public class EscapeZoneTrigger : MonoBehaviour
{
    [Header("탈출 루트 설정")]
    [Tooltip("이 트리거가 어디서 탈출 연출을 사용할지 선택")]
    public EscapeRoute escapeRoute;

    [Header("헬기 연출 설정")]
    public GameObject helicopterObject;     //헬리 네트워크 오브젝트
    public Transform heliBoardingPoint;     //헬리 탑승 위치
    public Transform heliStartPoint;        //헬리 처음 등장 위치
    public Transform heliLandingPoint;      //헬리 착륙 위치
    public AudioSource heliSound;           //헬리 이륙 사운드

    [Header("정문 연출 전용 (자동차/도보)")]
    public GameObject escapeCarObject;      //자동차 오브젝트
    public Transform carBoardingPoint;      //자동차 탑승 위치
    public AudioSource carSound;            //차 시동 소리 또는 발소리

    [Header("탈출 시스템 설정")]
    [Tooltip("최초 도달 후 남은 플레이어들에게 주어지는 탈출 제한 시간")]
    public float escapeTimeLimit = 60.0f;

    private HashSet<PlayerController> escapingPlayers = new HashSet<PlayerController>();

    //중복 연출 방지용 변수
    private bool isVehicleCalled = false;

    private void Start()
    {
        //초기 상태 비활성화
        if (helicopterObject != null) helicopterObject.SetActive(false);
        if (escapeCarObject != null) escapeCarObject.SetActive(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        PlayerController player = other.GetComponentInParent<PlayerController>();

        //상태 권한이 있는 서버 측의 트리거 판정에서만 탈출 요청을 보냄
        if (player != null && player.Object != null)
        {
            //이미 탈출 진행 중인 플레이어면 무시
            if (escapingPlayers.Contains(player)) return;

            //아직 살아있거나 납치 중인 상태의 플레이어만 탈출 가능 (이미 탈출했거나 죽은 경우 무시)
            if (player.NetPlayerState == PlayerState.Normal || player.NetPlayerState == PlayerState.Captured)
            {
                escapingPlayers.Add(player);

                if (player.Object.HasStateAuthority)
                {
                    //최초 1회만 탈출 수단 호출 및 60초 타이머 시작
                    if (!isVehicleCalled)
                    {
                        isVehicleCalled = true;
                        StartCoroutine(CallVehicleRoutine());
                        StartCoroutine(EscapeCountdownRoutine());
                    }

                    //즉시 이동 및 시야 회전 정지
                    player.SetInputLock(true, true);
                    StartCoroutine(PlayerBoardingRoutine(player));
                }
            }
        }
    }

    private IEnumerator CallVehicleRoutine()
    {
        if (escapeRoute == EscapeRoute.Rooftop)
        {
            //옥상 핼기 연출
            if (heliSound != null) heliSound.Play();

            //헬기 오브젝트 활성화
            if (helicopterObject != null && heliStartPoint != null && heliLandingPoint != null)
            {
                helicopterObject.transform.position = heliStartPoint.position;
                helicopterObject.SetActive(true);

                //헬기가 3초 동안 서서히 착륙 지점으로 내려옴
                float heliTime = 0f;
                float heliDuration = 3.0f;

                while (heliTime < heliDuration)
                {
                    heliTime += Time.deltaTime;
                    helicopterObject.transform.position = Vector3.Lerp(heliStartPoint.position, heliLandingPoint.position, heliTime / heliDuration);
                    yield return null;
                }

                //오차 보정
                helicopterObject.transform.position = heliLandingPoint.position;
            }
        }
        else if (escapeRoute == EscapeRoute.FrontDoor)
        {
            //정문 자동차/도보 연출
            if (carSound != null) carSound.Play();
            if (escapeCarObject != null) escapeCarObject.SetActive(true);
        }
    }

    private IEnumerator PlayerBoardingRoutine(PlayerController player)
    {
        Transform boardingPoint = null;
        float moveDuration = 2.0f;

        //조건에 맞는 탑승 지점 세팅
        if (escapeRoute == EscapeRoute.Rooftop && heliBoardingPoint != null)
        {
            boardingPoint = heliBoardingPoint;
            moveDuration = 2.0f;
        }
        else if (escapeRoute == EscapeRoute.FrontDoor && carBoardingPoint != null)
        {
            boardingPoint = carBoardingPoint;
            moveDuration = 2.5f;
        }

        if (boardingPoint != null)
        {
            //플레이어 강제 걷기 연출
            yield return StartCoroutine(MovePlayerGradually(player, boardingPoint, moveDuration));

            //도착 후 헬기/자동차에 위치 고정 (페이드 아웃 대기 시간 동안 안 떨어지게 찰싹 붙임)
            StartCoroutine(LockPlayerToVehicle(player, boardingPoint));
        }

        //공동 탈출 연출 (화면 페이드 아웃)
        if (player.Object.HasInputAuthority && ScreenFader.Instance != null) ScreenFader.Instance.FadeOut(2.5f);

        //화면이 완전히 까맣게 변하는 시간 대기
        yield return new WaitForSeconds(3.0f);

        //2.5초 뒤 완전히 화면이 까맣게 된 시점에서 탈출 처리 실행
        if (player != null && player.Object.IsValid) player.ServerEnterEscaped();
    }

    private IEnumerator EscapeCountdownRoutine()
    {
        //제한 시간 대기
        yield return new WaitForSeconds(escapeTimeLimit);

        //씬 내의 모든 플레이어 탐색
        PlayerController[] allPlayers = FindObjectsByType<PlayerController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        foreach (PlayerController p in allPlayers)
        {
            //살아있지만 트리거를 밟지 못한 지각생 판별
            if ((p.NetPlayerState == PlayerState.Normal || p.NetPlayerState == PlayerState.Captured) && !escapingPlayers.Contains(p))
            {
                //서버 권한으로 사망 처리
                if (p.Object.HasStateAuthority)
                {
                    p.ServerEnterDead(); //사망 함수 연결
                    Debug.Log($"[{p.gameObject.name}] 탈출 시간 초과로 사망 처리됨.");
                }
            }
        }
    }

    private IEnumerator MovePlayerGradually(PlayerController player, Transform targetTransform, float duration)
    {
        Vector3 startPos = player.transform.position;
        Quaternion startRot = player.transform.rotation;

        //탑승 지점과 플레이어 사이의 수평 방향 계산 및 고정
        Vector3 direction = (targetTransform.position - startPos).normalized;
        direction.y = 0; //수평 방향으로만 이동하도록 Y축 성분 제거
        Quaternion lookAtTargetRot = direction != Vector3.zero ? Quaternion.LookRotation(direction) : startRot;

        float elapsed = 0;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            //차량이 움직일 수 있으므로 매 프레임 목표 위치 갱신
            Vector3 currentTargetPos = targetTransform.position;            

            Vector3 currentPos = Vector3.Lerp(startPos, currentTargetPos, t);
            Quaternion currentRot = Quaternion.Slerp(startRot, lookAtTargetRot, t);

            //KCC 물리 엔진 간섭을 피해 위치와 시야 회전을 동시에 보간 적용
            if (player.KCCMotor != null)
            {
                player.KCCMotor.WarpToPose(currentPos, currentRot);
            }
            else
            {
                player.transform.position = currentPos;
                player.transform.rotation = currentRot;
            }

            yield return null;
        }
    }

    //도착 후 플레이어를 차량에 완전히 고정시키는 코루틴
    private IEnumerator LockPlayerToVehicle(PlayerController player, Transform targetTransform)
    {
        //플레이어가 살아있고 아직 탈출(Escaped) 처리가 안 끝났다면 계속 차량에 붙여둠
        while (player != null && player.Object != null && player.Object.IsValid && player.NetPlayerState != PlayerState.Escaped)
        {
            //위치는 차량에 고정, 회전은 탑승 완료 시점의 방향(차량을 바라보는 방향)을 그대로 유지
            if (player.KCCMotor != null) player.KCCMotor.WarpToPose(targetTransform.position, targetTransform.rotation);
            else player.transform.position = targetTransform.position;                
            yield return null;
        }
    }
}