// The Starry Night's sky dome: an inverted sphere round the hill, drawn behind everything (Background queue, no depth
// write). Unlit, URP, single-pass instanced stereo safe. A night gradient (optionally a night texture); the painting
// (_PaintTex) mapped cylindrically onto the front panel (yaw -_PanelYaw..+_PanelYaw round +Z, pitch _PanelPitchMin..
// _PanelPitchMax) with soft edges; _Paint reveals it outside the swirl band; the band (_SwirlMask: R = position along the
// wind's path 0..1, G = inside the band, black outside) is revealed along the path by _SwirlProgress; _Dawn blends to a
// dawn gradient, deep blue overhead to pale yellow-green at the horizon.
Shader "InnerFrame/StarryNight/Sky"
{
    Properties
    {
        _NightTop ("Night, overhead", Color) = (0.03, 0.07, 0.2, 1)
        _NightHorizon ("Night, at the horizon", Color) = (0.09, 0.19, 0.4, 1)
        _Below ("Below the horizon", Color) = (0.05, 0.08, 0.17, 1)
        _NightTex ("Night texture (optional, equirectangular)", 2D) = "black" {}
        _NightTexAmount ("Night texture amount", Range(0, 1)) = 0
        _PaintTex ("Painted sky", 2D) = "black" {}
        _Paint ("Paint (outside the swirl band)", Range(0, 1)) = 0
        _SwirlMask ("Swirl mask (R position, G band)", 2D) = "black" {}
        _SwirlProgress ("Swirl progress", Range(0, 1)) = 0
        _SwirlEdge ("Swirl leading edge", Range(0.005, 0.2)) = 0.04
        _PanelYaw ("Panel half-width (degrees)", Float) = 80
        _PanelPitchMin ("Panel bottom (degrees)", Float) = -5
        _PanelPitchMax ("Panel top (degrees)", Float) = 70
        _PanelSoft ("Panel soft edge (degrees)", Float) = 8
        _Dawn ("Dawn", Range(0, 1)) = 0
        _DawnTop ("Dawn, overhead", Color) = (0.13, 0.27, 0.56, 1)
        _DawnHorizon ("Dawn, at the horizon", Color) = (0.85, 0.88, 0.64, 1)
        _DawnKeep ("Painting kept at full dawn", Range(0, 1)) = 0.25
    }
    SubShader
    {
        Tags { "RenderType" = "Background" "Queue" = "Background" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" "PreviewType" = "Skybox" }
        Pass
        {
            Name "Sky"
            Cull Front
            ZWrite Off
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_NightTex); SAMPLER(sampler_NightTex);
            TEXTURE2D(_PaintTex); SAMPLER(sampler_PaintTex);
            TEXTURE2D(_SwirlMask); SAMPLER(sampler_SwirlMask);

            CBUFFER_START(UnityPerMaterial)
                half4 _NightTop, _NightHorizon, _Below, _DawnTop, _DawnHorizon;
                float4 _NightTex_ST, _PaintTex_ST, _SwirlMask_ST;
                half _NightTexAmount, _Paint, _SwirlProgress, _SwirlEdge, _Dawn, _DawnKeep;
                float _PanelYaw, _PanelPitchMin, _PanelPitchMax, _PanelSoft;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 dirOS : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.dirOS = v.positionOS.xyz;   // the dome is centred on the hill and never rotated
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float3 d = normalize(i.dirOS);
                float yaw = degrees(atan2(d.x, d.z));
                float pitch = degrees(asin(clamp(d.y, -1.0, 1.0)));

                // the night
                float up = saturate(d.y);
                half3 night = lerp(_NightHorizon.rgb, _NightTop.rgb, pow(up, 0.55));
                float2 eq = float2(yaw / 360.0 + 0.5, pitch / 180.0 + 0.5);
                night = lerp(night, SAMPLE_TEXTURE2D_LOD(_NightTex, sampler_NightTex, eq, 0).rgb, _NightTexAmount);
                night = lerp(night, _Below.rgb, saturate(-d.y * 4.0));

                // the painting on the front panel
                float2 uv = float2((yaw + _PanelYaw) / (2.0 * _PanelYaw), (pitch - _PanelPitchMin) / (_PanelPitchMax - _PanelPitchMin));
                float2 soft = float2(_PanelSoft / (2.0 * _PanelYaw), _PanelSoft / (_PanelPitchMax - _PanelPitchMin));
                float edge = smoothstep(0.0, soft.x, uv.x) * smoothstep(0.0, soft.x, 1.0 - uv.x)
                           * smoothstep(0.0, soft.y, uv.y) * smoothstep(0.0, soft.y, 1.0 - uv.y);
                half3 painted = SAMPLE_TEXTURE2D_LOD(_PaintTex, sampler_PaintTex, saturate(uv), 0).rgb;
                half2 mask = SAMPLE_TEXTURE2D_LOD(_SwirlMask, sampler_SwirlMask, saturate(uv), 0).rg;
                half band = mask.g;
                half swirl = saturate((_SwirlProgress * (1.0 + _SwirlEdge) - mask.r) / _SwirlEdge);
                half reveal = saturate(_Paint * (1.0 - band) + band * swirl) * edge;
                half3 col = lerp(night, painted, reveal);

                // dawn
                half3 dawn = lerp(_DawnHorizon.rgb, _DawnTop.rgb, pow(up, 0.5));
                dawn = lerp(dawn, _Below.rgb * 2.0, saturate(-d.y * 4.0));
                col = lerp(col, dawn, _Dawn * (1.0 - _DawnKeep * reveal));
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
}
