// Depth only, no colour: drawn under the hover rim of a glass object (PaintedBedroomHoverHighlight).
// Glass materials write no depth, so the rim's pushed-out back faces (InnerFrame/Held Outline) had nothing
// to hide behind and the whole glass went cream. This writes the object's own front-face depth just before
// the rim (queue Transparent-2, the rim's glass copy is at Transparent-1, the glass itself at Transparent),
// so only the edge survives, as on opaque objects. Single-pass stereo like the outline shader.
Shader "InnerFrame/Hover Depth Only"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-2" "RenderType"="Opaque" }
        Pass
        {
            Name "HoverDepthOnly"
            Tags { "LightMode"="SRPDefaultUnlit" }
            ColorMask 0
            ZWrite On
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
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
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return 0;
            }
            ENDHLSL
        }
    }
}
