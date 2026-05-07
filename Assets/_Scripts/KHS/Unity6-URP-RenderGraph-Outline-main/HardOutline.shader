Shader "Custom/HardOutline"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (1, 0, 0, 1)
        _Thickness ("Outline Thickness", Range(1, 10)) = 1.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off ZTest Always Cull Off

            HLSLPROGRAM
            #pragma vertex Vert 
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            TEXTURE2D_X(_OutlineRenderTexture);
            SAMPLER(sampler_OutlineRenderTexture);

            float4 _OutlineColor;
            float _Thickness;

            half4 Frag(Varyings input) : SV_Target {
                float2 uv = input.texcoord;

                half centerAlpha = SAMPLE_TEXTURE2D_X(_OutlineRenderTexture, sampler_OutlineRenderTexture, uv).a;

                float2 texelSize = float2(1.0 / _ScreenParams.x, 1.0 / _ScreenParams.y);
                float referenceHeight = 1080.0;
                float scaleFactor = _ScreenParams.y / referenceHeight;
                float scaledThickness = _Thickness * scaleFactor;
                float2 offset = texelSize * scaledThickness;

                half up = SAMPLE_TEXTURE2D_X(_OutlineRenderTexture, sampler_OutlineRenderTexture, uv + float2(0, offset.y)).a;
                half down = SAMPLE_TEXTURE2D_X(_OutlineRenderTexture, sampler_OutlineRenderTexture, uv + float2(0, -offset.y)).a;
                half left = SAMPLE_TEXTURE2D_X(_OutlineRenderTexture, sampler_OutlineRenderTexture, uv + float2(-offset.x, 0)).a;
                half right = SAMPLE_TEXTURE2D_X(_OutlineRenderTexture, sampler_OutlineRenderTexture, uv + float2(offset.x, 0)).a;

                half topLeft = SAMPLE_TEXTURE2D_X(_OutlineRenderTexture, sampler_OutlineRenderTexture, uv + float2(-offset.x, offset.y)).a;
                half topRight = SAMPLE_TEXTURE2D_X(_OutlineRenderTexture, sampler_OutlineRenderTexture, uv + float2(offset.x, offset.y)).a;
                half bottomLeft = SAMPLE_TEXTURE2D_X(_OutlineRenderTexture, sampler_OutlineRenderTexture, uv + float2(-offset.x, -offset.y)).a;
                half bottomRight = SAMPLE_TEXTURE2D_X(_OutlineRenderTexture, sampler_OutlineRenderTexture, uv + float2(offset.x, -offset.y)).a;

                half maxNeighbors = max(max(up, down), max(left, right));
                half maxDiagonals = max(max(topLeft, topRight), max(bottomLeft, bottomRight));
                half dilated = max(centerAlpha, max(maxNeighbors, maxDiagonals));

                half edge = saturate(dilated - centerAlpha);

                return half4(_OutlineColor.rgb, edge * _OutlineColor.a);
            }
            ENDHLSL
        }
    }
}