// 색 세 개를 세로로 섞은 하늘. 위·지평선·아래.
//
// 사진 같은 하늘은 두지 않는다. 기체와 바닥이 계단으로 끊은 평평한 색인데 하늘만
// 사실적이면, 하늘이 배경이 아니라 다른 게임의 화면처럼 붙어 있게 된다. 색 세 개면
// 톤을 기체와 같은 자리에서 고를 수 있다.
//
// 해나 별은 없다. 필요해지면 그때 붙인다.
Shader "Adler/Skybox"
{
    Properties
    {
        _TopColor ("하늘 꼭대기", Color) = (0.24, 0.45, 0.72, 1)
        _HorizonColor ("지평선", Color) = (0.78, 0.88, 0.95, 1)
        _BottomColor ("아래", Color) = (0.32, 0.38, 0.45, 1)

        // 지평선 띠가 위로 얼마나 넓게 번지는지. 클수록 띠가 얇아진다.
        _TopSharpness ("위쪽 경계의 조임", Range(0.5, 8)) = 1.5

        // 지평선 띠가 아래로 얼마나 넓게 번지는지. 아래는 대개 바닥이 가리므로
        // 위보다 좁게 잡아도 티가 안 난다.
        _BottomSharpness ("아래쪽 경계의 조임", Range(0.5, 8)) = 2.5
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Background"
            "RenderType" = "Background"
            "PreviewType" = "Skybox"
        }

        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _TopColor;
                half4 _HorizonColor;
                half4 _BottomColor;
                half  _TopSharpness;
                half  _BottomSharpness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 direction  : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output = (Varyings)0;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);

                // 스카이박스 메시는 카메라를 감싼 상자라, 정점의 오브젝트 좌표가
                // 곧 바라보는 방향이다.
                output.direction = input.positionOS.xyz;

                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // 위가 +1, 지평선이 0, 바로 아래가 -1.
                float height = normalize(input.direction).y;

                // 지평선에서 시작해 위로 갈수록 꼭대기 색으로, 아래로 갈수록
                // 아래 색으로 간다. 조임은 지평선 띠의 폭이다 — 거듭제곱이 클수록
                // 지평선 색이 좁게 남는다.
                half above = pow(saturate(height), _TopSharpness);
                half below = pow(saturate(-height), _BottomSharpness);

                half3 color = lerp(_HorizonColor.rgb, _TopColor.rgb, above);
                color = lerp(color, _BottomColor.rgb, below);

                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}
