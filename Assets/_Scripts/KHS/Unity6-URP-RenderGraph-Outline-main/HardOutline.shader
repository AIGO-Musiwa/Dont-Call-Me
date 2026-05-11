Shader "Custom/HardOutline"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (1, 1, 0, 1)
        _Thickness ("Outline Thickness", Range(0.1, 10)) = 1.0
        _Stencil ("Stencil ID", Int) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            // [핵심 1: 침범 허용 방어막]
            // 물체 영역(1번 도장)과 '같은(Equal)' 곳, 즉 물체의 안쪽에만 그립니다!
            Stencil
            {
                Ref [_Stencil]
                Comp Equal 
                Pass Keep
            }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off 
            ZTest Always 
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert 
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            TEXTURE2D_X(_OutlineRenderTexture);
            SAMPLER(sampler_OutlineRenderTexture);
            
            TEXTURE2D_X_FLOAT(_CameraDepthTexture);
            SAMPLER(sampler_CameraDepthTexture);

            float4 _OutlineColor;
            float _Thickness;

            half4 Frag(Varyings input) : SV_Target {
                float2 uv = input.texcoord;

                // 1. 원근 보정 (선 굵기가 카메라 거리에 맞게 자연스러워짐)
                float rawDepth = SAMPLE_TEXTURE2D_X_LOD(_CameraDepthTexture, sampler_CameraDepthTexture, uv, 0).r;
                float depth = LinearEyeDepth(rawDepth, _ZBufferParams);
                float distanceScale = 1.0 / depth; 
                
                float2 texelSize = float2(1.0 / _ScreenParams.x, 1.0 / _ScreenParams.y);
                float2 offset = texelSize * _Thickness * (distanceScale * 10.0);

                // 2. 안쪽 테두리 정밀 스캔 (8방향)
                // 나는 지금 무조건 물체 안쪽(center = 1)에 있음
                half center = SAMPLE_TEXTURE2D_X(_OutlineRenderTexture, sampler_OutlineRenderTexture, uv).a;
                
                half n1 = SAMPLE_TEXTURE2D_X(_OutlineRenderTexture, sampler_OutlineRenderTexture, uv + float2(0, offset.y)).a;
                half n2 = SAMPLE_TEXTURE2D_X(_OutlineRenderTexture, sampler_OutlineRenderTexture, uv + float2(0, -offset.y)).a;
                half n3 = SAMPLE_TEXTURE2D_X(_OutlineRenderTexture, sampler_OutlineRenderTexture, uv + float2(-offset.x, 0)).a;
                half n4 = SAMPLE_TEXTURE2D_X(_OutlineRenderTexture, sampler_OutlineRenderTexture, uv + float2(offset.x, 0)).a;
                half n5 = SAMPLE_TEXTURE2D_X(_OutlineRenderTexture, sampler_OutlineRenderTexture, uv + float2(-offset.x, offset.y)).a;
                half n6 = SAMPLE_TEXTURE2D_X(_OutlineRenderTexture, sampler_OutlineRenderTexture, uv + float2(offset.x, offset.y)).a;
                half n7 = SAMPLE_TEXTURE2D_X(_OutlineRenderTexture, sampler_OutlineRenderTexture, uv + float2(-offset.x, -offset.y)).a;
                half n8 = SAMPLE_TEXTURE2D_X(_OutlineRenderTexture, sampler_OutlineRenderTexture, uv + float2(offset.x, -offset.y)).a;

                // [핵심 2: 인사이드 판독]
                // 내 주변 8칸을 탐색해서, 단 한 칸이라도 허공(0)이 있다면? 
                // -> "아, 내가 물체의 맨 바깥쪽 살갗(테두리)이구나!" 하고 판단함.
                half minNeighbors = min(min(min(n1, n2), min(n3, n4)), min(min(n5, n6), min(n7, n8)));
                
                // 중심(1) - 주변 최솟값(허공이 있으면 0) = 1 (테두리 칠함!)
                // 중심(1) - 주변 최솟값(전부 물체면 1) = 0 (투명하게 놔둠)
                half edge = center - minNeighbors;

                return half4(_OutlineColor.rgb, edge * _OutlineColor.a);
            }
            ENDHLSL
        }
    }
}