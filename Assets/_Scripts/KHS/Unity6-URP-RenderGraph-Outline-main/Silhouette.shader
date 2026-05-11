Shader "Hidden/OutlineSilhouette"
{
    Properties
    {
        _Stencil ("Stencil ID", Int) = 1
        // [새로운 부품] 수축 강도 조절 레버 (값이 클수록 도장이 작아짐)
        _Shrink ("Shrink Amount", Range(0.0, 0.05)) = 0.005 
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Stencil
            {
                Ref [_Stencil]
                Comp Always
                Pass Replace
            }

            ZWrite On
            ZTest LEqual  
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL; // [추가] 표면이 바라보는 방향 센서
            };

            struct Varyings {
                float4 positionCS : SV_POSITION;
            };

            float _Shrink;

            Varyings Vert(Attributes input) {
                Varyings output;
                
                // [핵심 기술: 안쪽으로 뼈대 깎기]
                // 밖으로 부풀리는 게(+) 아니라, 노멀의 반대 방향(-)으로 정점을 당겨서 수축시킴
                float3 shrunkenPos = input.positionOS.xyz - (input.normalOS * _Shrink);
                
                output.positionCS = TransformObjectToHClip(shrunkenPos);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target {
                return half4(1, 1, 1, 1);
            }
            ENDHLSL
        }
    }
}