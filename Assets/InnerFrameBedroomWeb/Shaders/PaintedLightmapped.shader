// Painted Bedroom: albedo x a lightmap baked in Blender (Cycles), for the room's fixed objects (10/29).
// The lightmap stores sqrt(L / _LightMax) in 8 bits; L is irradiance (light only, no colour), so the
// result is exactly albedo x L, the same thing Blender rendered. No Unity lighting, no tonemapping.
// _Spec: the DIFFUSE bake has no specular term; a rough dielectric's 4% base reflectance under the same light is
// close to a constant added to the albedo. Verified against Cycles per region: +0.03 brings every surface within 2%.
// Shadows (11/10): the lightmap's alpha is the sun's share of L (bake_lightmaps.py). Unity's realtime sun (the Builder's
// "Window Light", same direction and strength as the Cycles sun) casts shadows from the objects you pick up; here that
// shadow darkens only the sunlit share, so a chair shades the floor's sun patch and nothing else. The ShadowCaster pass
// lets the room shade those objects in return (the window frame on a pitcher).
// Cheap on Quest: two texture reads, one shadow-map read, no lighting maths.
Shader "InnerFrame/Painted Lightmapped"
{
    Properties
    {
        _BaseMap ("Albedo", 2D) = "white" {}
        _BaseColor ("Colour", Color) = (1, 1, 1, 1)
        _LightMap ("Lightmap (uv2, sqrt-encoded)", 2D) = "gray" {}
        _LightMax ("Lightmap max L", Float) = 8
        _LightScale ("Lightmap scale", Float) = 1
        _Spec ("Base reflectance added to albedo", Float) = 0.03
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            Cull [_Cull]
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_LightMap); SAMPLER(sampler_LightMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _LightMax;
                half _LightScale;
                half _Spec;
                half _Cull;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv0 : TEXCOORD0; float2 uv1 : TEXCOORD1; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv0 : TEXCOORD0; float2 uv1 : TEXCOORD1; float3 positionWS : TEXCOORD2; UNITY_VERTEX_OUTPUT_STEREO };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.uv0 = TRANSFORM_TEX(v.uv0, _BaseMap);
                o.uv1 = v.uv1;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv0).rgb * _BaseColor.rgb;
                half4 lm = SAMPLE_TEXTURE2D(_LightMap, sampler_LightMap, i.uv1);
                half3 L = lm.rgb * lm.rgb * _LightMax * _LightScale;
                half shadow = MainLightRealtimeShadow(TransformWorldToShadowCoord(i.positionWS));   // 1 = lit, 0 = fully shadowed
                L *= 1.0h - lm.a * (1.0h - shadow);     // only the sun's share of the baked light goes dark
                return half4((albedo + _Spec) * L, 1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _LightMax;
                half _LightScale;
                half _Spec;
                half _Cull;
            CBUFFER_END
            float3 _LightDirection;
            float3 _LightPosition;
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; };
            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(v.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif
                o.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
                #if UNITY_REVERSED_Z
                    o.positionCS.z = min(o.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    o.positionCS.z = max(o.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                return o;
            }
            half4 frag(Varyings i) : SV_Target { return 0; }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull [_Cull]
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _LightMax;
                half _LightScale;
                half _Spec;
                half _Cull;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };
            Varyings vert(Attributes v) { Varyings o; o.positionCS = TransformObjectToHClip(v.positionOS.xyz); return o; }
            half4 frag(Varyings i) : SV_Target { return 0; }
            ENDHLSL
        }
    }
    FallBack Off
}
