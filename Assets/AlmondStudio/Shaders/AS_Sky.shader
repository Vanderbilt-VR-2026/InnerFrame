// The sky round the studio, on the inside of a big sphere (Cull Front), so it is the same painted sky from every angle.
// With a picture (_BaseMap: Textures/Sky/AS_Sky, a seamless painted sky made with ChatGPT after Almond Blossom), the
// picture repeats _Tiles times round (x) and from the bottom to the top (y) of the sphere. Without one: Almond
// Blossom's turquoise in soft dabs of four blues (value noise in longitude and latitude). URP, unlit, no depth write,
// drawn first. The wrap-round seam is straight behind the window (the view's -z), where nobody looks out.
Shader "InnerFrame/AlmondStudio/Sky"
{
    Properties
    {
        _BaseMap ("Painted sky (seamless; optional)", 2D) = "white" {}
        _UseMap ("Use the picture", Float) = 0
        _Tiles ("Picture repeats (round, bottom to top)", Vector) = (8, 4, 0, 0)
        _Tint ("Picture tint", Color) = (1, 1, 1, 1)
        _C0 ("Turquoise", Color) = (0.486, 0.682, 0.686, 1)
        _C1 ("Deeper", Color) = (0.400, 0.620, 0.660, 1)
        _C2 ("Lighter", Color) = (0.560, 0.750, 0.735, 1)
        _C3 ("Blue", Color) = (0.390, 0.560, 0.680, 1)
        _Dabs ("Dabs round the sky", Float) = 90
    }
    SubShader
    {
        Tags { "RenderType" = "Background" "Queue" = "Background" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "Sky"
            Cull Front
            ZWrite Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _Tiles;
                half4 _Tint, _C0, _C1, _C2, _C3;
                float _Dabs, _UseMap;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 dirOS : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };

            float hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float noise(float2 p)
            {
                float2 i = floor(p), f = frac(p); f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(hash(i), hash(i + float2(1, 0)), f.x), lerp(hash(i + float2(0, 1)), hash(i + float2(1, 1)), f.x), f.y);
            }

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.dirOS = v.positionOS.xyz;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float3 d = normalize(i.dirOS);
                float lon = atan2(d.x, d.z) / 6.2831853 + 0.5;
                float lat = asin(clamp(d.y, -1.0, 1.0)) / 3.1415927 + 0.5;
                if (_UseMap > 0.5)
                {
                    float2 uv = float2(lon * _Tiles.x, lat * _Tiles.y);
                    // the mip level from a coordinate without the wrap-round jump, so the seam shows no line
                    float2 uvC = float2(atan2(d.x, abs(d.z) + 1e-4) / 6.2831853 * _Tiles.x, uv.y);
                    half3 c = SAMPLE_TEXTURE2D_GRAD(_BaseMap, sampler_BaseMap, uv, ddx(uvC), ddy(uvC)).rgb;
                    return half4(c * _Tint.rgb, 1.0h);
                }
                float2 p = float2(lon * _Dabs, lat * _Dabs * 0.55);
                float2 w = float2(noise(p * 0.35), noise(p * 0.35 + 3.7));
                float n = noise(p + w * 2.0);
                float m = noise(p * 0.6 + 11.3);
                half3 col = lerp(_C1.rgb, _C0.rgb, smoothstep(0.25, 0.6, n));
                col = lerp(col, _C2.rgb, smoothstep(0.62, 0.9, n) * 0.8);
                col = lerp(col, _C3.rgb, smoothstep(0.65, 0.95, m) * 0.5);
                return half4(col, 1.0h);
            }
            ENDHLSL
        }
    }
}
