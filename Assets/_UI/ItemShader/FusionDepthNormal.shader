Shader "Custom/FusionDepthNormal"
{
    Properties
    {
        _OutlineColor("외곽선 색상 (Color)", Color) = (1, 1, 0, 1)
        _DepthThreshold("깊이 감도 (낮을수록 얇고 민감함)", Range(0, 1)) = 0.1
        _NormalThreshold("각도 감도 (낮을수록 얇고 민감함)", Range(0, 1)) = 0.4
    }
    SubShader
    {
        // 불투명 물체가 그려진 직후, 그 표면 위에 투명하게 덧칠하는 코팅 레이어
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off     // 깊이를 건드리지 않아 덩치가 커지지 않음
            ZTest LEqual   // 본체 표면에 완벽하게 밀착
            Cull Back      // 앞면에만 그림

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

            struct Attributes {
                float4 positionOS : POSITION;
            };

            struct Varyings {
                float4 positionCS : SV_POSITION;
                float4 screenPos : TEXCOORD0;
            };

            float4 _OutlineColor;
            float _DepthThreshold;
            float _NormalThreshold;

            Varyings Vert(Attributes input) {
                Varyings output;
                // [핵심] 정점을 0.001mm도 부풀리지 않음! 100% 원본 크기 유지
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.screenPos = ComputeScreenPos(output.positionCS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target {
                // 현재 내 물체의 화면상 위치(UV) 가져오기
                float2 uv = input.screenPos.xy / input.screenPos.w;
                float2 texelSize = _ScreenSize.zw;

                // レーダー(레이더) 가동: 십자 방향으로 깊이와 노멀(각도) 스캔
                float depthCenter = Linear01Depth(SampleSceneDepth(uv), _ZBufferParams);
                float3 normalCenter = SampleSceneNormals(uv);

                float depthUp = Linear01Depth(SampleSceneDepth(uv + float2(0, texelSize.y)), _ZBufferParams);
                float3 normalUp = SampleSceneNormals(uv + float2(0, texelSize.y));

                float depthRight = Linear01Depth(SampleSceneDepth(uv + float2(texelSize.x, 0)), _ZBufferParams);
                float3 normalRight = SampleSceneNormals(uv + float2(texelSize.x, 0));

                // 경계선 판독 로직 (깊이나 표면 각도가 급격히 꺾이는 곳을 찾음)
                float depthDiff = abs(depthCenter - depthUp) + abs(depthCenter - depthRight);
                float edgeDepth = step(_DepthThreshold * 0.01, depthDiff);

                float normalDiff = distance(normalCenter, normalUp) + distance(normalCenter, normalRight);
                float edgeNormal = step(_NormalThreshold, normalDiff);

                float edge = saturate(edgeDepth + edgeNormal);

                // 테두리가 발견된 곳만 잉크를 칠하고, 내부는 완전히 투명하게(알파 0) 처리
                return half4(_OutlineColor.rgb, edge * _OutlineColor.a);
            }
            ENDHLSL
        }
    }
}