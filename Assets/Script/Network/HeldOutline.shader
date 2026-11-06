// Outline drawn around a shared object while a player holds it, in that player's colour (NetworkGrabbable).
// Based on InnerFrame/Painted Outline (Assets/SerenaStyleExperiment): the mesh's back faces, pushed out along
// their normals by a fixed number of pixels, so only a rim shows around the object. Adds single-pass stereo
// (the Quest renders both eyes in one pass) and a gentle pulse so it reads as a highlight.
Shader "InnerFrame/Held Outline"
{
    Properties
    {
        _Color ("Colour", Color) = (1, 1, 1, 1)
        _Width ("Width (pixels)", Range(0, 10)) = 3
        _Pulse ("Pulse (0 = steady)", Range(0, 1)) = 0.35
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Geometry+1" "RenderType"="Opaque" }
        Pass
        {
            Name "HeldOutline"
            Tags { "LightMode"="UniversalForward" }
            Cull Front
            ZWrite Off
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Width;
                float _Pulse;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 worldNormal = TransformObjectToWorldNormal(input.normalOS);
                float3 viewNormal = mul((float3x3)UNITY_MATRIX_V, worldNormal);
                float2 direction = viewNormal.xy / max(length(viewNormal.xy), 0.001);
                float pulse = 1.0 + _Pulse * 0.5 * sin(_Time.y * 6.0);   // breathes a little, about once a second
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionCS.xy += direction * output.positionCS.w * (_Width * pulse * 2.0 / _ScreenParams.xy);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half glow = _Pulse * 0.25 * (0.5 + 0.5 * sin(_Time.y * 6.0));   // brighter at the top of each breath
                return half4(lerp(_Color.rgb, half3(1, 1, 1), glow), 1);
            }
            ENDHLSL
        }
    }
}
