Shader "Instanced/FlowVector"
{
    Properties
    {
        _NormalDirections ("_NormalDirections", 3D) = "white" {}
        _HeightMagnitudes("_HeightMagnitudes", 3D) = "white" {}
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

        struct FlowVector {
            float4x4 transform;
            float magnitude;
        };

        fixed4 _ColorMin, _ColorMax;
        float3 BOUNDS_MIN, BOUNDS_EXTENTS;
        uint WIDTH, HEIGHT, DEPTH;

        UNITY_DECLARE_TEX3D_NOSAMPLER(_NormalDirections);
        UNITY_DECLARE_TEX3D_NOSAMPLER(_HeightMagnitudes);
	    SamplerState my_linear_repeat_sampler;

		uint3 GetCoord(int index) {
			uint z = index % DEPTH;
			uint y = (index / DEPTH) % HEIGHT;
			uint x = index / (HEIGHT * DEPTH);
            return uint3(x, y, z);
        }

        float3 GetPosition(int index) {
            uint3 coord = GetCoord(index);
			return BOUNDS_MIN + coord + float3(1,1,1) * .5;
		}

        float4 GetUV(int index) {
            float3 coord = GetCoord(index);
            return float4(
                coord.x / WIDTH,
                coord.y / HEIGHT,
                coord.z / DEPTH,
                1
            );
        }

        float4x4 SetRotationToDirection(float3 direction, inout float4x4 tmat) {
            // set new rotation toward velocity
            // https://math.stackexchange.com/questions/180418/calculate-rotation-matrix-to-align-vector-a-to-vector-b-in-3d
            float3x3 rmat = 0.0;
            rmat._m00_m11_m22 = float3(1, 1, 1);

            float3 a = float3(0, 1, 0);
            float3 b = normalize(direction);
            float3 v = cross(a, b);
            float c = dot(a, b);
            float3x3 ssc = {
                 0, -v.z,  v.y,
               v.z,    0, -v.x,
              -v.y,  v.x,    0
            };
            float3x3 term2 = mul(ssc, ssc) * rcp(1 + c);
            rmat += ssc + term2;

            tmat._m00_m01_m02 = rmat._m00_m01_m02;
            tmat._m10_m11_m12 = rmat._m10_m11_m12;
            tmat._m20_m21_m22 = rmat._m20_m21_m22;
            return tmat;
		}

        float4x4 SetScaleFromMagnitude(float magnitude, inout float4x4 tmat) {
			float s = min(1.0, magnitude * magnitude);
			float4x4 scale = 0.0;
			scale._m00_m11_m22_m33 = float4(s,s,s,1.0);
            return mul(tmat, scale);
        }

        float3 UnpackNormal(float3 packedNormal) {
            return (packedNormal * 2.0) - float3(1,1,1);
        }

        void setup() {
		#ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
		#ifdef SHADER_API_D3D11

			float3 pos = GetPosition(unity_InstanceID);
			float4 uv = GetUV(unity_InstanceID);
			float3 direction = UnpackNormal(_NormalDirections.SampleLevel(my_linear_repeat_sampler, uv, 0, 0.0));
			float magnitude = _HeightMagnitudes.SampleLevel(my_linear_repeat_sampler, uv, 0, 0.0).x;

			float4x4 tmat = {
				1, 0, 0,  pos.x,
				0, 1, 0,  pos.y,
				0, 0, 1,  pos.z,
				0, 0, 0,      1,
			};
			tmat = SetRotationToDirection(direction, tmat);
			tmat = SetScaleFromMagnitude(magnitude, tmat);
			unity_ObjectToWorld = tmat;

		#endif
        #endif
        }


        struct Input {
            int _;  
        };

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
			float strength = 0.5f;
			#ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
			#ifdef SHADER_API_D3D11
                float4 uv = GetUV(unity_InstanceID);
                strength = _HeightMagnitudes.SampleLevel(my_linear_repeat_sampler, uv, 0.0, 0).x;
			#endif
			#endif
            o.Albedo = lerp(_ColorMin, _ColorMax, strength);
            o.Alpha = 0.1f;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
