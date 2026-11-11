// The almond blossoms (AS_Blossoms): quads cut out of the painted atlas (petal, bud petal, heart, sepals), double-
// sided. Vertex colour r = how much of a bud it still is (blends the petal cell with the bud petal cell beside it,
// +0.5 in u), g = shade (a fixed soft light, worked out per quad on the CPU). Alpha-tested so it sorts like solid
// geometry. URP, unlit, single-pass instanced stereo safe.
Shader "InnerFrame/AlmondStudio/Blossom"
{
    Properties
    {
        _BaseMap ("Atlas", 2D) = "white" {}
        _Cutoff ("Alpha cutoff", Range(0, 1)) = 0.5
    }
    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "Queue" = "AlphaTest" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "Blossom"
            Cull Off
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half _Cutoff;
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
                half4 c = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
                if (i.color.r > 0.001h)
                {
                    half4 bud = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv + float2(0.5, 0.0));
                    c = lerp(c, bud, i.color.r);
                }
                clip(c.a * i.color.a - _Cutoff);
                return half4(c.rgb * i.color.g, 1.0h);
            }
            ENDHLSL
        }
    }
}
