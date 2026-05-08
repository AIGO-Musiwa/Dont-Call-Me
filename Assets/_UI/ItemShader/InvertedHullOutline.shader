Shader "Custom/InvertedHullOutline"
{
    Properties
    {
        _OutlineColor ("외곽선 색상 (Outline Color)", Color) = (1, 1, 0, 1)
        // 0.01~0.1 사이의 아주 작은 값으로 부풀려야 자연스러움
        _OutlineThickness ("외곽선 두께 (Thickness)", Range(0.001, 0.1)) = 0.01 
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        
        Pass
        {
            Name "Outline"
            
            // [핵심 기계 장치] 앞면을 렌더링하지 않고 '뒷면'만 렌더링함
            // 이렇게 하면 본체가 앞을 가려주고 삐져나온 테두리만 보이게 됨
            Cull Front
            
            // 본체 뒤에 가려진 부분은 안 그리고, 본체보다 뒤에 있는 배경보다는 앞에 그리게 설정
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL; // 물체 표면이 바라보는 수직 방향
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            float4 _OutlineColor;
            float _OutlineThickness;

            Varyings Vert(Attributes input)
            {
                Varyings output;
                
                // [부풀리기 공정] 정점(Vertex)을 표면이 바라보는 방향(Normal)으로 두께만큼 밀어냄
                float3 expandedPosition = input.positionOS.xyz + (input.normalOS * _OutlineThickness);
                
                // 밀어낸 3D 좌표를 2D 카메라 화면 좌표로 변환
                output.positionCS = TransformObjectToHClip(expandedPosition);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // 기공사가 선택한 색상 출력
                return _OutlineColor;
            }
            ENDHLSL
        }
    }
}