using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Experimental.Rendering;

public class OutlineFeature : ScriptableRendererFeature
{
    [Serializable]
    public class OutlineSettings
    {
        public LayerMask LayerMask;
        public Material SilhouetteMaterial;
        public Material OutlineMaterial;
    }

    public OutlineSettings settings = new OutlineSettings();

    class RenderSilhouettePass : ScriptableRenderPass
    {
        private Material silhouetteMaterial;
        private FilteringSettings filteringSettings;
        private static readonly int SilhouetteTextureID = Shader.PropertyToID("_OutlineRenderTexture");

        public RenderSilhouettePass(LayerMask layerMask, Material mat)
        {
            silhouetteMaterial = mat;
            filteringSettings = new FilteringSettings(RenderQueueRange.opaque, layerMask);
        }

        private class PassData
        {
            public RendererListHandle rendererList;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (silhouetteMaterial == null) return;

            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
            UniversalRenderingData renderingData = frameData.Get<UniversalRenderingData>();
            UniversalLightData lightData = frameData.Get<UniversalLightData>();

            TextureDesc texDesc = new TextureDesc(cameraData.cameraTargetDescriptor.width, cameraData.cameraTargetDescriptor.height)
            {
                colorFormat = GraphicsFormat.R8G8B8A8_UNorm,
                clearBuffer = true,
                clearColor = Color.clear,
                name = "_OutlineRenderTexture"
            };
            TextureHandle silhouetteTexture = renderGraph.CreateTexture(texDesc);

            using (var builder = renderGraph.AddRasterRenderPass<PassData>("Render Silhouette", out var passData))
            {
                var sortingCriteria = cameraData.defaultOpaqueSortFlags;
                var drawingSettings = CreateDrawingSettings(new ShaderTagId("UniversalForward"), renderingData, cameraData, lightData, sortingCriteria);
                drawingSettings.overrideMaterial = silhouetteMaterial;

                RendererListParams listParams = new RendererListParams(renderingData.cullResults, drawingSettings, filteringSettings);
                passData.rendererList = renderGraph.CreateRendererList(listParams);
                builder.UseRendererList(passData.rendererList);

                builder.SetRenderAttachment(silhouetteTexture, 0, AccessFlags.Write);
                builder.AllowPassCulling(false);

                builder.SetGlobalTextureAfterPass(silhouetteTexture, SilhouetteTextureID);

                builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                {
                    context.cmd.DrawRendererList(data.rendererList);
                });
            }
        }
    }

    class DrawOutlinePass : ScriptableRenderPass
    {
        private Material outlineMaterial;

        public DrawOutlinePass(Material mat)
        {
            outlineMaterial = mat;
            requiresIntermediateTexture = true; 
        }

        private class PassData
        {
            public Material material;
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
                builder.AllowPassCulling(false);

                builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                {
                    Blitter.BlitTexture(context.cmd, new Vector2(1, 1), data.material, 0);
                });
            }
        }
    }

    private RenderSilhouettePass silhouettePass;
    private DrawOutlinePass outlinePass;

    public override void Create()
    {
        silhouettePass = new RenderSilhouettePass(settings.LayerMask, settings.SilhouetteMaterial)
        {
            renderPassEvent = RenderPassEvent.AfterRenderingOpaques
        };

        outlinePass = new DrawOutlinePass(settings.OutlineMaterial)
        {
            renderPassEvent = RenderPassEvent.AfterRenderingSkybox
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