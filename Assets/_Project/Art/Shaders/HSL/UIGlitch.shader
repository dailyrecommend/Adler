// UI 그림을 신호가 끊긴 화면처럼 망가뜨린다.
//
// 스프라이트를 갈아 끼우는 방식으로는 그림 하나가 통째로 움직일 뿐이라, 정작 지지직거림의
// 핵심인 "가로줄이 서로 다르게 어긋나는 것"이 나오지 않는다. 그건 픽셀 단위로 UV를 밀어야
// 하므로 프래그먼트에서 해야 한다.
//
// 아틀라스에 묶인 스프라이트를 위해 _UVRect 안에서만 UV를 감는다. 그냥 밀면 옆에 놓인
// 다른 그림이 딸려 나온다.
Shader "Adler/UI Glitch"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [Header(Glitch)]
        _GlitchAmount ("세기 (0이면 멀쩡하다)", Range(0,1)) = 1
        _TickRate ("무늬가 바뀌는 빠르기 (초당)", Float) = 18
        _Rows ("가로줄 개수", Range(1,128)) = 24
        _Displace ("줄이 밀리는 폭", Range(0,0.5)) = 0.07
        _ChromaSplit ("색 어긋남", Range(0,0.1)) = 0.012
        _NoiseAmount ("잡티", Range(0,1)) = 0.35
        _Dropout ("줄이 사라지는 정도", Range(0,1)) = 0.1
        _ScanSpeed ("흐르는 띠의 속도", Float) = 1.2
        _ScanIntensity ("흐르는 띠의 밝기", Range(0,1)) = 0.25
        _Seed ("씨앗 (칸마다 다르게)", Float) = 0

        // 아틀라스 안에서 이 그림이 차지하는 UV 범위. xy가 왼쪽아래, zw가 오른쪽위.
        _UVRect ("UV Rect", Vector) = (0,0,1,1)

        // 여기부터는 UGUI가 요구하는 것들. 빼면 마스크와 겹침 순서가 망가진다.
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex        : SV_POSITION;
                fixed4 color         : COLOR;
                float2 texcoord      : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;

            float4 _UVRect;
            float _GlitchAmount;
            float _TickRate;
            float _Rows;
            float _Displace;
            float _ChromaSplit;
            float _NoiseAmount;
            float _Dropout;
            float _ScanSpeed;
            float _ScanIntensity;
            float _Seed;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);

                // 정점 색에는 Image의 색과 CanvasGroup의 투명도가 함께 실려 온다.
                // 이걸 빼먹으면 칸이 사라질 때 아이콘만 남는다.
                OUT.color = v.color * _Color;
                return OUT;
            }

            float Hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453123);
            }

            // 그림이 놓인 칸 안에서만 UV를 감는다. 아틀라스에서 밀려나면 남의 그림을 읽는다.
            float2 WrapInRect(float2 uv)
            {
                float2 size = max(_UVRect.zw - _UVRect.xy, 1e-5);
                return _UVRect.xy + frac((uv - _UVRect.xy) / size) * size;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float amount = saturate(_GlitchAmount);

                // 프레임마다 새 무늬를 뽑으면 주사율에 따라 다르게 보이고, 144Hz에서는
                // 너무 잘게 떨려 회색으로 뭉개진다. 정해진 간격의 계단으로 끊어 쓴다.
                float tick = floor(_Time.y * max(_TickRate, 0.001)) + _Seed * 31.7;

                float2 uv = IN.texcoord;
                float row = floor(uv.y * _Rows);

                // 모든 줄이 움직이면 그림이 사라진다. 일부만 어긋나야 원래 모습이 남아
                // 무엇이 망가진 것인지 알아볼 수 있다.
                float active = step(1.0 - amount, Hash(float2(row, tick)));
                float shift = (Hash(float2(row + 0.5, tick * 1.7)) - 0.5) * 2.0 * _Displace * active;

                float2 duv = WrapInRect(uv + float2(shift, 0));
                float split = _ChromaSplit * active;

                // 채널을 따로 뽑아 어긋나게 한다. 색이 갈라지는 것이 신호 문제로 읽히는
                // 가장 강한 신호다 — 흑백으로 흔들기만 하면 그냥 흔들리는 그림이다.
                fixed4 col;
                fixed4 mid = tex2D(_MainTex, duv);
                col.g = mid.g;
                col.a = mid.a;
                col.r = tex2D(_MainTex, WrapInRect(duv + float2(split, 0))).r;
                col.b = tex2D(_MainTex, WrapInRect(duv - float2(split, 0))).b;

                col += _TextureSampleAdd;

                // 잡티
                float noise = Hash(uv * 512.0 + tick);
                col.rgb = lerp(col.rgb, col.rgb * (0.4 + noise), _NoiseAmount * amount);

                // 줄 단위로 잠깐씩 사라진다
                float drop = step(Hash(float2(row * 3.3, tick + 17.0)), _Dropout * amount);
                col.a *= 1.0 - drop;

                // 위로 흐르는 밝은 띠
                float bar = frac(uv.y - _Time.y * _ScanSpeed);
                col.rgb += _ScanIntensity * amount * smoothstep(0.94, 1.0, bar) * col.a;

                col *= IN.color;

                #ifdef UNITY_UI_CLIP_RECT
                col.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(col.a - 0.001);
                #endif

                return col;
            }
            ENDCG
        }
    }
}
