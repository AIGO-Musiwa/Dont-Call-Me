using Fusion;
using System;
using UnityEngine;

/// <summary>
/// 퍼즐 상호작용 공통 베이스
/// 
/// 역할
/// - 플레이어 생존 상태 확인
/// - 서버 권한에서만 퍼즐 판정
/// - 규칙에 따라 오른손 아이템 먼저 드랍
/// - 이후 퍼즐 고유 로직 실행
/// - solved 상태 변경 시 모든 클라이언트에서 후처리 훅 호출
/// - 스폰 시 주입받은 Zone 정보를 공통으로 보관
/// </summary>
public abstract class PuzzleInteractableBase : NetworkBehaviour, IInteractable
{
    [Header("공통 퍼즐 프롬프트")]
    [SerializeField] private string defaultPromptText = "퍼즐 상호작용";                          // 기본 프롬프트 문구
    [SerializeField] private string promptWhenHoldingItem = "오른손 아이템 내려놓고 퍼즐 상호작용"; // 오른손 아이템 보유 시 프롬프트 문구

    [Header("퍼즐 진행도")]
    [SerializeField] private bool countForStage1Progress = true; // 1단계 진행도 집계 대상인지 여부

    [Header("사운드 모듈")]
    [SerializeField] public MultiAudioTrigger audioModule; // 공통 성공/실패 사운드 모듈

    [Networked, OnChangedRender(nameof(OnSolvedStateChangedRender))]
    public NetworkBool NetIsSolved { get; private set; } // 퍼즐 최종 클리어 여부 네트워크 동기화 값

    [Networked, OnChangedRender(nameof(OnSpawnZoneChangedRender))]
    private NetworkBool NetHasSpawnZone { get; set; } // 스폰 Zone 주입 여부

    [Networked, OnChangedRender(nameof(OnSpawnZoneChangedRender))]
    private int NetSpawnZoneValue { get; set; } // Zone enum을 int로 저장

    [Networked, OnChangedRender(nameof(OnStage3HintDataChangedRender))]
    private int NetStage3HintSerial { get; set; } // Stage3 힌트 데이터 변경 이벤트 카운터

    [Networked] private NetworkBool NetHasStage3HintData { get; set; } // Stage3 힌트 데이터 보유 여부
    [Networked] private int NetStage3HintDigit0 { get; set; } // Stage3 힌트 0번 숫자
    [Networked] private int NetStage3HintDigit1 { get; set; } // Stage3 힌트 1번 숫자
    [Networked] private int NetStage3HintDigit2 { get; set; } // Stage3 힌트 2번 숫자
    [Networked] private int NetStage3HintDigit3 { get; set; } // Stage3 힌트 3번 숫자
    [Networked] private int NetStage3HintDigit4 { get; set; } // Stage3 힌트 4번 숫자
    [Networked] private int NetStage3HintDigit5 { get; set; } // Stage3 힌트 5번 숫자

    [Networked] private NetworkBool NetStage3HintReveal0 { get; set; } // Stage3 힌트 0번 공개 여부
    [Networked] private NetworkBool NetStage3HintReveal1 { get; set; } // Stage3 힌트 1번 공개 여부
    [Networked] private NetworkBool NetStage3HintReveal2 { get; set; } // Stage3 힌트 2번 공개 여부
    [Networked] private NetworkBool NetStage3HintReveal3 { get; set; } // Stage3 힌트 3번 공개 여부
    [Networked] private NetworkBool NetStage3HintReveal4 { get; set; } // Stage3 힌트 4번 공개 여부
    [Networked] private NetworkBool NetStage3HintReveal5 { get; set; } // Stage3 힌트 5번 공개 여부

    public bool CountForStageProgress => countForStage1Progress; // 진행도 집계 포함 여부 외부 읽기용

    private float failSounddB = 49f;

    /// <summary>
    /// 현재 퍼즐에 스폰 Zone이 주입되었는지 반환한다.
    /// </summary>
    public bool HasSpawnZone
    {
        get
        {
            if (!IsNetworkReady)
                return false;

            return NetHasSpawnZone;
        }
    }

    /// <summary>
    /// 현재 퍼즐이 스폰된 Zone.
    /// HasSpawnZone이 false면 기본값일 수 있으므로 먼저 확인해야 한다.
    /// </summary>
    public Zone SpawnZone => (Zone)NetSpawnZoneValue;

    public bool IsSolved
    {
        get
        {
            if (!IsNetworkReady)
                return false; // Spawned 전에는 Networked 프로퍼티 접근 금지

            return NetIsSolved; // 네트워크 준비 후 solved 상태 반환
        }
    }

    public bool IsNetworkReady { get; private set; } // 네트워크 준비 여부 확인용

    public event Action<PuzzleInteractableBase> Solved; // 퍼즐 클리어 시 외부에 알리는 이벤트

    public override void Spawned()
    {
        IsNetworkReady = true; // Spawned 이후 네트워크 준비 완료 표시

        if (NetHasSpawnZone)
            HandleSpawnZoneAssigned(SpawnZone); // 이미 Zone이 들어온 상태면 자식 후처리 반영

        if (NetHasStage3HintData)
            HandleStage3HintDataChanged(); // 이미 Stage3 힌트 데이터가 들어온 상태면 화면 데이터 반영

        if (NetIsSolved)
            HandleSolvedStateChanged(); // 이미 solved 상태로 스폰된 경우 화면/연출 반영
    }

    /// <summary>
    /// PuzzleSpawnManager가 퍼즐을 스폰한 Zone을 주입한다.
    /// 서버 권한에서만 호출한다.
    /// </summary>
    public virtual void SetSpawnZone(Zone zone)
    {
        if (!HasStateAuthority)
            return; // 서버/상태 권한 없는 쪽은 Zone 확정 불가

        NetSpawnZoneValue = (int)zone; // Zone 값을 네트워크 상태로 저장
        NetHasSpawnZone = true; // Zone 주입 완료 표시

        HandleSpawnZoneAssigned(zone); // 서버 측 즉시 후처리
    }

    /// <summary>
    /// 스폰 Zone이 주입되었을 때 자식 퍼즐에서 필요한 처리를 override한다.
    /// 기본 구현은 없음.
    /// </summary>
    protected virtual void HandleSpawnZoneAssigned(Zone zone)
    {
        // 자식 퍼즐에서 필요 시 override
    }

    /// <summary>
    /// 현재 플레이어가 이 퍼즐 조작물과 상호작용 가능한지 검사한다.
    /// </summary>
    public virtual bool CanInteract(PlayerController actor)
    {
        if (!CanInteractCommon(actor))
            return false; // 공통 조건에서 탈락하면 상호작용 불가

        return CanInteractInternal(actor); // 자식 퍼즐 추가 조건 검사
    }

    /// <summary>
    /// 퍼즐 상호작용 진입점.
    /// 공통 규칙 처리 후 자식 퍼즐의 서버 로직으로 전달한다.
    /// </summary>
    public void Interact(PlayerController actor)
    {
        if (!HasStateAuthority)
            return; // 상태 권한 없는 쪽은 실제 판정 불가

        if (!CanInteract(actor))
            return; // 상호작용 불가 상태면 종료

        DropRightHandIfNeeded(actor); // 오른손 아이템 있으면 먼저 드랍

        ServerInteract(actor); // 자식 퍼즐 실제 서버 로직 실행
    }

    /// <summary>
    /// 상호작용 프롬프트 문구를 반환한다.
    /// </summary>
    public virtual string GetPromptText(PlayerController actor)
    {
        if (actor != null && actor.NetRightHandItem != null)
            return promptWhenHoldingItem; // 오른손에 아이템 있으면 드랍 안내 문구

        return defaultPromptText; // 기본 문구 반환
    }

    /// <summary>
    /// 퍼즐 상호작용 공통 최소 조건 검사.
    /// 퍼즐별 세부 조건은 자식 클래스에서 추가한다.
    /// </summary>
    protected virtual bool CanInteractCommon(PlayerController actor)
    {
        if (actor == null)
            return false; // 플레이어 참조 없으면 불가

        if (actor.NetPlayerState != PlayerState.Normal)
            return false; // 일반 생존 상태가 아니면 불가

        if (IsSolved)
            return false; // 이미 클리어된 퍼즐은 공통 차단

        return true; // 공통 조건 통과
    }

    /// <summary>
    /// 오른손에 아이템이 있으면 퍼즐 상호작용 전에 드랍한다.
    /// </summary>
    protected virtual void DropRightHandIfNeeded(PlayerController actor)
    {
        if (actor == null)
            return; // 플레이어 없으면 종료

        if (actor.NetRightHandItem != null)
            actor.ServerDropRightHandItem(); // 오른손 아이템 드랍
    }

    /// <summary>
    /// 퍼즐별 추가 상호작용 가능 조건.
    /// 기본 구현은 항상 true.
    /// </summary>
    protected virtual bool CanInteractInternal(PlayerController actor)
    {
        return true; // 자식 퍼즐에서 필요 시 override
    }

    /// <summary>
    /// 퍼즐 성공 상태를 네트워크에 반영한다.
    /// 서버 권한에서만 호출한다.
    /// </summary>
    protected virtual void MarkSolved()
    {
        if (!HasStateAuthority)
            return; // 권한 없는 쪽은 solved 확정 불가

        if (!IsNetworkReady)
            return; // 네트워크 준비 전이면 종료

        if (NetIsSolved)
            return; // 이미 solved면 중복 처리 방지

        if (audioModule != null)
            audioModule.PlaySound(SoundType.Success); // 성공

        NetIsSolved = true; // solved 상태 네트워크 반영
        Solved?.Invoke(this); // 외부 이벤트 발행
    }

    /// <summary>
    /// 퍼즐 실패 시 자식 클래스에서 호출하는 공통 훅.
    /// 기본 구현은 비워두고 자식 클래스에서 연출을 추가한다.
    /// </summary>
    protected virtual void MarkFailed()
    {
        SoundEmitter.EmitWalkieDirect(failSounddB, transform.position, SpawnZone);
        if (audioModule != null)
            audioModule.PlaySound(SoundType.Fail); // 실패
    }

    /// <summary>
    /// solved 상태를 테스트용으로 되돌릴 때 사용한다.
    /// </summary>
    protected virtual void ResetSolvedState()
    {
        if (!HasStateAuthority)
            return; // 권한 없는 쪽은 리셋 불가

        NetIsSolved = false; // solved 상태 해제
    }

    /// <summary>
    /// NetIsSolved가 변경되었을 때 Fusion Render 단계에서 호출된다.
    /// 모든 클라이언트에서 solved 후처리를 반영하기 위한 진입점이다.
    /// </summary>
    private void OnSolvedStateChangedRender()
    {
        HandleSolvedStateChanged(); // 자식 클래스 후처리 훅 호출
    }

    /// <summary>
    /// 스폰 Zone 값이 변경되었을 때 Fusion Render 단계에서 호출된다.
    /// 모든 클라이언트에서 Zone 후처리를 반영하기 위한 진입점이다.
    /// </summary>
    private void OnSpawnZoneChangedRender()
    {
        if (!NetHasSpawnZone)
            return;

        HandleSpawnZoneAssigned(SpawnZone);
    }

    /// <summary>
    /// solved 상태 변경 시 공통으로 호출되는 후처리 함수.
    /// 자식 클래스는 이 함수를 override해서 화면 전환 등을 처리한다.
    /// </summary>
    protected virtual void HandleSolvedStateChanged()
    {
        // 기본 구현 없음
    }


    /// <summary>
    /// Stage3 힌트 데이터를 Networked 값으로 저장한다.
    /// PuzzleSpawnManager가 서버 권한에서 Stage2 퍼즐에 힌트를 배정할 때 호출한다.
    /// </summary>
    public void SetNetworkStage3HintData(FinalCodeHintData hintData)
    {
        if (!HasStateAuthority)
            return;

        if (!IsNetworkReady)
            return;

        if (hintData == null)
        {
            NetHasStage3HintData = false;
            NetStage3HintSerial++;
            HandleStage3HintDataChanged();
            return;
        }

        NetStage3HintDigit0 = hintData.GetDigit(0);
        NetStage3HintDigit1 = hintData.GetDigit(1);
        NetStage3HintDigit2 = hintData.GetDigit(2);
        NetStage3HintDigit3 = hintData.GetDigit(3);
        NetStage3HintDigit4 = hintData.GetDigit(4);
        NetStage3HintDigit5 = hintData.GetDigit(5);

        NetStage3HintReveal0 = hintData.IsRevealed(0);
        NetStage3HintReveal1 = hintData.IsRevealed(1);
        NetStage3HintReveal2 = hintData.IsRevealed(2);
        NetStage3HintReveal3 = hintData.IsRevealed(3);
        NetStage3HintReveal4 = hintData.IsRevealed(4);
        NetStage3HintReveal5 = hintData.IsRevealed(5);

        NetHasStage3HintData = true;
        NetStage3HintSerial++; // 모든 클라이언트에서 힌트 표시를 다시 적용하게 만든다.

        HandleStage3HintDataChanged(); // 서버/Host 로컬 즉시 반영
    }

    /// <summary>
    /// 현재 Networked Stage3 힌트 데이터가 있는지 반환한다.
    /// </summary>
    protected bool HasNetworkStage3HintData()
    {
        if (!IsNetworkReady)
            return false;

        return NetHasStage3HintData;
    }

    /// <summary>
    /// Networked Stage3 힌트 데이터로 FinalCodeHintData를 재구성한다.
    /// </summary>
    protected bool TryGetNetworkStage3HintData(out FinalCodeHintData hintData)
    {
        hintData = null;

        if (!IsNetworkReady)
            return false;

        if (!NetHasStage3HintData)
            return false;

        int[] digits =
        {
            NetStage3HintDigit0,
            NetStage3HintDigit1,
            NetStage3HintDigit2,
            NetStage3HintDigit3,
            NetStage3HintDigit4,
            NetStage3HintDigit5
        };

        bool[] revealed =
        {
            NetStage3HintReveal0,
            NetStage3HintReveal1,
            NetStage3HintReveal2,
            NetStage3HintReveal3,
            NetStage3HintReveal4,
            NetStage3HintReveal5
        };

        hintData = new FinalCodeHintData(digits, revealed);
        return true;
    }

    /// <summary>
    /// Stage3 힌트 데이터 Networked 값이 변경되었을 때 Fusion Render 단계에서 호출된다.
    /// </summary>
    private void OnStage3HintDataChangedRender()
    {
        HandleStage3HintDataChanged();
    }

    /// <summary>
    /// Stage3 힌트 데이터 변경 시 자식 퍼즐에서 View에 반영하기 위한 후처리 함수.
    /// </summary>
    protected virtual void HandleStage3HintDataChanged()
    {
        // 자식 퍼즐에서 필요 시 override
    }

    /// <summary>
    /// 실제 퍼즐 고유 판정.
    /// 반드시 서버 권한에서만 실행된다.
    /// </summary>
    protected abstract void ServerInteract(PlayerController actor);
}