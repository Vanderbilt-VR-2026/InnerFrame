// The two fields of small stars: one mesh each (SN_Sky builds them), one draw call each, additive. Each star is a quad
// facing the seat; a medium star also has a halo quad (the painted halo). Per vertex: TEXCOORD1 = (the star's centre,
// twinkle phase), TEXCOORD2 = (group: 0 shows before the lantern / 1 after; kind: 0 core / 1 halo; brightness;
// twinkle speed). The halo quad is scaled about the star's centre by lerp(_HaloSmallScale, 1, _HaloFull).
// _Early, _Late: the two groups' visibility; _HaloWake, _HaloFull: the halos; _Fade: dawn. URP, stereo safe.
Shader "InnerFrame/StarryNight/StarField"
{
    Properties
    {
        _BaseMap ("Atlas: core (left half), painted halo (right half)", 2D) = "white" {}
        _Early ("Early group", Range(0, 1)) = 0.35
        _Late ("Late group", Range(0, 1)) = 0
        _HaloWake ("Halos: woken", Range(0, 1)) = 0
        _HaloFull ("Halos: full (painted)", Range(0, 1)) = 0
        _HaloSmallScale ("Woken halo / full halo", Range(0, 1)) = 0.45
        _Fade ("Dawn fade", Range(0, 1)) = 0
        _Twinkle ("Twinkle", Range(0, 1)) = 0.45
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "StarField"
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half _Early, _Late, _HaloWake, _HaloFull, _HaloSmallScale, _Fade, _Twinkle;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 centre : TEXCOORD1;
                float4 info : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half alpha : TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                bool halo = v.info.y > 0.5;
                float scale = halo ? lerp(_HaloSmallScale, 1.0, _HaloFull) : 1.0;
                float3 p = v.centre.xyz + (v.positionOS.xyz - v.centre.xyz) * scale;
                o.positionCS = TransformObjectToHClip(p);
                o.uv = v.uv;
                half visible = lerp(_Early, _Late, v.info.x) * (1.0 - _Fade);
                half twinkle = 1.0 - _Twinkle * (0.5 + 0.5 * sin(_Time.y * v.info.w + v.centre.w));
                o.alpha = halo ? visible * max(_HaloWake, _HaloFull) * v.info.z : visible * twinkle * v.info.z;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                half4 c = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
                return half4(c.rgb * c.a * i.alpha, 0);
            }
            ENDHLSL
        }
    }
}
