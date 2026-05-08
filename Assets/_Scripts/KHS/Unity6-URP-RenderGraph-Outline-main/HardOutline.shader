Shader "Custom/HardOutline"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (1, 1, 0, 1)
        _Thickness ("Outline Thickness", Range(0.1, 5)) = 1.0
        _Stencil ("Stencil ID", Int) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Stencil
            {
                Ref [_Stencil]
                Comp Equal    // 물체 안쪽 영역에서만 그리기 (인사이드 방식)
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
            
            // [추가] 실제 카메라의 깊이 정보 시스템 연결
            TEXTURE2D_X_FLOAT(_CameraDepthTexture);
            SAMPLER(sampler_CameraDepthTexture);

            float4 _OutlineColor;
            float _Thickness;

            half4 Frag(Varyings input) : SV_Target {
                float2 uv = input.texcoord;

                // 1. 현재 위치의 깊이값(Z)을 가져와 원근감 계산
                float rawDepth = SAMPLE_TEXTURE2D_X_LOD(_CameraDepthTexture, sampler_CameraDepthTexture, uv, 0).r;
                float depth = LinearEyeDepth(rawDepth, _ZBufferParams);

                // 2. [원근 보정] 멀리 있는 물체는 외곽선을 얇게, 가까우면 굵게 조절
                // 이 수식이 없으면 길쭉한 물체의 끝부분이 회전할 때 따로 놈
                float distanceScale = 1.0 / depth; 
                
                float2 texelSize = float2(1.0 / _ScreenParams.x, 1.0 / _ScreenParams.y);
                // 거리(depth)에 따라 두께를 동적으로 변화시켜 3D 일체감 부여
                float2 offset = texelSize * _Thickness * (distanceScale * 10.0);

                // 3. 실루엣 샘플링 (안쪽 경계선 탐색)
                half centerAlpha = SAMPLE_TEXTURE2D_X(_OutlineRenderTexture, sampler_OutlineRenderTexture, uv).a;
                
                half n1 = SAMPLE_TEXTURE2D_X(_OutlineRenderTexture, sampler_OutlineRenderTexture, uv + float2(0, offset.y)).a;
                half n2 = SAMPLE_TEXTURE2D_X(_OutlineRenderTexture, sampler_OutlineRenderTexture, uv + float2(0, -offset.y)).a;
                half n3 = SAMPLE_TEXTURE2D_X(_OutlineRenderTexture, sampler_OutlineRenderTexture, uv + float2(-offset.x, 0)).a;
                half n4 = SAMPLE_TEXTURE2D_X(_OutlineRenderTexture, sampler_OutlineRenderTexture, uv + float2(offset.x, 0)).a;

                half minNeighbors = min(min(n1, n2), min(n3, n4));
                half edge = centerAlpha * (1.0 - minNeighbors);

                // 4. [보정] 너무 먼 물체에서 선이 깨지는 현상 방지
                edge = saturate(edge * depth);

                return half4(_OutlineColor.rgb, edge * _OutlineColor.a);
            }
            ENDHLSL
        }
    }
}