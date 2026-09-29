Shader "InnerFrame/Painted Outline"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (0.16, 0.18, 0.12, 1)
        _OutlineWidth ("Outline Width (pixels)", Range(0, 5)) = 2
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Geometry+1" }
        Pass
        {
            Name "PaintedSilhouette"
            Cull Front
            ZWrite Off
            ZTest LEqual
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _OutlineColor;
                float _OutlineWidth;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 worldNormal = TransformObjectToWorldNormal(input.normalOS);
                float3 viewNormal = mul((float3x3)UNITY_MATRIX_V, worldNormal);
                float2 direction = viewNormal.xy;
                direction /= max(length(direction), 0.001);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionCS.xy += direction * output.positionCS.w *
                    (_OutlineWidth * 2.0 / _ScreenParams.xy);
                return output;
            }

            half4 frag(Varyings input) : SV_Target { return _OutlineColor; }
            ENDHLSL
        }
    }
}
