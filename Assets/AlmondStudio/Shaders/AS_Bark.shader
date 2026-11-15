// The almond tree's bark, painted: long strokes along each branch in Almond Blossom's greys, greens and blues
// (value noise stretched along UV x, which runs base to tip), shaded half-Lambert from a fixed morning light so the
// branches read round. The painted part of a branch (UV x below _Progress, set per renderer by AS_Tree) warms a
// little; the blossoms there come in step 2. URP, unlit (its own light), single-pass instanced stereo safe.
Shader "InnerFrame/AlmondStudio/Bark"
{
    Properties
    {
        _Light ("Bark light", Color) = (0.80, 0.86, 0.72, 1)
        _Mid ("Bark mid", Color) = (0.58, 0.69, 0.60, 1)
        _Dark ("Bark dark", Color) = (0.27, 0.38, 0.33, 1)
        _Blue ("Bark blue", Color) = (0.42, 0.58, 0.66, 1)
        _Stretch ("Stroke length (along / around)", Float) = 70
        _LightDir ("Light direction (world)", Vector) = (0.3, 0.8, -0.5, 0)
        _Progress ("Painted (0..1, per renderer)", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "Bark"
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Light, _Mid, _Dark, _Blue;
                float _Stretch;
                float4 _LightDir;
                float _Progress;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 normalWS : TEXCOORD1; float3 positionWS : TEXCOORD2; UNITY_VERTEX_OUTPUT_STEREO };

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
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = v.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                // strokes: long along the branch, a few round it, wobbling
                float2 p = float2(i.uv.x * _Stretch, i.uv.y * 5.0);
                float n = noise(p + noise(p * 0.5) * 1.5);
                float n2 = noise(p * float2(0.5, 2.3) + 7.1);
                half3 col = lerp(_Dark.rgb, _Mid.rgb, smoothstep(0.2, 0.55, n));
                col = lerp(col, _Light.rgb, smoothstep(0.6, 0.85, n));
                col = lerp(col, _Blue.rgb, smoothstep(0.7, 0.95, n2) * 0.6);
                // round, from one soft morning light
                float ndl = dot(normalize(i.normalWS), normalize(_LightDir.xyz));
                col *= 0.62 + 0.45 * saturate(ndl * 0.5 + 0.5);
                // the painted part warms a little
                float painted = _Progress > 0.0 ? step(i.uv.x, _Progress) : 0.0;
                col = lerp(col, col * half3(1.10, 1.06, 0.95) + 0.04, painted);
                return half4(col, 1.0h);
            }
            ENDHLSL
        }
    }
}
