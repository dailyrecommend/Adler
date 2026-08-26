// 빛을 계단으로 끊어 평평하게 칠한다.
//
// 사실적인 음영은 곡면을 곡면으로 보이게 하려고 밝기를 연속으로 흘리는데, 그러면
// 면과 면의 경계가 흐려져서 기체의 각진 형태가 뭉개진다. 몇 단계로 끊어버리면
// 경계가 선으로 남아, 어디가 꺾인 자리인지가 멀리서도 읽힌다.
//
// 하는 일이 그것뿐이다. 반사광도 테두리 빛도 두지 않는다 — 계단으로 끊은 면 위에
// 그런 것이 얹히면 무엇이 형태고 무엇이 빛인지가 흐려지고, 정작 살리려던 경계선이
// 그 아래 묻힌다. 필요해지면 그때 붙이는 편이 낫다.
//
// URP 전용이다. 앞면 조명 외에 그림자 드리우기·깊이·깊이+법선 패스를 함께 갖는데,
// 그게 없으면 이 재질을 쓴 것만 그림자를 안 만들고 화면 효과에서도 빠진다.
Shader "Adler/Toon"
{
    Properties
    {
        [MainTexture] _BaseMap ("바탕 텍스처", 2D) = "white" {}
        [MainColor]   _BaseColor ("바탕 색", Color) = (1, 1, 1, 1)

        // 켜면 오브젝트를 키운 만큼 텍스처가 더 반복된다. 격자 무늬를 바닥에 깔고
        // 바닥을 150배로 늘려도 격자 한 칸의 실제 크기는 그대로다 — 끄면 무늬가
        // 바닥과 함께 늘어나 칸 하나가 지도만 해진다.
        [Toggle(_SCALE_TILING)] _ScaleTiling ("크기 따라 타일 반복", Float) = 0

        // 위 토글이 켜졌을 때 미터당 몇 번 반복할지. 1이면 1m에 한 칸,
        // 0.1이면 10m에 한 칸이다. 칸 크기 = 1m ÷ 이 값.
        _TileDensity ("미터당 반복 수", Float) = 1

        [Header(Shading)]
        [Space(4)]
        // 경계선의 개수다. 3이면 밝기는 0·1/3·2/3·1의 네 가지가 된다.
        _Steps ("명암 경계 수", Range(1, 8)) = 3
        _Softness ("단계 경계의 무름", Range(0, 0.5)) = 0.02

        // 색이 아니라 배율이라 Color가 아니라 Vector다. Color로 두면 리니어 공간에서
        // sRGB 변환을 거쳐, 인스펙터에 적은 0.35가 셰이더에는 0.1로 들어온다.
        // 1이면 그늘이 없고, 낮출수록 짙어진다. 살짝 푸르게 두면 하늘 아래로 읽힌다.
        _ShadowColor ("그늘 배율 (RGB)", Vector) = (0.35, 0.4, 0.55, 1)
        _ShadowRange ("그늘의 넓이", Range(0, 1)) = 0.5
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4  _BaseColor;
            float4 _ShadowColor;
            float  _TileDensity;
            float  _Steps;
            float  _Softness;
            float  _ShadowRange;
        CBUFFER_END
        ENDHLSL

        // ------------------------------------------------------------------
        // 화면에 그리는 패스
        // ------------------------------------------------------------------
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment

            #pragma shader_feature_local_vertex _SCALE_TILING

            // 그림자 종류는 서로 배타적이라 한 세트다. 따로 선언하면 메인 그림자 없이
            // 캐스케이드만 켜진 뜻 없는 변형이 생기고, 변형 수는 그만큼 배로 뛴다.
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH

            // 정점 단위 추가 광원은 쓰지 않는다. 계단으로 끊는 음영에 정점에서 보간된
            // 빛을 섞으면 경계가 흐물거려서, 선으로 남기려던 것이 남지 않는다.
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS

            // Unity 6의 기본 경로. 이게 없으면 클러스터 목록이 채워지지 않아
            // 추가 광원이 하나도 안 들어온다. (_FORWARD_PLUS는 6.1에서 폐기됐다)
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP

            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS   : TEXCOORD2;
                float  fogFactor  : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // 0~1을 정해진 수의 계단으로 끊는다.
            //
            // 경계 폭을 gradient에서 재는 것이 중요하다. 끊을 값에는 그림자가 곱해져
            // 있는데, 그림자 경계에서는 그 값이 한 픽셀 만에 확 꺾여 fwidth가 폭발한다.
            // 폭이 반 칸을 넘으면 smoothstep이 사실상 상수가 되어 그 자리만 중간 밝기로
            // 뭉개진다 — 선으로 남기려던 경계가 하필 그림자 경계에서 깨지는 것이다.
            // 기울기는 그림자가 안 섞인 각도에서만 잰다.
            float Posterize(float value, float gradient, float steps, float softness)
            {
                float scaled = saturate(value) * steps;
                float level = floor(scaled);
                float edge = scaled - level;
                float width = max(abs(fwidth(gradient)) * steps * 0.5, softness);

                return (level + smoothstep(0.5 - width, 0.5 + width, edge)) / steps;
            }

            /// 계단으로 끊지 않는 평범한 램버트. 추가 광원이 쓴다.
            half3 Sunlight(Light light, half3 albedo, float3 normalWS)
            {
                return albedo * light.color
                     * saturate(dot(normalWS, light.direction))
                     * (light.distanceAttenuation * light.shadowAttenuation);
            }

            Varyings Vertex(Attributes input)
            {
                Varyings output = (Varyings)0;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normals = GetVertexNormalInputs(input.normalOS);

                output.positionCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                output.normalWS = normals.normalWS;

                float2 uv = input.uv;

                // 오브젝트의 크기를 변환 행렬에서 읽어 UV에 곱한다. 150배로 키우면
                // 무늬도 150배 더 반복되므로, 화면에 보이는 칸 크기는 변하지 않는다.
                // 크기는 축마다 따로 읽는다 — 한쪽으로만 길게 늘여도 칸은 네모다.
                //
                // 평면과 지형은 UV가 X·Z를 따라가므로 그 두 축을 쓴다. 쿼드처럼
                // X·Y를 쓰는 메시에는 세로 크기가 안 걸리는데, 그런 것은 대개
                // 화면 요소라 이 토글을 켤 일이 없다.
                #ifdef _SCALE_TILING
                    uv *= _TileDensity * float2(
                        length(unity_ObjectToWorld._m00_m10_m20),
                        length(unity_ObjectToWorld._m02_m12_m22));
                #endif

                output.uv = TRANSFORM_TEX(uv, _BaseMap);
                output.fogFactor = ComputeFogFactor(positions.positionCS.z);

                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;

                float3 normalWS = normalize(input.normalWS);

                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                // 그림자를 밝기에 곱해 넣는다. 따로 칠하면 등진 면과 그림자가 진 면이
                // 서로 다른 색이 되어, 같은 어둠인데 둘로 보인다.
                float ndotl = dot(normalWS, mainLight.direction) * 0.5 + 0.5;
                float lit = Posterize(
                    ndotl * mainLight.shadowAttenuation - (_ShadowRange - 0.5),
                    ndotl,
                    _Steps,
                    _Softness);

                // 가장 밝은 단계는 반드시 1이다. 여기에 무엇을 더하거나 곱하면 흰색이
                // 흰색으로 안 나오는데, 그건 재질이 아니라 조명이 틀린 것처럼 보인다.
                // 그늘 배율은 어두운 쪽에만 닿는다.
                half3 shade = lerp(_ShadowColor.rgb, half3(1.0, 1.0, 1.0), lit);
                half3 color = albedo.rgb * mainLight.color * shade;

                // 추가 광원은 단계로 끊지 않는다. 점광원은 대개 작고 가까이 있어서,
                // 거기까지 끊으면 계단이 겹쳐 얼룩으로 보인다.
                #if defined(_ADDITIONAL_LIGHTS)
                    // 클러스터 경로의 LIGHT_LOOP_BEGIN은 화면을 격자로 나눠 그 칸에 닿는
                    // 광원만 도는데, 어느 칸인지는 inputData에서 읽는다. 이것을 채우지
                    // 않으면 매크로가 엉뚱한 칸을 뒤져 광원이 통째로 빠진다.
                    InputData inputData = (InputData)0;
                    inputData.positionWS = input.positionWS;
                    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);

                    // 클러스터 경로는 추가 디렉셔널을 배열 앞쪽에 몰아두고, 격자 순회는
                    // 그 뒤부터 시작한다. 앞쪽은 화면 위치와 무관하므로 따로 돌아야 한다.
                    #if USE_CLUSTER_LIGHT_LOOP
                        [loop]
                        for (uint head = 0;
                             head < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS);
                             head++)
                        {
                            color += Sunlight(GetAdditionalLight(head, input.positionWS, half4(1, 1, 1, 1)),
                                              albedo.rgb, normalWS);
                        }
                    #endif

                    uint count = GetAdditionalLightsCount();

                    LIGHT_LOOP_BEGIN(count)
                        color += Sunlight(GetAdditionalLight(lightIndex, input.positionWS, half4(1, 1, 1, 1)),
                                          albedo.rgb, normalWS);
                    LIGHT_LOOP_END
                #endif

                color = MixFog(color, input.fogFactor);

                return half4(color, albedo.a);
            }
            ENDHLSL
        }

        // ------------------------------------------------------------------
        // 그림자를 드리우는 패스. 이게 없으면 이 재질만 그림자를 만들지 않는다.
        // ------------------------------------------------------------------
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowVertex
            #pragma fragment ShadowFragment

            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings ShadowVertex(Attributes input)
            {
                Varyings output = (Varyings)0;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif

                // 빛 쪽으로 살짝 밀어 자기 그림자에 자기가 걸리는 얼룩을 없앤다.
                float4 positionCS = TransformWorldToHClip(
                    ApplyShadowBias(positionWS, normalWS, lightDirectionWS));

                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif

                output.positionCS = positionCS;
                return output;
            }

            half4 ShadowFragment(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        // ------------------------------------------------------------------
        // 깊이만 그리는 패스. 화면 효과와 소프트 파티클이 이것을 읽는다.
        // ------------------------------------------------------------------
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex DepthVertex
            #pragma fragment DepthFragment
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings DepthVertex(Attributes input)
            {
                Varyings output = (Varyings)0;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 DepthFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return 0;
            }
            ENDHLSL
        }

        // ------------------------------------------------------------------
        // 깊이와 법선을 함께 그리는 패스. SSAO와 외곽선 계열이 이것을 읽는다.
        // ------------------------------------------------------------------
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On

            HLSLPROGRAM
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings DepthNormalsVertex(Attributes input)
            {
                Varyings output = (Varyings)0;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);

                return output;
            }

            half4 DepthNormalsFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                return half4(NormalizeNormalPerPixel(input.normalWS), 0.0);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
