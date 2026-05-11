using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Experimental.Rendering;

// [통제실] 유니티 에디터의 Renderer Feature 리스트에 등록될 메인 클래스
public class OutlineFeature : ScriptableRendererFeature
{
    [Serializable]
    public class OutlineSettings
    {
        [Header("대상 설정")]
        public LayerMask LayerMask;           // 외곽선을 그릴 대상 레이어 (예: ItemOutline)

        [Header("재료(Material) 설정")]
        public Material SilhouetteMaterial;   // 1단계: 실루엣을 뽑아낼 때 쓸 재료
        public Material OutlineMaterial;      // 2단계: 최종 외곽선을 화면에 덧칠할 재료

        [Header("마스킹 설정")]
        [Range(0, 255)]
        public int StencilReference = 1;      // 스텐실 버퍼에 사용할 도장 번호 (기본값 1)
    }

    public OutlineSettings settings = new OutlineSettings();

    // ---------------------------------------------------------------------------------------------------------
    // [공정 1] 실루엣 추출 공정
    // ---------------------------------------------------------------------------------------------------------
    class RenderSilhouettePass : ScriptableRenderPass
    {
        private Material silhouetteMaterial;
        private FilteringSettings filteringSettings;
        private static readonly int SilhouetteTextureID = Shader.PropertyToID("_OutlineRenderTexture");

        public RenderSilhouettePass(LayerMask layerMask, Material mat)
        {
            silhouetteMaterial = mat;
            // 지정된 레이어의 '불투명(Opaque)' 물체들만 골라내도록 필터링
            filteringSettings = new FilteringSettings(RenderQueueRange.opaque, layerMask);
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (silhouetteMaterial == null) return;

            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
            UniversalRenderingData renderingData = frameData.Get<UniversalRenderingData>();
            UniversalLightData lightData = frameData.Get<UniversalLightData>();
            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();

            // 1. 실루엣을 담을 임시 가상 도화지(Texture) 설정
            TextureDesc texDesc = new TextureDesc(cameraData.cameraTargetDescriptor.width, cameraData.cameraTargetDescriptor.height)
            {
                colorFormat = GraphicsFormat.R8G8B8A8_UNorm,
                clearBuffer = true,
                clearColor = Color.clear,
                name = "_OutlineRenderTexture"
            };
            TextureHandle silhouetteTexture = renderGraph.CreateTexture(texDesc);

            // 2. 그리기 명령(Raster Pass) 추가
            using (var builder = renderGraph.AddRasterRenderPass<PassData>("Render Silhouette", out var passData))
            {
                var sortingCriteria = cameraData.defaultOpaqueSortFlags;
                var drawingSettings = CreateDrawingSettings(new ShaderTagId("UniversalForward"), renderingData, cameraData, lightData, sortingCriteria);
                drawingSettings.overrideMaterial = silhouetteMaterial;

                RendererListParams listParams = new RendererListParams(renderingData.cullResults, drawingSettings, filteringSettings);
                passData.rendererList = renderGraph.CreateRendererList(listParams);
                builder.UseRendererList(passData.rendererList);

                // 가상 도화지와 카메라의 깊이/스텐실 버퍼를 모두 연결
                builder.SetRenderAttachment(silhouetteTexture, 0, AccessFlags.Write);
                builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture, AccessFlags.Write);
                builder.AllowPassCulling(false);

                builder.SetGlobalTextureAfterPass(silhouetteTexture, SilhouetteTextureID);

                // [수리 완료] RasterCommandBuffer에서는 DrawRendererList 하나만으로 충분합니다.
                // 스텐실 도장은 'SilhouetteMaterial'의 설정에 따라 자동으로 찍힙니다.
                builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                {
                    context.cmd.DrawRendererList(data.rendererList);
                });
            }
        }

        private class PassData { public RendererListHandle rendererList; }
    }

    // ---------------------------------------------------------------------------------------------------------
    // [공정 2] 외곽선 도색 공정
    // ---------------------------------------------------------------------------------------------------------
    class DrawOutlinePass : ScriptableRenderPass
    {
        private Material outlineMaterial;

        public DrawOutlinePass(Material mat)
        {
            outlineMaterial = mat;
            requiresIntermediateTexture = true;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (outlineMaterial == null) return;

            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            TextureHandle activeColorTexture = resourceData.activeColorTexture;

            using (var builder = renderGraph.AddRasterRenderPass<PassData>("Draw Outline", out var passData))
            {
                passData.material = outlineMaterial;

                builder.SetRenderAttachment(activeColorTexture, 0);
                // 아까 찍어둔 도장(Stencil)을 확인하기 위해 깊이/스텐실 버퍼를 읽기 전용으로 연결
                builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture, AccessFlags.Read);
                builder.AllowPassCulling(false);

                // [수리 완료] RasterCommandBuffer에서 지원하지 않는 SetStencilState를 제거했습니다.
                // 대신 'OutlineMaterial'의 셰이더 설정에서 스텐실 판정을 수행하게 됩니다.
                builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                {
                    Blitter.BlitTexture(context.cmd, new Vector2(1, 1), data.material, 0);
                });
            }
        }

        private class PassData { public Material material; }
    }

    private RenderSilhouettePass silhouettePass;
    private DrawOutlinePass outlinePass;

    public override void Create()
    {
        // 1공정: 실루엣 추출
        silhouettePass = new RenderSilhouettePass(settings.LayerMask, settings.SilhouetteMaterial)
        {
            renderPassEvent = RenderPassEvent.AfterRenderingOpaques
        };

        // 2공정: 외곽선 덧칠
        // [중요] AfterRenderingOpaques 바로 다음에 붙여야 카메라 이동과의 시차(Lag)가 사라짐
        outlinePass = new DrawOutlinePass(settings.OutlineMaterial)
        {
            renderPassEvent = RenderPassEvent.AfterRenderingOpaques
        };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (settings.SilhouetteMaterial != null && settings.OutlineMaterial != null)
        {
            renderer.EnqueuePass(silhouettePass);
            renderer.EnqueuePass(outlinePass);
        }
    }
}