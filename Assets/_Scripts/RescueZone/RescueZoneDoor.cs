using Fusion;
using System.Collections.Generic;
using UnityEngine;

public class RescueZoneDoor : NetworkBehaviour
{
    [Header("구역 설정")]
    public Zone myZone;

    [Header("문 작동 설정")]
    public Vector3 openRotation = new Vector3(0, 90, 0);
    public float openSpeed = 5f;

    [Header("오디오 선택")]
    public AudioSource audioSource;
    public AudioClip doorOpenSound;

    //네트워크를 통해 모든 클라이언트에게 공유되는 문 개방 상태
    [Networked] public NetworkBool IsOpen { get; set; }    

    //첫 포획 감금 전까지 초기 '강제 열림' 상태를 유지하기 위한 로컬 플래그
    [Networked] public NetworkBool IsInitialForcedOpen { get; set; }

    //초기 문이 닫힌 상태와 목표로 하는 문이 열린 상태의 회전값 캐싱 
    private Quaternion closedRotation;
    private Quaternion targetOpenRotation;

    //크리처의 문 제어를 위한 정적 딕셔너리
    private static Dictionary<Zone, List<RescueZoneDoor>> doors = new Dictionary<Zone, List<RescueZoneDoor>>();

    private void Awake()
    {
        //초기 회전값 캐싱
        closedRotation = transform.localRotation;
        targetOpenRotation = closedRotation * Quaternion.Euler(openRotation);
    }

    public override void Spawned()
    {
        //딕셔너리에 자신을 등록
        if (!doors.ContainsKey(myZone)) doors[myZone] = new List<RescueZoneDoor>();
        if (!doors[myZone].Contains(this)) doors[myZone].Add(this);        

        if (HasStateAuthority)
        {            
            IsInitialForcedOpen = true;
            IsOpen = true;
        }

        //게임 시작 시 호스트가 문을 기본적으로 '열림' 상태로 설정
        transform.localRotation = targetOpenRotation;

        Debug.Log($"[RescueZoneDoor] {myZone} 문 스폰 완료! (초기 열림 상태 적용 완료)");
    }

    //객체 소멸 시 딕셔너리에서 제거
    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (doors.ContainsKey(myZone) && doors[myZone].Contains(this)) doors[myZone].Remove(this);
    }

    public override void Render()
    {
        //네트워크 오브젝트가 유효하지 않으면 실행 방지
        if (Object == null || !Object.IsValid) return;

        //초기 강제 열림 상태일 경우, 네트워크 동기화를 무시하고 무조건 열린 상태 유지
        if (IsInitialForcedOpen)
        {
            transform.localRotation = targetOpenRotation;
            return;
        }

        //네트워크 변수(IsOpen) 상태에 따라 목표 회전값 결정
        Quaternion targetRot = IsOpen ? targetOpenRotation : closedRotation;

        //매 프레임 부드럽게 목표 회전값을 향해 문을 회전시킴 (Lerp)
        transform.localRotation = Quaternion.Lerp(transform.localRotation, targetRot, Time.deltaTime * openSpeed);
    }


    //퍼즐의 비밀번호가 풀렸을 때 호출할 함수
    public void OpenDoor()
    {
        //상태 권한 확인 및 이미 열려있는지 체크
        if (HasStateAuthority && !IsOpen)
        {
            IsOpen = true;
            RPC_PlayDoorSound();
            Debug.Log($"[RescueZoneDoor] {myZone} 구출 구역 퍼즐 해금! 문이 개방됩니다.");
        }
    }

    public void CloseDoor()
    {
        if (HasStateAuthority)
        {
            //이제부터 Render의 정상 동기화 로직이 작동하도록 고정 플래그 해제
            if (IsInitialForcedOpen) IsInitialForcedOpen = false;
            
            //문이 열려있다가 닫치는 순간 퍼즐 리셋
            IsOpen = false;

            //문이 닫힐 때 해당 구역의 퍼즐을 리셋
            RescueZonePuzzle[] puzzles = FindObjectsByType<RescueZonePuzzle>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var puzzle in puzzles)
            {
                if (puzzle.PuzzleZone == this.myZone) puzzle.RegeneratePuzzle();
            }
        }
    }

    //문 열리는 소리를 모든 클라이언트에서 재생
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayDoorSound()
    {
        if (audioSource != null && doorOpenSound != null)
        {
            audioSource.PlayOneShot(doorOpenSound);
        }
    }
    
    //구출 구역 내에 있는 모든 문을 열음
    public static void OpenAllDoorsInZone(Zone zone)
    {
        if (doors.TryGetValue(zone, out List<RescueZoneDoor> zoneDoors))
        {
            foreach (var door in zoneDoors)
            {
                if (door.HasStateAuthority) door.OpenDoor();
                else door.RPC_RequestOpenDoor();
            }
            Debug.Log($"[RescueZoneDoor] {zone}의 모든 문({zoneDoors.Count}개)이 동시에 개방됩니다.");
        }
    }

    //구출 구역 내에 있는 모든 문을 닫음
    public static void CloseAllDoorsInZone(Zone zone)
    {
        if (doors.TryGetValue(zone, out List<RescueZoneDoor> zoneDoors))
        {
            foreach (var door in zoneDoors)
            {
                if (door.HasStateAuthority) door.CloseDoor();
                else door.RPC_RequestCloseDoor();
            }
            Debug.Log($"[RescueZoneDoor] {zone}의 모든 문({zoneDoors.Count}개)이 닫힙니다.");
        }
    }

    public static void EmitFailNoise(Zone zone, Vector3 position)
    {
        SoundEmitter.EmitToEventBus(SoundChannel.Walkie, 150f, position, 0f, zone);
        Debug.Log($"[RescueZoneDoor] 퍼즐 오답. 크리처를 유인하는 150dB 소음이 발생했습니다.");
    }

    //인스펙터에서 임시로 테스트해 볼 수 있는 디버그 버튼
    [ContextMenu("Debug/강제로 문 열기 (테스트)")]
    private void DebugForceOpen()
    {
        if (!Application.isPlaying) return;
        if (Object == null || !Object.IsValid) return;

        if (HasStateAuthority) OpenDoor();
        else RPC_RequestOpenDoor();
    }

    [ContextMenu("Debug/강제로 문 닫기 (테스트)")]
    private void DebugForceClose()
    {
        if (!Application.isPlaying) return;
        if (Object != null && Object.IsValid)
        {
            if (HasStateAuthority) CloseDoor();
            else RPC_RequestCloseDoor();
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_RequestOpenDoor() => OpenDoor();

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_RequestCloseDoor() => CloseDoor();
}
