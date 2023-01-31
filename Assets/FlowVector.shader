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

        struct FlowVector {
            float4x4 rotation;
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

        // https://stackoverflow.com/questions/14845084/how-do-i-convert-a-1d-index-into-a-3d-index
        float3 GetPosition(uint index) {
			int z = index % DEPTH;
			int y = (index / DEPTH) % HEIGHT;
			int x = index / (HEIGHT * DEPTH);
            return BOUNDS_MIN + float3(x,y,z) + float3(.5, .5, .5);
        }

        void setup() {
		#ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
			#ifdef SHADER_API_D3D11
				FlowVector flow = flowVectors[unity_InstanceID];

                // Translate matrix
                float3 pos = GetPosition(unity_InstanceID);
                float4x4 translate = 0.0;
                translate._m00_m11_m22_m33 = float4(1,1,1,1);
                translate._m03_m13_m23 = pos;

                // Rotation matrix
                float4x4 rotation = flow.rotation;

                // Scale matrix
                float s = pow(1.0 - flow.magnitude, 2);
                float4x4 scale = 0.0;
                scale._m00_m11_m22_m33 = float4(s,s,s,1.0);

                // Compose transform matrix
                //unity_ObjectToWorld = mul(translate, scale);
                unity_ObjectToWorld = mul(flow.transform, scale);
            #endif
        #endif
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
			#ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
                 // TODO: extract scaling from TRS matrix
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
