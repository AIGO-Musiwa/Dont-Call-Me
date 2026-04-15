using Fusion;
using System;
using UnityEngine;

/// <summary>
/// 퍼즐 상호작용 공통 베이스
/// 
/// 역할
/// -플레이어 생존 상태 확인
/// -서버 권한에서만 퍼즐 판정
/// -규칙에 따라 오른손 아이템 먼저 드랍
/// -이후 퍼즐 고유 로직 실행
/// </summary>
public abstract class PuzzleInteractableBase : NetworkBehaviour, IInteractable
{
    [Header("공통 퍼즐 프롬프트")]
    [SerializeField] private string defaultPromptText = "퍼즐 상호작용";                              // 기본 프롬프트 문구
    [SerializeField] private string promptWhenHoldingItem = "오른손 아이템 내려놓고 퍼즐 상호작용";     // 오른손에 아이템 보유시 프롬프트 문구

    [Header("퍼즐 진행도")]
    [SerializeField] private bool countForStage1Progress = true;        // 1단계 진행도 집계 대상인지 여부

    [Networked] public NetworkBool NetIsSolved { get; private set; }    // 퍼즐이 최종 클리어 되었는지
    public bool CountForStageProgress => countForStage1Progress;        // 진행도 집계 포함 여부
    public bool IsSolved => NetIsSolved;                                // 현재 클리어 여부 읽기용

    public event Action<PuzzleInteractableBase> Solved;                 // 퍼즐 클리어 시 외부에 알리는 이벤트

    /// <summary>
    /// 현재 플레이어가 이 퍼즐 조작물을 상호작용 가능한지 검사
    /// </summary>
    /// <param name="actor"></param>
    /// <returns></returns>
    public virtual bool CanInteract(PlayerController actor)
    {
        if (!CanInteractCommon(actor))
            return false;

        return CanInteractInternal(actor);
    }

    /// <summary>
    /// 퍼즐 상호작용 진입
    /// 공통 규칙 처리 후 자식 퍼즐의 서버 로직으로 전달
    /// </summary>
    /// <param name="actor"></param>
    public void Interact(PlayerController actor)
    {
        if (!HasStateAuthority) 
            return;
        
        if (!CanInteract(actor)) 
            return;

        // 오른손에 아이템이 있으면 퍼즐 상호작용 전에 먼저 드랍
        DropRightHandIfNeeded(actor);

        // 퍼즐별 실제 처리 시작
        ServerInteract(actor);
    }

    public virtual string GetPromptText(PlayerController actor)
    {
        if (actor != null && actor.NetRightHandItem != null)
            return promptWhenHoldingItem;

        return defaultPromptText;
    }

    /// <summary>
    /// 퍼즐 상호작용 공통 최소 조건 검사
    /// 퍼즐별 세부 조건은 자식 클래슫에서 추가
    /// </summary>
    /// <param name="actor"></param>
    protected virtual bool CanInteractCommon(PlayerController actor)
    {
        if (actor == null) 
            return false;

        if (actor.NetPlayerState != PlayerState.Normal) 
            return false;

        // 이미 클리어된 퍼즐은 다시 상호작용하지 않도록 공통 차단
        if (NetIsSolved)
            return false;

        return true;
    }

    /// <summary>
    /// 오른손에 아이템이 있으면 퍼즐 상호작용 전에 드랍
    /// </summary>
    protected virtual void DropRightHandIfNeeded(PlayerController actor)
    {
        if (actor == null)
            return;

        if (actor.NetRightHandItem != null)
            actor.ServerDropRightHandItem();
    }
    
    /// <summary>
    /// 퍼즐별 추가 상호작용 가능 조건
    /// </summary>
    protected virtual bool CanInteractInternal(PlayerController actor)
    {
        return true;
    }

    protected virtual void MarkSolved()
    {
        if (!HasStateAuthority)
            return;

        if (NetIsSolved)
            return;

        NetIsSolved = true;
        Solved?.Invoke(this);
    }

    /// <summary>
    /// 퍼즐 실패 시 자식 클래스에서 호출하는 공통 훅
    /// 기본 구현 비워두고 자식 클래스에서 연출 추가
    /// </summary>
    protected virtual void MarkFailed()
    {
        // 기본 공통 처리 없음
    }

    /// <summary>
    /// 퍼즐 리셋 시 성공 상태 되돌릴때 사용하는 호출용 메서드
    /// 테스트용
    /// </summary>
    protected virtual void ResetSolvedState()
    {
        if (!HasStateAuthority)
            return;

        NetIsSolved = false;
    }

    /// <summary>
    /// 실제 퍼즐 고유 판정
    /// 반드시 서버 권한에서만 실행됨
    protected abstract void ServerInteract(PlayerController actor);
}
