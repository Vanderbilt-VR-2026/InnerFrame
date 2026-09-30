Shader "VanGoghBedroom/HeadBlackout"
{
 SubShader
 {
  Tags { "Queue"="Overlay" "RenderType"="Opaque" }
  Pass
  {
   Cull Off ZWrite Off ZTest Always
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_instancing
   #include "UnityCG.cginc"
   struct appdata { float4 vertex:POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
   struct v2f { float4 vertex:SV_POSITION; UNITY_VERTEX_OUTPUT_STEREO };
   v2f vert(appdata v) { v2f o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); o.vertex=UnityObjectToClipPos(v.vertex);return o; }
   half4 frag(v2f i):SV_Target { return half4(0,0,0,1); }
   ENDHLSL
  }
 }
}
