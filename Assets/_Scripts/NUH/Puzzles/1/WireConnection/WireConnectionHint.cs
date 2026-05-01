using System.Collections.Generic;
using UnityEngine;

public class WireConnectionHint : MonoBehaviour, IPuzzleSeedReceiver
{
    [Header("설정")]
    [SerializeField] private int socketCount = 6;

    [Header("힌트 슬롯 렌더러")]
    [SerializeField] private List<Renderer> leftHintRenderers = new();
    [SerializeField] private List<Renderer> rightHintRenderers = new();

    [Header("색상 머터리얼 프로퍼티")]
    [SerializeField] private string colorPropertyName = "_BaseColor";

    [Header("디버그")]
    [SerializeField] private bool enableDebugLog = true;

    private MaterialPropertyBlock _mpb;

    private void Awake()
    {
        _mpb = new MaterialPropertyBlock();
    }

    public void ApplyAnswerSeed(int seed)
    {
        WireConnectionAnswerGenerator.Result result = WireConnectionAnswerGenerator.Generate(seed, socketCount);

        int count = Mathf.Min(socketCount, Mathf.Min(leftHintRenderers.Count, rightHintRenderers.Count));

        for (int slotIndex = 0; slotIndex < count; slotIndex++)
        {
            int leftIndex = result.HintOrderLeftIndices[slotIndex];
            int rightIndex = result.CorrectRightIndexByLeft[leftIndex];

            ApplyColor(leftHintRenderers[slotIndex], ToUnityColor(result.LeftColors[leftIndex]));
            ApplyColor(rightHintRenderers[slotIndex], ToUnityColor(result.RightColors[rightIndex]));
        }

        Log("전선 힌트 시드 적용 완료");
    }

    private void ApplyColor(Renderer renderer, Color color)
    {
        if (renderer == null)
            return;

        renderer.GetPropertyBlock(_mpb);
        _mpb.SetColor(colorPropertyName, color);
        renderer.SetPropertyBlock(_mpb);
    }

    private Color ToUnityColor(WireSocketColor socketColor)
    {
        return socketColor switch
        {
            WireSocketColor.Red => Color.red,
            WireSocketColor.DarkOrange => new Color(1f, 0.55f, 0f),
            WireSocketColor.Yellow => Color.yellow,
            WireSocketColor.Green => Color.green,
            WireSocketColor.Blue => Color.blue,
            WireSocketColor.DarkGray => new Color(0.25f, 0.25f, 0.25f),
            WireSocketColor.Purple => Color.purple,
            WireSocketColor.White => Color.white,
            WireSocketColor.Black => Color.black,
            _ => Color.white
        };
    }

    private void Log(string message)
    {
        if (!enableDebugLog)
            return;

        Debug.Log($"[WireConnectionHint] {message}", this);
    }
}
