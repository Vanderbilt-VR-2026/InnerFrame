Shader "Hidden/InnerFrame/ViewPixels"
{
    SubShader
    {
        Pass
        {
            Cull Off ZWrite Off ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct Input { float4 vertex : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Output { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            float4x4 _HudClip;
            sampler2D _MainTex;
            half4 _HudTint, _TextureSampleAdd;
            Output vert(Input v)
            {
                Output o;
                o.pos = mul(_HudClip, v.vertex);
                o.uv = v.uv;
                o.color = v.color * _HudTint;
                return o;
            }
            half4 frag(Output i) : SV_Target
            {
                return (tex2D(_MainTex, i.uv) + _TextureSampleAdd) * i.color;
            }
            ENDHLSL
        }
    }
}
