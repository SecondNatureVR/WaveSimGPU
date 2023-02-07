Shader "Particles/BiolumeParticle"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Opaque" }
        LOD 100

        Blend One One
        ZWrite Off

 
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog
 
            #include "UnityCG.cginc"
			#include "Assets/Shaders/FlowFieldCommon.hlsl"
 
            struct appdata
            {
                float4 vertex : POSITION;
                float4 uv : TEXCOORD0;
                float3 velocity : TEXCOORD1;
            };
 
            struct v2f
            {
                float4 uv : TEXCOORD0;
                // Speed = uv.z
                // AgePercent = uv.w
                float3 velocity : TEXCOORD1;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
            };
 
            sampler2D _MainTex;
            float4 _MainTex_ST;

            v2f vert (appdata v)
            {
                v2f o;
                float3 flowUV = GetTexUVFromWorldPosition(v.vertex.xyz);
                float3 dir = FLOW_SAMPLE_DIRECTION(flowUV);
                float mag = FLOW_SAMPLE_MAGNITUDE(flowUV);
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv.xy = TRANSFORM_TEX(v.uv.xy, _MainTex);
                v.uv.z = mag;
                v.uv.w = 100;
                v.velocity = dir * mag;

                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }
 
            fixed4 frag (v2f i) : SV_Target
            {
                // sample the texture
                fixed4 col = tex2D(_MainTex, i.uv);
                // apply fog
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
}
