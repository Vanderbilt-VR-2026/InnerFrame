Shader "Hidden/InnerFrame/EyeOverlay"
{
    SubShader
    {
        Tags { "Queue"="Overlay" "RenderType"="Transparent" "DisableBatching"="True" }
        Pass
        {
            Cull Off ZWrite Off ZTest Always
            Blend One OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            struct Input { float4 vertex : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Output { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            sampler2D _MainTex;
            float4 _ViewRect;
            float _ViewDepth, _HalfIPD;
            Output vert(Input v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                Output o;
                UNITY_INITIALIZE_OUTPUT(Output, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float2 xy = _ViewRect.xy + v.vertex.xy * _ViewRect.zw;
                #if defined(USING_STEREO_MATRICES)
                    xy.x += (unity_StereoEyeIndex == 0 ? _HalfIPD : -_HalfIPD);
                #endif
                // These vertices are already in view space. Tracking changes the scene's view matrix only.
                o.pos = mul(UNITY_MATRIX_P, float4(xy, -_ViewDepth, 1));
                o.uv = v.uv;
                return o;
            }
            half4 frag(Output i) : SV_Target { return tex2D(_MainTex, i.uv); }
            ENDHLSL
        }
    }
}
