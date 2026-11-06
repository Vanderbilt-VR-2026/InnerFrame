// The valley's mist: near-horizontal noise layers whose texture drifts (_Scroll, UV per second) and a few soft puffs
// that turn to face the viewer (_Billboard 1: the quad is rebuilt round its object's origin, upright, along the
// camera's right; its object scale gives the size). Alpha-blended (soft, overlapping), vertex alpha fades the edges,
// and anything closer than _NearFade.x fades out over _NearFade.y metres. URP, single-pass instanced stereo safe.
Shader "InnerFrame/StarryNight/Mist"
{
    Properties
    {
        _BaseMap ("Texture", 2D) = "white" {}
        _BaseColor ("Colour", Color) = (1, 1, 1, 1)
        _Scroll ("Drift (UV per second; xy)", Vector) = (0, 0, 0, 0)
        _Billboard ("Face the viewer", Float) = 0
        _NearFade ("Near fade (start, length; metres)", Vector) = (2, 3, 0, 0)
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Mist"
            Blend SrcAlpha OneMinusSrcAlpha
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
                float4 _BaseMap_ST, _Scroll, _NearFade;
                half4 _BaseColor;
                float _Billboard;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; UNITY_VERTEX_OUTPUT_STEREO };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 positionWS;
                if (_Billboard > 0.5)
                {
                    float3 centre = TransformObjectToWorld(float3(0, 0, 0));
                    float sx = length(float3(UNITY_MATRIX_M[0].x, UNITY_MATRIX_M[1].x, UNITY_MATRIX_M[2].x));
                    float sy = length(float3(UNITY_MATRIX_M[0].y, UNITY_MATRIX_M[1].y, UNITY_MATRIX_M[2].y));
                    float3 right = UNITY_MATRIX_V[0].xyz;
                    right.y = 0;
                    right = normalize(right + float3(1e-5, 0, 0));
                    positionWS = centre + right * v.positionOS.x * sx + float3(0, 1, 0) * v.positionOS.y * sy;
                }
                else positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(positionWS);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap) + _Scroll.xy * _Time.y;
                float near = saturate((distance(positionWS, GetCameraPositionWS()) - _NearFade.x) / max(_NearFade.y, 1e-3));
                o.color = v.color * _BaseColor;
                o.color.a *= near;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                return SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * i.color;
            }
            ENDHLSL
        }
    }
}
