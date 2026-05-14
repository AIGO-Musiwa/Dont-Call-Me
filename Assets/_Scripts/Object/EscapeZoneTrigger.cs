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

    private HashSet<PlayerController> escapingPlayers = new HashSet<PlayerController>();

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
                    //즉시 이동 및 시야 회전 정지
                    player.SetInputLock(true, true);
                    StartCoroutine(CinematicEscapeRoutine(player));
                }
            }
        }
    }

    private IEnumerator CinematicEscapeRoutine(PlayerController player)
    {
        //루트별 개별 연출 실행
        if (escapeRoute == EscapeRoute.Rooftop)
        {
            //옥상 핼기 연출
            if (player.Object.HasInputAuthority && heliSound != null) heliSound.Play();

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
                    helicopterObject.transform.position = helicopterObject.transform.position = Vector3.Lerp(heliStartPoint.position, heliLandingPoint.position, heliTime / heliDuration);
                    yield return null;
                }
            }

            //플레이어 강제 걷기 연출
            if (heliBoardingPoint != null)
            {
                yield return StartCoroutine(MovePlayerGradually(player, heliBoardingPoint.position, 2.0f));
            }
        }

        else if (escapeRoute == EscapeRoute.FrontDoor)
        {
            //정문 자동차/도보 연출
            if (player.Object.HasInputAuthority && carSound != null) carSound.Play();
            if (escapeCarObject != null) escapeCarObject.SetActive(true);

            //차문 앞(또는 도보 탈출구)으로 플레이어 강제 이동
            if (carBoardingPoint != null)
            {
                yield return StartCoroutine(MovePlayerGradually(player, carBoardingPoint.position, 2.5f));
            }
        }

        //공동 탈출 연출 (화면 페이드 아웃)
        if (player.Object.HasInputAuthority && ScreenFader.Instance != null) ScreenFader.Instance.FadeOut(2.5f);

        //화면이 완전히 까맣게 변하는 시간 대기
        yield return new WaitForSeconds(2.5f);

        //2.5초 뒤 완전히 화면이 까맣게 된 시점에서 탈출 처리 실행
        if (player != null && player.Object.IsValid) player.ServerEnterEscaped();        
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
