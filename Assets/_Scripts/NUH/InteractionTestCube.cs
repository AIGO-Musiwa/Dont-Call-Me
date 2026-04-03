using Fusion;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class InteractionTestCube : NetworkBehaviour, IInteractable
{
    [SerializeField] private Renderer targetRenderer;
    [SerializeField] private Color offColor = Color.white;
    [SerializeField] private Color onColor = Color.red;

    [Networked] public NetworkBool IsOn { get; set; }

    private void Awake()
    {
        if(targetRenderer == null)
            targetRenderer = GetComponentInChildren<Renderer>();
    }

    public bool CanInteract(PlayerController actor)
    {
        if (actor == null)
            return false;

        return actor.NetPlayerState == PlayerState.Alive;
    }

    public void Interact(PlayerController actor)
    {
        if (!HasStateAuthority)
            return;

        IsOn = !IsOn;
    }

    public string GetPromptText(PlayerController actor)
    {
        return "테스트 상호작용";
    }

    public override void Render()
    {
        {
            if (targetRenderer == null)
                return;

            targetRenderer.material.color = IsOn ? onColor : offColor;
        }
    }
}
