// The sky round the studio: Almond Blossom's turquoise, laid in short dabs of four blues (value noise in the view
// direction's longitude and latitude, the dabs a little longer than tall), on the inside of a big sphere
// (Cull Front), so it is the same painted sky from every angle. URP, unlit, no depth write, drawn first.
Shader "InnerFrame/AlmondStudio/Sky"
{
    Properties
    {
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

            CBUFFER_START(UnityPerMaterial)
                half4 _C0, _C1, _C2, _C3;
                float _Dabs;
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
