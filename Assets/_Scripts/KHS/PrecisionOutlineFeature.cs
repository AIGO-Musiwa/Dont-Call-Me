using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

public class PrecisionOutlineFeature : ScriptableRendererFeature
{
    // 기공사가 인스펙터에서 조작할 설정 패널
    [System.Serializable]
    public class Settings
    {
        public LayerMask targetLayer;     // ItemOutline 레이어
        public Material outlineMaterial;  // PrecisionOutline 셰이더가 발린 매테리얼
    }

    public Settings settings = new Settings();

    class OutlinePass : ScriptableRenderPass
    {
        private Material outlineMaterial;
        private FilteringSettings filteringSettings;
        private RenderStateBlock renderStateBlock;

        public OutlinePass(Settings settings)
        {
            this.outlineMaterial = settings.outlineMaterial;
            this.filteringSettings = new FilteringSettings(RenderQueueRange.opaque, settings.targetLayer);

            // [수리 완료 1] 강제 스텐실 명령 대신, 합법적인 RenderStateBlock 부품을 생성합니다.
            // "물체를 그릴 때 스텐실 버퍼에 1번 도장을 찍어라"는 설정입니다.
            this.renderStateBlock = new RenderStateBlock(RenderStateMask.Stencil);
            this.renderStateBlock.stencilState = new StencilState(true, 1, 255, CompareFunction.Always, StencilOp.Replace, StencilOp.Replace, StencilOp.Replace);
            this.renderStateBlock.stencilReference = 1;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (outlineMaterial == null) return;

            var resourceData = frameData.Get<UniversalResourceData>();
            var renderingData = frameData.Get<UniversalRenderingData>();
            var cameraData = frameData.Get<UniversalCameraData>();

            // --- 1단계 공정: 조준된 물체(ItemOutline) 영역에 스텐실 도장 찍기 ---
            using (var builder = renderGraph.AddRasterRenderPass<PassData>("Mark Stencil Pass", out var passData))
            {
                // [수리 완료 2] CreateDrawingSettings의 인수를 최신 규격인 3개(ref renderingData 포함)로 맞췄습니다.
                var sortingCriteria = cameraData.defaultOpaqueSortFlags;
                var drawSettings = CreateDrawingSettings(new ShaderTagId("UniversalForward"), ref renderingData, sortingCriteria);

                // RenderStateBlock을 넘겨주어 스텐실 도장이 자동으로 찍히게 합니다.
                var param = new RendererListParams(renderingData.cullResults, drawSettings, filteringSettings, renderStateBlock);
                passData.rendererList = renderGraph.CreateRendererList(param);
                builder.UseRendererList(passData.rendererList);

                // 화면과 깊이 버퍼를 연결 (스텐실을 기록하기 위함)
                builder.SetRenderAttachment(resourceData.activeColorTexture, 0);
                builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture, AccessFlags.Write);
                builder.AllowPassCulling(false);

                builder.SetRenderFunc((PassData data, RasterGraphContext context) => {
                    context.cmd.DrawRendererList(data.rendererList);
                });
            }

            // --- 2단계 공정: 뎁스-노멀 레이더 스캔 및 테두리 출력 ---
            using (var builder = renderGraph.AddRasterRenderPass<PassData>("Draw Precision Outline", out var passData))
            {
                passData.material = outlineMaterial;

                builder.SetRenderAttachment(resourceData.activeColorTexture, 0);
                builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture, AccessFlags.Read);
                builder.AllowPassCulling(false);

                builder.SetRenderFunc((PassData data, RasterGraphContext context) => {
                    // Blitter가 화면 전체를 스캔하지만, 
                    // 매테리얼 내부의 Stencil { Ref 1 Comp Equal } 덕분에 물체 안쪽 테두리만 칠해집니다.
                    Blitter.BlitTexture(context.cmd, new Vector2(1, 1), data.material, 0);
                });
            }
        }

        private class PassData
        {
            public RendererListHandle rendererList;
            public Material material;
        }
    }

    private OutlinePass pass;

    public override void Create()
    {
        pass = new OutlinePass(settings)
        {
            // 모든 불투명 물체가 완성된 직후 레이더를 가동합니다.
            renderPassEvent = RenderPassEvent.AfterRenderingOpaques
        };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (settings.outlineMaterial != null)
        {
            renderer.EnqueuePass(pass);
        }
    }
}