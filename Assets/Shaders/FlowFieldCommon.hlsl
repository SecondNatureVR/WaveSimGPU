#include "UnityCG.cginc"

#ifndef FLOWFIELDCOMMON_INCLDED
#define FLOWFIELDCOMMON_INCLUDED

bool _DrawDebug;
float3 BOUNDS_MIN, BOUNDS_SIZE;
float3 DRAWBOX_MIN, DRAWBOX_SIZE;
float3 TEXEL_SIZE;
float3 _UV_Offset, _UV_Scale;
uint3 TEX_DIMENSIONS;

UNITY_DECLARE_TEX3D_NOSAMPLER(_NormalDirections);
UNITY_DECLARE_TEX3D_NOSAMPLER(_HeightMagnitudes);
SamplerState SmpClampPoint;

#define TSAMP SmpClampPoint
#define FLOW_SAMPLE(texture3D, uv) texture3D.SampleLevel(TSAMP, uv, 0.0, 0)
#define FLOW_SAMPLE_DIRECTION(uv) FLOW_SAMPLE(_NormalDirections, uv)
#define FLOW_SAMPLE_MAGNITUDE(uv) FLOW_SAMPLE(_HeightMagnitudes, uv)

#define GET_CURRENT_MAGNITUDE  _HeightMagnitudes[coord]
#define GET_CURRENT_DIRECTION  decodeDirection(_NormalDirections[coord]).xyz
#define GET_DIRECTION(coord)  decodeDirection(_NormalDirections[coord]).xyz
#define GET_MAGNITUDE(coord) _HeightMagnitudes[coord]
#define SET_DIRECTION(direction) _NormalDirectionsRW[coord] = encodeDirection(direction)
#define SET_MAGNITUDE(magnitude) _HeightMagnitudesRW[coord] = magnitude;

uint3 GetTexCoord(int index) {
    uint z = index % TEX_DIMENSIONS.z;
    uint y = (index / TEX_DIMENSIONS.z) % TEX_DIMENSIONS.y;
    uint x = index / (TEX_DIMENSIONS.y * TEX_DIMENSIONS.z);
    return uint3(x, y, z);
}

float4 GetTexUV(uint3 coord) {
    return float4(
      coord.x * TEXEL_SIZE.x,
      coord.y * TEXEL_SIZE.y,
      coord.z * TEXEL_SIZE.z,
      1
    );
}

float4 GetTexUV(int index) {
    uint3 coord = GetTexCoord(index);
    return GetTexUV(coord);
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

float3 decodeDirection(float4 direction)
{
    return direction.rgb * 2 - 1;
}

float4 encodeDirection(float4 direction)
{
    return (direction + 1) * 0.5;
}

float4 encodeDirection(float3 direction)
{
    return encodeDirection(float4(normalize(direction), 1));
}

float4 encodeDirection(float x, float y, float z)
{
    return encodeDirection(float3(x, y, z));
}

#endif
