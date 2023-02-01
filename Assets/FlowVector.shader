Shader "Instanced/FlowVector"
{
    Properties
    {
        _NormalDirections ("_NormalDirections", 3D) = "white" {}
        _HeightMagnitudes("_HeightMagnitudes", 3D) = "white" {}
        _ColorMin ("ColorMin", Color) = (0,0,1,1)
        _ColorMax ("ColorMax", Color) = (1,0,0,1)
        _UV_Offset ("Offset", float) = (0, 0, 0)
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
        float4 _UV_Offset;
        float3 BOUNDS_MIN, BOUNDS_EXTENTS, BOUNDS_SIZE;
        uint WIDTH, HEIGHT, DEPTH;
        float3 TEXEL_SIZE;
        uint3 TEX_DIMENSIONS;

        UNITY_DECLARE_TEX3D_NOSAMPLER(_NormalDirections);
        UNITY_DECLARE_TEX3D_NOSAMPLER(_HeightMagnitudes);
        float4 _NormalDirections_TexelSize;
	    SamplerState SmpClampTrilinear;

		uint3 GetCoord(int index) {
			uint z = index % TEX_DIMENSIONS.z;
			uint y = (index / TEX_DIMENSIONS.z) % TEX_DIMENSIONS.y;
			uint x = index / (TEX_DIMENSIONS.y * TEX_DIMENSIONS.z);
            return uint3(x, y, z);
        }

        float4 GetIndexUV(int index) {
            uint3 coord = GetCoord(index);
            return float4(
		  	  coord.x * TEXEL_SIZE.x,
		  	  coord.y * TEXEL_SIZE.y,
		  	  coord.z * TEXEL_SIZE.z,
		      1
		    );
        }

        float4 GetWorldUV(int index) {
            float3 uv = GetIndexUV(index).xyz;
            // Moves UV into center of texel in world space
            return float4(uv + TEXEL_SIZE * 0.5 + _UV_Offset, 1);
        }

        float3 GetWorldPosition(int index) {
            float3 uv = GetWorldUV(index).xyz;
			return BOUNDS_MIN + float3(
                 uv.x * BOUNDS_SIZE.x,
                 uv.y * BOUNDS_SIZE.y,
                 uv.z * BOUNDS_SIZE.z
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
			float s = magnitude * magnitude;
			float4x4 scale = 0.0;
			scale._m00_m11_m22_m33 = float4(s,s,s,1.0);
            return mul(tmat, scale);
        }

        void setup() {
		#ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
		#ifdef SHADER_API_D3D11

			float3 pos = GetWorldPosition(unity_InstanceID);
			float4 uv = GetWorldUV(unity_InstanceID);
			float3 direction = _NormalDirections.SampleLevel(SmpClampTrilinear, uv, 0, 0.0).rgb * 2 - 1;
			float magnitude = _HeightMagnitudes.SampleLevel(SmpClampTrilinear, uv, 0, 0.0).x;

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
                float4 uv = GetWorldUV(unity_InstanceID);
                strength = _HeightMagnitudes.SampleLevel(SmpClampTrilinear, uv, 0.0, 0).x;
			#endif
			#endif
            o.Albedo = lerp(_ColorMin, _ColorMax, strength);
            o.Alpha = 0.1f;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
