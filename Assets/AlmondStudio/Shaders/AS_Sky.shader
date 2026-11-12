// The sky round the studio, painted as in Almond Blossom: short, thick strokes of turquoise and blue-green laid side
// by side, each a capsule with a flat colour from four, a lighter ridge and faint bristle streaks, their directions
// drifting slowly across the sky. Two staggered layers of strokes over a ground colour, a little deeper toward the top
// (as the painting is). On the inside of a big sphere (Cull Front), so it is the same painted sky from every angle and
// below the horizon too (no ground: the painting has none). URP, unlit, no depth write, drawn first.
Shader "InnerFrame/AlmondStudio/Sky"
{
    Properties
    {
        _C0 ("Turquoise", Color) = (0.470, 0.730, 0.735, 1)
        _C1 ("Deeper blue-green", Color) = (0.300, 0.560, 0.640, 1)
        _C2 ("Pale", Color) = (0.650, 0.830, 0.800, 1)
        _C3 ("Teal", Color) = (0.380, 0.660, 0.640, 1)
        _Dabs ("Strokes round the sky", Float) = 140
        _Length ("Stroke length (cells)", Range(0.6, 2.5)) = 1.5
        _Width ("Stroke width (cells)", Range(0.15, 0.8)) = 0.42
        _Ridge ("Ridge and bristles", Range(0, 1)) = 0.5
        _Top ("Deeper toward the top", Range(0, 1)) = 0.35
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
                float _Dabs, _Length, _Width, _Ridge, _Top;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 dirOS : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };

            float hash1(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float2 hash2(float2 p) { return frac(sin(float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)))) * 43758.5453); }
            float noise(float2 p)
            {
                float2 i = floor(p), f = frac(p); f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(hash1(i), hash1(i + float2(1, 0)), f.x), lerp(hash1(i + float2(0, 1)), hash1(i + float2(1, 1)), f.x), f.y);
            }

            half3 Pick(float h)
            {
                // mostly turquoise, as the painting is; the others in between
                return h < 0.40 ? _C0.rgb : h < 0.62 ? _C3.rgb : h < 0.84 ? _C1.rgb : _C2.rgb;
            }

            // One layer of strokes: the stroke on top at p (largest coverage), its colour and its coverage.
            void Strokes(float2 p, float seed, inout half3 col, inout float cover)
            {
                float2 cell = floor(p);
                float best = 0.0; half3 bestCol = col;
                [unroll] for (int y = -1; y <= 1; y++)
                [unroll] for (int x = -1; x <= 1; x++)
                {
                    float2 c = cell + float2(x, y);
                    float2 r = hash2(c + seed);
                    float2 centre = c + 0.2 + r * 0.6;
                    // directions drift slowly across the sky, each stroke a little off its neighbours
                    float a = (noise(c * 0.08 + seed) - 0.5) * 3.2 + (r.x - 0.5) * 0.7 + 0.35;
                    float2 dir = float2(cos(a), sin(a)), nrm = float2(-dir.y, dir.x);
                    float2 d = p - centre;
                    float len = _Length * (0.7 + 0.6 * r.y), wid = _Width * (0.8 + 0.4 * r.x);
                    float u = dot(d, dir), v = dot(d, nrm);
                    // a capsule: flat sides, round ends
                    float uc = max(abs(u) - len * 0.5, 0.0);
                    float dist = sqrt(uc * uc + v * v) / wid;
                    float m = 1.0 - smoothstep(0.82, 1.0, dist);
                    // later cells win ties, so strokes overlap like paint laid one after another
                    float order = hash1(c + seed + 7.1);
                    float w = m * (0.75 + 0.25 * order);
                    if (w > best)
                    {
                        best = w;
                        half3 sc = Pick(hash1(c + seed + 3.3));
                        // a lighter ridge down the stroke, and bristle streaks along it
                        float ridge = 1.0 - saturate(abs(v) / wid);
                        float bristle = sin((v / wid) * 18.0 + r.x * 40.0) * 0.5 + 0.5;
                        sc *= 1.0 + _Ridge * (0.10 * ridge - 0.06 * bristle * (1.0 - ridge * 0.5));
                        // the stroke runs out of paint toward its end
                        sc = lerp(sc, col, saturate((u / len + 0.5) - 0.75) * 0.6);
                        bestCol = sc;
                    }
                }
                col = lerp(col, bestCol, best);
                cover = max(cover, best);
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
                float2 p = float2(lon * _Dabs, lat * _Dabs * 0.5);
                // the ground under the strokes: turquoise, a little deeper toward the top
                half3 col = lerp(_C0.rgb, _C1.rgb, saturate((d.y - 0.1) * 1.4) * _Top);
                float cover = 0.0;
                Strokes(p, 0.0, col, cover);
                Strokes(p * 1.07 + float2(0.5, 0.37), 17.0, col, cover);
                // the deeper top shows through the strokes too
                col = lerp(col, col * (_C1.rgb / max(_C0.rgb, 0.01)), saturate((d.y - 0.3) * 1.2) * _Top * 0.5);
                return half4(col, 1.0h);
            }
            ENDHLSL
        }
    }
}
