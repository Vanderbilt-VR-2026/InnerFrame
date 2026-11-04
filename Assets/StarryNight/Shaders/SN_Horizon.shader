// The painted horizon (horizon.png's band: far hills, the village, the mist below it), on a strip round the seat.
// Unlit, alpha-blended: the soft key edge against the sky and the strip's faded ends (vertex alpha) need it. Texture x
// _Tint (night to dawn, set by SN_Sky) x vertex colour; plus the lit windows' mask (_GlowMap) x _GlowColour x _Glow.
// URP, single-pass instanced stereo safe; no depth write, no fog (it is tinted instead).
Shader "InnerFrame/StarryNight/Horizon"
{
    Properties
    {
        _BaseMap ("Band (RGBA)", 2D) = "white" {}
        _GlowMap ("Lit windows (mask)", 2D) = "black" {}
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        _GlowColour ("Window glow colour", Color) = (1, 0.72, 0.32, 1)
        _Glow ("Window glow", Range(0, 2)) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-20" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Horizon"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_GlowMap); SAMPLER(sampler_GlowMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST, _GlowMap_ST;
                half4 _Tint, _GlowColour;
                half _Glow;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; UNITY_VERTEX_OUTPUT_STEREO };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                half4 c = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _Tint * i.color;
                half glow = SAMPLE_TEXTURE2D(_GlowMap, sampler_GlowMap, i.uv).r;
                c.rgb += glow * _GlowColour.rgb * _Glow;
                return c;
            }
            ENDHLSL
        }
    }
}
