Shader "FusionPunk/TacticalOverlay"
{
    Properties
    {
        [HDR] _GlowColor("발광 색상 (Glow Color)", Color) = (0, 0.8, 1, 1) // 창백한 네온 블루 추천
        _PulseSpeed("맥동 속도 (Pulse Speed)", Range(0.0, 10.0)) = 3.0
        _ScanSpeed("스캔라인 속도 (Scan Speed)", Range(0.0, 10.0)) = 5.0
    }
    SubShader
    {
        // 투명하게 덧칠하는 코팅 레이어
        Tags { "RenderType"="Transparent" "Queue"="Transparent+1" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Blend One One // Additive (기존 물체 색상 위에 빛을 더함)
            ZWrite Off    // 깊이 버퍼에 쓰지 않음 (다른 물체를 가리지 않음)
            ZTest LEqual  // 본체 표면에 완벽하게 밀착
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionOS_pass : TEXCOORD0; // 로컬 3D 좌표 전달
            };

            float4 _GlowColor;
            float _PulseSpeed;
            float _ScanSpeed;

            Varyings Vert(Attributes input)
            {
                Varyings output;
                // 크기 확장(Offset)이 전혀 없는 순정 좌표 변환
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                
                // 3D 스캔라인을 위해 로컬 좌표를 넘겨줌
                output.positionOS_pass = input.positionOS.xyz; 
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // 1. 베이스 맥동 (숨쉬는 듯한 부드러운 깜빡임)
                float pulse = (sin(_Time.y * _PulseSpeed) * 0.5 + 0.5) * 0.2 + 0.1;

                // 2. 스캔라인 (Y축 방향으로 기계 표면을 훑고 지나가는 홀로그램 선)
                float scan = frac(input.positionOS_pass.y * 10.0 - _Time.y * _ScanSpeed);
                scan = step(0.8, scan) * 0.4; // 선명하고 얇은 띠 형성

                // 최종 출력: 기공사가 선택한 색상 * (맥동 + 스캔라인)
                return _GlowColor * (pulse + scan);
            }
            ENDHLSL
        }
    }
}