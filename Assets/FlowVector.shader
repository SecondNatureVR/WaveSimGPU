Shader "Instanced/FlowVector"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
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

        half _Glossiness;
        half _Metallic;
        fixed4 _Color;

		#ifdef SHADER_API_D3D11
			StructuredBuffer<float3> positionBuffer;
			StructuredBuffer<float3> flowVectorBuffer;
		#endif

        void setup() {

		#ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
			#ifdef SHADER_API_D3D11
				float3 position     = positionBuffer[unity_InstanceID];
				float3 flowvec	    = flowVectorBuffer[unity_InstanceID];
			#else
				float3 position = 0;
				float4x4 flowvec = 0;
			#endif

            // Define rotation from mesh instance direction (up) to flowvec direction
            // https://forum.unity.com/threads/rotate-mesh-inside-shader.1109660/
            float3 forward = flowvec;
            float3 right = normalize(cross(forward, float3(0,1,0)));
			float3 up = cross(right, forward); // does not need to be normalized
 
            unity_ObjectToWorld = 0.0;
		    unity_ObjectToWorld._m03_m13_m23_m33 = float4(position, 1);
            unity_ObjectToWorld._m00_m01_m02 = right;
            unity_ObjectToWorld._m10_m11_m12 = up;
            unity_ObjectToWorld._m20_m21_m22 = forward;
        #endif
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            // Albedo comes from a texture tinted by color
            fixed4 c = tex2D (_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = c.rgb;
            // Metallic and smoothness come from slider variables
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Alpha = c.a;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
