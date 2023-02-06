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

        float _MagScale, _MagCurve;
        bool _FlipMag;
        float _SmoothMin, _SmoothMax;
        fixed4 _ColorMin, _ColorMax;

        // Debug
		#ifdef SHADER_API_D3D11

		StructuredBuffer<float4> _backStepPos;

        #endif

        #include "Assets/Shaders/FlowFieldCommon.hlsl"

        // Rotate by finding common plane and angle
		// https://math.stackexchange.com/questions/180418/calculate-rotation-matrix-to-align-vector-a-to-vector-b-in-3d
        float4x4 SetRotationToDirection(float3 direction, inout float4x4 tmat) {
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
