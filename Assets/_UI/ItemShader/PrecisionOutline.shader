Shader "FusionPunk/PrecisionOutline"
{
    Properties
    {
        _OutlineColor("외곽선 색상", Color) = (1, 1, 0, 1)
        _DepthThreshold("깊이 감도", Range(0, 1)) = 0.1
        _NormalThreshold("각도 감도", Range(0, 1)) = 0.4
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            // [잠금장치] 스텐실 도장이 찍힌 곳(물체 본체)에서만 스캐너 가동
            Stencil {
                Ref 1
                Comp Equal
            }

            ZWrite Off ZTest Always Cull Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

            float4 _OutlineColor;
            float _DepthThreshold;
            float _NormalThreshold;

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                float2 texelSize = _ScreenSize.zw;

                // 1. 십자 모양으로 주변 픽셀 샘플링 (정밀 스캔)
                float depthCenter = Linear01Depth(SampleSceneDepth(uv), _ZBufferParams);
                float3 normalCenter = SampleSceneNormals(uv);

                float depthUp = Linear01Depth(SampleSceneDepth(uv + float2(0, texelSize.y)), _ZBufferParams);
                float3 normalUp = SampleSceneNormals(uv + float2(0, texelSize.y));

                float depthRight = Linear01Depth(SampleSceneDepth(uv + float2(texelSize.x, 0)), _ZBufferParams);
                float3 normalRight = SampleSceneNormals(uv + float2(texelSize.x, 0));

                // 2. 깊이 차이 계산 (경계선 검출)
                float depthDiff = abs(depthCenter - depthUp) + abs(depthCenter - depthRight);
                float edgeDepth = step(_DepthThreshold * 0.01, depthDiff);

                // 3. 노멀(각도) 차이 계산 (내부 모서리 검출)
                float normalDiff = distance(normalCenter, normalUp) + distance(normalCenter, normalRight);
                float edgeNormal = step(_NormalThreshold, normalDiff);

                // 4. 최종 외곽선 강도 (깊이 혹은 노멀 차이가 큰 곳)
                float edge = saturate(edgeDepth + edgeNormal);

                return half4(_OutlineColor.rgb, edge * _OutlineColor.a);
            }
            ENDHLSL
        }
    }
}