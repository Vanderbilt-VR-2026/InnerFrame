// Cafe Terrace: every surface of the set and every object. Unlit, URP, single-pass instanced stereo safe.
// colour = texture x _TexGain x light x _Tint, then _Saturation.
// The light is not computed here: Blender baked paint x light into each vertex (make_unity_export.py), stored as
// sqrt(value / _LightRange) so lamp-lit yellow above 1 survives 8 bits. The chairs and tables carry the light of
// the spot they stand on the same way. _VertexLight 0 uses _Tint alone (the flame).
Shader "InnerFrame/CafeTerrace/Painted"
{
    Properties
    {
        _BaseMap ("Texture", 2D) = "white" {}
        _TexGain ("Texture gain (2 for the grey streak textures)", Float) = 1
        _TexDesat ("Texture towards its own grey", Range(0, 1)) = 0
        [HDR] _Tint ("Tint", Color) = (1, 1, 1, 1)
        [Toggle] _VertexLight ("Baked light in the vertex colours", Float) = 1
        _LightRange ("Brightest baked value", Float) = 4
        _Saturation ("Saturation", Range(0, 1.5)) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "Painted"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _Tint;
                half _TexGain, _TexDesat, _VertexLight, _LightRange, _Saturation;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half3 light : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                half3 baked = v.color.rgb * v.color.rgb * _LightRange;
                o.light = lerp(half3(1, 1, 1), baked, _VertexLight) * _Tint.rgb;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half3 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).rgb;
                tex = lerp(tex, dot(tex, half3(0.2126, 0.7152, 0.0722)).xxx, _TexDesat);
                half3 col = tex * _TexGain * i.light;
                col = lerp(dot(col, half3(0.2126, 0.7152, 0.0722)).xxx, col, _Saturation);
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
