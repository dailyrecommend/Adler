// 세상에 놓이지만 무엇에도 완전히 가려지지는 않는 표식.
//
// 착탄 예측처럼 조준을 돕는 표식은 지면에 눕되 늘 보여야 한다. 그런데 깊이 판정을
// 그냥 끄면 "저 뒤에 있다"는 정보까지 사라져서, 언덕 너머에 떨어지는 것과 눈앞에
// 떨어지는 것이 똑같아 보인다.
//
// 그래서 두 번 그린다. 가려진 부분은 흐리게, 드러난 부분은 진하게. 기체에 가린
// 자리도 비쳐 보이면서 거리감은 남는다.
Shader "Adler/HUD Marker"
{
    Properties
    {
        [MainTexture] _BaseMap ("Texture", 2D) = "white" {}
        [MainColor] _BaseColor ("Color", Color) = (1, 1, 1, 1)
        _OccludedColor ("Occluded Color", Color) = (1, 1, 1, 0.3)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
        }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        struct Attributes
        {
            float4 positionOS : POSITION;
            float2 uv         : TEXCOORD0;
        };

        struct Varyings
        {
            float4 positionHCS : SV_POSITION;
            float2 uv          : TEXCOORD0;
        };

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half4 _OccludedColor;
        CBUFFER_END

        Varyings Vertex(Attributes input)
        {
            Varyings output;
            output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
            output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
            return output;
        }

        half4 SampleTinted(float2 uv, half4 tint)
        {
            return SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv) * tint;
        }
        ENDHLSL

        // 무언가에 가려진 부분. 먼저 그려서 아래에 깔린다.
        Pass
        {
            Name "Occluded"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            ZTest Greater
            ZWrite Off
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment FragmentOccluded

            half4 FragmentOccluded(Varyings input) : SV_Target
            {
                return SampleTinted(input.uv, _OccludedColor);
            }
            ENDHLSL
        }

        // 그대로 보이는 부분.
        Pass
        {
            Name "Visible"
            Tags { "LightMode" = "UniversalForward" }

            ZTest LEqual
            ZWrite Off
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment FragmentVisible

            half4 FragmentVisible(Varyings input) : SV_Target
            {
                return SampleTinted(input.uv, _BaseColor);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
