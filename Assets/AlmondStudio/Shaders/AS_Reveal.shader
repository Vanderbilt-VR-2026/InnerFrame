// The almond tree outside the studio window: Van Gogh's Almond Blossom in two versions, bare (_BareTex) and the
// original (_FullTex), and a reveal map (_MapTex, built at runtime by AS_Branches.BuildRevealMap: R = place along its
// branch 0..1, G = branch index). A pixel shows the original once its branch's bloom (_Progress[index]) has passed it,
// with a soft edge (_Soft). _All forces the whole original (the ending). Unlit: the painting carries its own light.
// URP, single-pass instanced stereo safe.
Shader "InnerFrame/AlmondStudio/Reveal"
{
    Properties
    {
        _BareTex ("Bare (no blossoms)", 2D) = "white" {}
        _FullTex ("Original", 2D) = "white" {}
        _MapTex ("Reveal map (runtime)", 2D) = "black" {}
        _Soft ("Edge softness", Range(0.005, 0.3)) = 0.06
        _All ("Show all", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "Reveal"
            Cull Off
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BareTex); SAMPLER(sampler_BareTex);
            TEXTURE2D(_FullTex); SAMPLER(sampler_FullTex);
            TEXTURE2D(_MapTex);  SAMPLER(sampler_MapTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _BareTex_ST;
                half _Soft;
                half _All;
            CBUFFER_END

            float _Progress[8];

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                half4 bare = SAMPLE_TEXTURE2D(_BareTex, sampler_BareTex, i.uv);
                half4 full = SAMPLE_TEXTURE2D(_FullTex, sampler_FullTex, i.uv);
                float4 map = SAMPLE_TEXTURE2D(_MapTex, sampler_MapTex, i.uv);
                int id = (int)round(map.g * 255.0);
                id = clamp(id, 0, 7);
                float p = _Progress[id];
                float r = p > 0.0 ? saturate((p - map.r) / max(_Soft, 1e-3) + 1.0) : 0.0;
                r = max(r, _All);
                return half4(lerp(bare.rgb, full.rgb, r), 1.0h);
            }
            ENDHLSL
        }
    }
}
