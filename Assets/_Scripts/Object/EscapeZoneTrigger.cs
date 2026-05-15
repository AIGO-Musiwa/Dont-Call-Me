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
        //플레이어 강제 걷기 연출
        if (escapeRoute == EscapeRoute.Rooftop && heliBoardingPoint != null)
        {
            yield return StartCoroutine(MovePlayerGradually(player, heliBoardingPoint.position, 2.0f));
        }
        else if (escapeRoute == EscapeRoute.FrontDoor && carBoardingPoint != null)
        {
            yield return StartCoroutine(MovePlayerGradually(player, carBoardingPoint.position, 2.5f));
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

    private IEnumerator MovePlayerGradually(PlayerController player, Vector3 targetPos, float duration)
    {
        Vector3 startPos = player.transform.position;
        float elapsed = 0;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            //KCC나 물리 엔진 간섭을 피하기 위해 직접 좌표 보간
            player.transform.position = Vector3.Lerp(startPos, targetPos, elapsed / duration);
            yield return null;
        }

        player.transform.position = targetPos;
    }
}