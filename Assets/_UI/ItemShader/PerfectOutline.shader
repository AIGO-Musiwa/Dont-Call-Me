Shader "FusionPunk/PerfectOutline"
{
    Properties
    {
        _OutlineColor("외곽선 색상", Color) = (1, 1, 0, 1)
        // 1.0 ~ 3.0 사이의 아주 얇은 픽셀 두께를 권장
        _OutlineWidth("외곽선 두께", Range(0.0, 10.0)) = 1.5 
    }
    SubShader
    {
        // 불투명 물체들이 다 그려진 직후(Geometry+1)에 그림
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry+1" }

        Pass
        {
            Name "Outline"
            Cull Front // [핵심] 뒷면만 그려서 껍데기 역할 수행
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            float4 _OutlineColor;
            float _OutlineWidth;

            Varyings Vert(Attributes input)
            {
                Varyings output;

                // 1. 3D 공간의 위치와 수직 방향(Normal) 데이터를 가져옴
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);

                // 2. 화면(Clip Space) 기준으로 수직 방향을 변환
                float3 normalCS = TransformWorldToHClipDir(normalInput.normalWS);
                
                // 3. 픽셀 단위로 얼마나 밀어낼지 계산 (스케일 왜곡 방지)
                float2 offset = normalize(normalCS.xy) * (_OutlineWidth * 0.001);

                output.positionCS = vertexInput.positionCS;
                
                // 4. 카메라와의 거리(w)에 비례해서 두께를 보정 -> 멀어도 얇고 일정하게!
                output.positionCS.xy += offset * output.positionCS.w;

                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                return _OutlineColor; // 기공사가 세팅한 형광 도료 출력
            }
            ENDHLSL
        }
    }
}