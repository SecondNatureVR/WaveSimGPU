Shader "Instanced/FlowVector"
{
    Properties
    {
        _NormalDirections ("_NormalDirections", 3D) = "white" {}
        _HeightMagnitudes("_HeightMagnitudes", 3D) = "white" {}
        _ColorMin ("ColorMin", Color) = (0,0,1,1)
        _ColorMax ("ColorMax", Color) = (1,0,0,1)
        _SmoothMin ("SmoothMin", float) = 0
        _SmoothMax ("SmoothMax", float) = 0
        _MagScale ("MagScale", float) = 1
        _MagCurve ("MagCurve", float) = 1
        [MaterialToggle] _FlipMag ("FlipMagnitude", float) = 0
        [MaterialToggle] _DrawDebug ("DrawDebug", float) = 0
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
            float3 velocity;
        };

        float _MagScale, _MagCurve;
        bool _FlipMag, _DrawDebug;
        float _SmoothMin, _SmoothMax;
        fixed4 _ColorMin, _ColorMax;
        float4 _UV_Offset, _UV_Scale;
        float3 BOUNDS_MIN, BOUNDS_EXTENTS, BOUNDS_SIZE;
        float3 DRAWBOX_MIN, DRAWBOX_EXTENTS, DRAWBOX_SIZE;
        float3 TEXEL_SIZE;
        uint3 TEX_DIMENSIONS;



        UNITY_DECLARE_TEX3D_NOSAMPLER(_NormalDirections);
        UNITY_DECLARE_TEX3D_NOSAMPLER(_HeightMagnitudes);
        UNITY_DECLARE_TEX3D_NOSAMPLER(_Velocity);
        float4 _NormalDirections_TexelSize;
	    SamplerState SmpClampPoint;

        #define TSAMP SmpClampPoint
        #define FLOW_SAMPLE(texture3D, uv) texture3D.SampleLevel(TSAMP, uv, 0.0, 0)
        #define FLOW_SAMPLE_DIRECTION(uv) FLOW_SAMPLE(_NormalDirections, uv)
        #define FLOW_SAMPLE_MAGNITUDE(uv) FLOW_SAMPLE(_HeightMagnitudes, uv)
        #define FLOW_SAMPLE_VELOCITY(uv) FLOW_SAMPLE(_Velocity, uv)

        // Debug
		#ifdef SHADER_API_D3D11

		StructuredBuffer<float4> _backStepPos;

        #endif

		uint3 GetTexCoord(int index) {
			uint z = index % TEX_DIMENSIONS.z;
			uint y = (index / TEX_DIMENSIONS.z) % TEX_DIMENSIONS.y;
			uint x = index / (TEX_DIMENSIONS.y * TEX_DIMENSIONS.z);
            return uint3(x, y, z);
        }

        float4 GetTexUV(int index) {
            uint3 coord = GetTexCoord(index);
            return float4(
		  	  coord.x * TEXEL_SIZE.x,
		  	  coord.y * TEXEL_SIZE.y,
		  	  coord.z * TEXEL_SIZE.z,
		      1
		    );
        }

        float4 GetWorldUV(int index) {
			float3 uv = GetTexUV(index).xyz;
			// Map UV into center of world space voxel
			uv = uv + TEXEL_SIZE * 0.5 + _UV_Offset;
			uv = float3(
				uv.x * _UV_Scale.x,
				uv.y * _UV_Scale.y,
				uv.z * _UV_Scale.z
			);
            // remap uvs into drawbox
            if (_DrawDebug) {
                float3 toDbox = float3(
					DRAWBOX_SIZE.x / BOUNDS_SIZE.x,
					DRAWBOX_SIZE.y / BOUNDS_SIZE.y,
					DRAWBOX_SIZE.z / BOUNDS_SIZE.z
                );
                float3 worldOffset = DRAWBOX_MIN - BOUNDS_MIN;
                uv = float3(uv.x * toDbox.x, uv.y * toDbox.y, uv.z * toDbox.z);
                uv += float3(
                    worldOffset.x / BOUNDS_SIZE.x,
                    worldOffset.y / BOUNDS_SIZE.y,
                    worldOffset.z / BOUNDS_SIZE.z
                );
			}
            return float4(uv, 1);
        }

        float3 GetWorldPosition(int index) {
            float3 uv = GetWorldUV(index).xyz;
			return BOUNDS_MIN + float3(
                 uv.x * BOUNDS_SIZE.x,
                 uv.y * BOUNDS_SIZE.y,
                 uv.z * BOUNDS_SIZE.z
		    );
		}

        float3 invLerp(float3 from, float3 to, float3 value) {
            return (value - from) / (to - from);
        }

        // Example: Let the bounding box be divided into texture dimension 3D cells
        // Where each cell has dimensions WORLD_TEXEL_SIZE
        // Let c = centroid at the first 3D cell with corner at BOUNDS_MIN
        // GetTexUVFromWorldPosition(c.worldPos) = (0, 0, 0)
        // GetTexUVFromWorldPosition(c.worldPos + WORLD_TEXEL_SIZE) = TEXEL_SIZE
        // GetTexUVFromWorldPosition(BOUNDS_MAX - c.worldPosition) = (1, 1, 1)
        float3 GetTexUVFromWorldPosition(float3 pos) {
            float3 WORLD_TEXEL_SIZE = float3(
                BOUNDS_SIZE.x / TEX_DIMENSIONS.x,
                BOUNDS_SIZE.y / TEX_DIMENSIONS.y,
                BOUNDS_SIZE.z / TEX_DIMENSIONS.z
            );
            float3 offsetToCenter = WORLD_TEXEL_SIZE * 0.5;
            float3 fromTexInWorldPos = BOUNDS_MIN + offsetToCenter;
            float3 toTexInWorldPos = BOUNDS_MIN + BOUNDS_SIZE - offsetToCenter;
            return invLerp(fromTexInWorldPos, toTexInWorldPos, pos);
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
            float s = lerp(_SmoothMin, _SmoothMax, min(pow(magnitude, _MagCurve), 1.2)) * _MagScale;
            if (_DrawDebug) {
                s *= min(DRAWBOX_SIZE.x, min(DRAWBOX_SIZE.y, DRAWBOX_SIZE.z))
                   / max(BOUNDS_SIZE.x, max(BOUNDS_SIZE.y, BOUNDS_SIZE.z));
            }
			float4x4 scale = 0.0;
			scale._m00_m11_m22_m33 = float4(s,s,s,1.0);
            return mul(tmat, scale);
        }

        void setup() {
		#ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
		#ifdef SHADER_API_D3D11
            float3 pos;
            if (_DrawDebug) {
                pos = _backStepPos[unity_InstanceID].xyz;
            } else {
				pos = GetWorldPosition(unity_InstanceID);
            }
			float4 uv = float4(GetTexUVFromWorldPosition(pos), 1);
            float3 velocity = FLOW_SAMPLE_VELOCITY(uv);
			float3 direction = FLOW_SAMPLE_DIRECTION(uv).rgb * 2 - 1;
			float magnitude = FLOW_SAMPLE_MAGNITUDE(uv);

            if (_FlipMag)
                magnitude = 1 - magnitude;

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
			float magnitude = 0.5f;
			#ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
			#ifdef SHADER_API_D3D11
                float4 uv = GetWorldUV(unity_InstanceID);
                magnitude = FLOW_SAMPLE_MAGNITUDE(uv);
                if (_FlipMag)
                    magnitude = 1 - magnitude;
			#endif
			#endif
            o.Albedo = lerp(_ColorMin, _ColorMax, magnitude);
            o.Alpha = 0.1f;
        }
        ENDCG
    }
    FallBack "Diffuse"
}