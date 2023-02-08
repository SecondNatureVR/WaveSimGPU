Shader "Instanced/FlowVector"
{
    Properties
    {
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _ColorMin ("ColorMin", Color) = (0,0,1,1)
        _ColorMax ("ColorMax", Color) = (1,0,0,1)
        _SmoothMin ("SmoothMin", float) = 0
        _SmoothMax ("SmoothMax", float) = 0
        _MagScale ("MagScale", float) = 1
        _MagCurve ("MagCurve", float) = 1
    }
    SubShader
    {
		Blend SrcAlpha OneMinusSrcAlpha
        Tags { "RenderType"="TransparentCutout" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard nolightmap nometa noforwardadd keepalpha fullforwardshadows addshadow vertex:vert
		#pragma multi_compile_instancing
		#pragma instancing_options assumeuniformscaling procedural:setup
		#pragma target 4.5

        sampler2D _MainTex;
		struct Input {
            float2 uv_MainTex;
            fixed4 vertexColor;
        };
        
        
        #include "UnityStandardParticleInstancing.cginc"
		#include "UnityCG.cginc"
		#include "UnityLightingCommon.cginc"
		#include "AutoLight.cginc"

        float _MagScale, _MagCurve;
        float _SmoothMin, _SmoothMax;
        fixed4 _ColorMin, _ColorMax;

        // Debug
        struct Particle {
            float3 position;
            float3 velocity;
        };
		#ifdef SHADER_API_D3D11

		StructuredBuffer<Particle> _Particles;

        #endif

        #include "Assets/Shaders/FlowFieldCommon.hlsl"

        // Rotate by finding common plane and angle
		// https://math.stackexchange.com/questions/180418/calculate-rotation-matrix-to-align-vector-a-to-vector-b-in-3d
        float4x4 SetRotationToDirection(float3 direction, inout float4x4 tmat) {
            float3x3 rmat = 0.0;
            rmat._m00_m11_m22 = float3(1, 1, 1);

            float3 a = float3(0, 0, -1);
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
			float4x4 scale = 0.0;
			scale._m00_m11_m22_m33 = float4(s,s,s,1.0);
            return mul(tmat, scale);
        }

        void setup() {
		#ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
		#ifdef SHADER_API_D3D11
            Particle p = _Particles[unity_InstanceID];
            float3 pos = p.position;
			float3 toCamera = _WorldSpaceCameraPos - pos;
			float3 direction = normalize(toCamera);
			float magnitude = length(p.velocity);

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

        void vert (inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            vertInstancingUVs(v.texcoord, o.uv_MainTex);
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
			float magnitude = 0.5f;
			#ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
			#ifdef SHADER_API_D3D11
                float3 uv = GetTexUVFromWorldPosition(_Particles[unity_InstanceID].position);
                magnitude = FLOW_SAMPLE_MAGNITUDE(uv);
			#endif
			#endif
            fixed4 tc = tex2D(_MainTex, IN.uv_MainTex);
			clip(tc.a - 0.99);
            o.Albedo = tc.rgb * lerp(_ColorMin, _ColorMax, magnitude).rgb;
            o.Alpha = tc.a;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
