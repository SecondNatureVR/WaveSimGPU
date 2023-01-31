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
        #pragma surface surf Standard fullforwardshadows
		#pragma multi_compile_instancing
		#pragma instancing_options assumeuniformscaling procedural:setup
		#pragma target 4.5

		#include "UnityCG.cginc"
		#include "UnityLightingCommon.cginc"
		#include "AutoLight.cginc"

        sampler2D _MainTex;

        struct FlowVector {
            float4x4 transform;
            float magnitude;
        };

        struct Input
        {
            float2 uv_MainTex;
        };

        fixed4 _ColorMin, _ColorMax;
        float3 BOUNDS_MIN;
        uint WIDTH, HEIGHT, DEPTH;

		#ifdef SHADER_API_D3D11
			StructuredBuffer<FlowVector> flowVectors;
		#endif

        void setup() {
		#ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
			#ifdef SHADER_API_D3D11
				FlowVector flow = flowVectors[unity_InstanceID];

                // Scale matrix
                float s = clamp(0.2, 1.0, flow.magnitude);
                float4x4 scale = 0.0;
                scale._m00_m11_m22_m33 = float4(s,s,s,1.0);

                // Compose transform matrix
                unity_ObjectToWorld = mul(flow.transform, scale);
                //unity_ObjectToWorld = flow.transform;
            #endif
        #endif
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
			#ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
                float strength = flowVectors[unity_InstanceID].magnitude;
            #else
                float strength = 0.5f;
			#endif
            o.Albedo = lerp(_ColorMin, _ColorMax, strength);
            o.Alpha = 0.1f;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
