Shader "Hidden/OutlineSilhouette"
{
    Properties
    {
        _Stencil ("Stencil ID", Int) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            // [스텐실] 물체 자리에 번호를 새김
            Stencil
            {
                Ref [_Stencil]
                Comp Always
                Pass Replace
            }

            // [심도] 실제 물체의 깊이와 똑같이 테스트함 (위치 어긋남 방지)
            ZWrite On
            ZTest LEqual  
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes {
                float4 positionOS : POSITION;
            };

            struct Varyings {
                float4 positionCS : SV_POSITION;
            };

            Varyings Vert(Attributes input) {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target {
                // 알파값을 1로 채워 실루엣 영역임을 표시
                return half4(1, 1, 1, 1);
            }
            ENDHLSL
        }
    }
}