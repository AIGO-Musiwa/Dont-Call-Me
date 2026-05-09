Shader "Custom/MarkStencil"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "MarkStencil"
            ColorMask 0   // 색상을 전혀 칠하지 않음 (투명)
            ZWrite Off    // 깊이 버퍼도 건드리지 않음
            ZTest LEqual  // 물체가 보이는 면에만 작동

            // [핵심 장치] 1번 도장 쾅!
            Stencil
            {
                Ref 1
                Comp Always
                Pass Replace
            }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target { 
                return half4(0,0,0,0); 
            }
            ENDHLSL
        }
    }
}