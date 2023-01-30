Shader "Instanced/FlowVector"
{
    Properties
    {
        _ColorMin ("ColorMin", Color) = (0,0,1,1)
        _ColorMax ("ColorMax", Color) = (1,0,0,1)
    }
    SubShader
    {
        Blend SrcAlpha OneMinusSrcAlpha
        Tags { "RenderType"="Transparent" }
        LOD 200

        CGPROGRAM
        // Physically based Standard lighting model, and enable shadows on all light types
        #pragma surface surf Standard fullforwardshadows
		#pragma multi_compile_instancing
		#pragma instancing_options assumeuniformscaling procedural:setup
		#pragma target 4.5

		#include "UnityCG.cginc"
		#include "UnityLightingCommon.cginc"
		#include "AutoLight.cginc"

        sampler2D _MainTex;

        struct Input
        {
            float2 uv_MainTex;
        };

        fixed4 _ColorMin, _ColorMax;

		#ifdef SHADER_API_D3D11
			StructuredBuffer<float3> positionBuffer;
			StructuredBuffer<float4x4> flowVectorBuffer;
		#endif

        void setup() {

		#ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
			#ifdef SHADER_API_D3D11
				float3 position     = positionBuffer[unity_InstanceID];
				float4x4 flowvec	    = flowVectorBuffer[unity_InstanceID];
			#else
				float3 position = 0;
				float4x4 flowvec = 0;
			#endif

              unity_ObjectToWorld = flowvec;
        #endif
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
			#ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
                 // TODO: extract scaling from TRS matrix
                float3 pos = flowVectorBuffer[unity_InstanceID]._m03_m13_m23;
				//float strength = length(s); 
                float strength = length(pos) / 10;
            #else
                float strength = 0.5f;
			#endif
            o.Albedo = lerp(_ColorMin, _ColorMax, 1 - strength);
            o.Alpha = 0.01f;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
