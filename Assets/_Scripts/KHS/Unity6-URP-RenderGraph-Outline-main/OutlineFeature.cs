using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Experimental.Rendering;

// [통제실] 유니티 에디터의 Renderer Feature 리스트에 등록될 메인 클래스
public class OutlineFeature : ScriptableRendererFeature
{
    // [설정창] 인스펙터에서 기공사가 직접 조절할 다이얼들
    [Serializable]
    public class OutlineSettings
    {
        public LayerMask LayerMask;           // 어떤 레이어(예: ItemOutline)를 가진 물체를 칠할 것인가?
        public Material SilhouetteMaterial;   // 물체의 형체(실루엣)를 뽑아낼 때 쓸 재료
        public Material OutlineMaterial;      // 최종적으로 외곽선을 화면에 그려낼 때 쓸 재료
    }

    public OutlineSettings settings = new OutlineSettings();

    // [제1공정] 실루엣 추출: 외곽선을 그리기 위해 물체의 '모양'만 가상 도화지에 먼저 그리는 과정
    class RenderSilhouettePass : ScriptableRenderPass
    {
        private Material silhouetteMaterial;
        private FilteringSettings filteringSettings;
        // 가상 도화지(텍스처)의 고유 이름표
        private static readonly int SilhouetteTextureID = Shader.PropertyToID("_OutlineRenderTexture");

        public RenderSilhouettePass(LayerMask layerMask, Material mat)
        {
            silhouetteMaterial = mat;
            // 지정된 레이어의 불투명(Opaque) 물체들만 골라내도록 필터 설정
            filteringSettings = new FilteringSettings(RenderQueueRange.opaque, layerMask);
        }

        // 실제 렌더링 명령을 예약하는 곳 (유니티 6 렌더 그래프 방식)
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (silhouetteMaterial == null) return;

            // 카메라 및 렌더링에 필요한 각종 데이터 꾸러미 가져오기
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
            UniversalRenderingData renderingData = frameData.Get<UniversalRenderingData>();
            UniversalLightData lightData = frameData.Get<UniversalLightData>();

            // 1. 가상 도화지(텍스처) 설계도 작성 (화면 크기와 동일하게)
            TextureDesc texDesc = new TextureDesc(cameraData.cameraTargetDescriptor.width, cameraData.cameraTargetDescriptor.height)
            {
                colorFormat = GraphicsFormat.R8G8B8A8_UNorm,
                clearBuffer = true,            // 그리기 전에 깨끗이 비우기
                clearColor = Color.clear,      // 투명하게 초기화
                name = "_OutlineRenderTexture"
            };

            // 2. 가상 도화지 실제 생성
            TextureHandle silhouetteTexture = renderGraph.CreateTexture(texDesc);

            // 3. 래스터(그리기) 패스 추가
            using (var builder = renderGraph.AddRasterRenderPass<PassData>("Render Silhouette", out var passData))
            {
                var sortingCriteria = cameraData.defaultOpaqueSortFlags;
                // 물체를 그릴 때 필요한 설정 (어떤 셰이더를 쓸지 등)
                var drawingSettings = CreateDrawingSettings(new ShaderTagId("UniversalForward"), renderingData, cameraData, lightData, sortingCriteria);
                drawingSettings.overrideMaterial = silhouetteMaterial; // 기공사가 넣은 실루엣 재료로 덮어쓰기

                // 그릴 물체들의 리스트 생성 (컬링 결과물에서 필터링)
                RendererListParams listParams = new RendererListParams(renderingData.cullResults, drawingSettings, filteringSettings);
                passData.rendererList = renderGraph.CreateRendererList(listParams);
                builder.UseRendererList(passData.rendererList);

                // 이 공정의 결과물을 가상 도화지에 기록하겠다고 선언
                builder.SetRenderAttachment(silhouetteTexture, 0, AccessFlags.Write);
                builder.AllowPassCulling(false);

                // 이 공정이 끝나면 다른 공정(셰이더)에서도 이 도화지를 쓸 수 있게 전역 이름표 붙이기
                builder.SetGlobalTextureAfterPass(silhouetteTexture, SilhouetteTextureID);

                // 실제 그리기 실행 명령
                builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                {
                    context.cmd.DrawRendererList(data.rendererList);
                });
            }
        }

        private class PassData
        {
            public RendererListHandle rendererList;
        }
    }

    // [제2공정] 외곽선 도색: 가상 도화지를 분석해서 실제 화면 위에 선을 긋는 과정
    class DrawOutlinePass : ScriptableRenderPass
    {
        private Material outlineMaterial;

        public DrawOutlinePass(Material mat)
        {
            outlineMaterial = mat;
            // 중간 결과물이 필요함을 선언 (Post-processing처럼 화면 위에 덧칠하기 위해)
            requiresIntermediateTexture = true;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (outlineMaterial == null) return;

            // 현재 화면에 그려지고 있는 실제 색상 버퍼 가져오기
            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            TextureHandle activeColorTexture = resourceData.activeColorTexture;

            using (var builder = renderGraph.AddRasterRenderPass<PassData>("Draw Outline", out var passData))
            {
                passData.material = outlineMaterial;

                // 최종 결과물을 현재 화면(activeColorTexture)에 직접 그리겠다고 선언
                builder.SetRenderAttachment(activeColorTexture, 0);
                builder.AllowPassCulling(false);

                // Blitter를 사용하여 화면 전체에 외곽선 재료를 덧칠(Blit)함
                builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                {
                    // 화면 전체 크기로 셰이더 결과물을 쏴주는 장치
                    Blitter.BlitTexture(context.cmd, new Vector2(1, 1), data.material, 0);
                });
            }
        }

        private class PassData
        {
            public Material material;
        }
    }

    private RenderSilhouettePass silhouettePass;
    private DrawOutlinePass outlinePass;

    // [조립] 부품 초기화 및 공정 순서 결정
    public override void Create()
    {
        // 실루엣 추출 패스: 불투명 물체들을 다 그린 직후에 실행
        silhouettePass = new RenderSilhouettePass(settings.LayerMask, settings.SilhouetteMaterial)
        {
            renderPassEvent = RenderPassEvent.AfterRenderingOpaques
        };

        // 외곽선 도색 패스: 하늘(Skybox)까지 다 그린 다음에 최종적으로 덧칠
        outlinePass = new DrawOutlinePass(settings.OutlineMaterial)
        {
            renderPassEvent = RenderPassEvent.AfterRenderingSkybox
        };
    }

    // [배치] 렌더러의 작업 큐에 이 공정들을 끼워넣기
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        // 재료들이 제대로 다 꽂혀있을 때만 공장 가동
        if (settings.SilhouetteMaterial != null && settings.OutlineMaterial != null)
        {
            renderer.EnqueuePass(silhouettePass);
            renderer.EnqueuePass(outlinePass);
        }
    }
}