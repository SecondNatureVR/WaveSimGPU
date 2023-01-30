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
			StructuredBuffer<float3> flowVectorBuffer;
		#endif

        float3x3 ssc(float3 v) {
            float3x3 mat = {
                0, -v.z, v.y,
                v.z, 0, -v.x,
                -v.y, v.x, 0
            };
            return mat;
        }

        void setup() {

		#ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
			#ifdef SHADER_API_D3D11
				float3 position     = positionBuffer[unity_InstanceID];
				float3 flowvec	    = flowVectorBuffer[unity_InstanceID];
			#else
				float3 position = 0;
				float4x4 flowvec = 0;
			#endif

            // set position
            unity_ObjectToWorld = 0.0;
		    unity_ObjectToWorld._m03_m13_m23_m33 = float4(position, 1);
            unity_ObjectToWorld._m00_m11_m22 = float3(1,1,1); // identity values

            // set rotation of A to B
            // https://math.stackexchange.com/questions/180418/calculate-rotation-matrix-to-align-vector-a-to-vector-b-in-3d
            float3 A = float3(0, 1, 0);
            float3 B = normalize(flowvec);

            // G, the 2D rotation along plane with normal A x B
            float cosTheta = dot(A, B);
            float sinTheta = normalize(cross(A, B));
            float3x3 G = {
			   cosTheta, -1.0f * sinTheta,    0.0f,
			   sinTheta,         cosTheta,    0.0f,
			       0.0f,             0.0f,    1.0f,
            };

            // F, the change of basis onto (u v w)^-1
            float3 u = A;
            float3 v = (B - mul(dot(A,B), A)) / (normalize(B - mul(dot(A, B), A)));
            float3 w = cross(B, A);

            float3x3 F = float3x3(u, v, w);

            // U, rotation vector
            float3x3 U = mul(transpose(F), mul(G, F));

            float4x4 rotation = 0.0;
            rotation._m00_m01_m02 = U._11_12_13;
            rotation._m10_m11_m12 = U._21_22_23;
            rotation._m20_m21_m22 = U._31_32_33;
            rotation._m33 = 1.0;
            // unity_ObjectToWorld = mul(rotation, unity_ObjectToWorld);


            // Rik's answer
            float4x4 sscw = 0.0; 
            float3x3 tmp = ssc(w);
            sscw._m00_m01_m02 = tmp._11_12_13;
            sscw._m10_m11_m12 = tmp._21_22_23;
            sscw._m20_m21_m22 = tmp._31_32_33;
            sscw._m33 = 1.0;
            float4x4 I = 0.0f;
            I._m00_m11_m22_m33 = float4(1,1,1,1); // Identity
            float4x4 R = I + sscw + pow(sscw, 2) * (1 / (1 + cosTheta));


            // Apply rotation
            // unity_ObjectToWorld = mul(R, unity_ObjectToWorld);


            // Third rotation approach
            // https://forum.unity.com/threads/rotate-mesh-inside-shader.1109660/
            float3 forward = B;
			float3 right = normalize(cross(forward, A));
			float3 up = cross(right, forward); // does not need to be normalized
			float3x3 rmat = float3x3(right, up, forward);
            float4x4 tmat = 0.0;
			tmat._m00_m10_m20 = right;
			tmat._m01_m11_m21 = up;
			tmat._m02_m12_m22 = forward;
            tmat._m33 = 1.0;
			 
            unity_ObjectToWorld = mul(tmat, unity_ObjectToWorld);

            // scaling
            float s = clamp(0.01f, 1.0f, length(flowvec));
			unity_ObjectToWorld._m00_m10_m20 *= s;
			unity_ObjectToWorld._m01_m11_m21 *= s;
			unity_ObjectToWorld._m02_m12_m22 *= s;

        #endif
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
			#ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
				float3 strength = length(flowVectorBuffer[unity_InstanceID]) / 10;
            #else
                float3 strength = 0.5f;
			#endif
            o.Albedo = lerp(_ColorMin, _ColorMax, saturate(strength - 0.5f));
            o.Alpha = 0.01f;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
