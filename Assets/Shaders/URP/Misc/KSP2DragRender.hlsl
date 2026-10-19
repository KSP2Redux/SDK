// KSP2DragRender.hlsl
//
// The single pass of KSP2/Aerodynamics/DragRender. Math from the verified decompile in
// ksp2-shaders/Aerodynamics_DragRender/final. Also included by the SDK's copy of the shader
// (Packages/ksp2redux.sdk/Assets/Shaders/KSP2/Aerodynamics/DragRender.shader), which carries the
// same name so DragRenderer finds a shader in mod projects too. Both copies must give the same drag cubes.
//
// The including shader declares the UnityPerMaterial block (_BumpMap_ST).

#ifndef KSP2_DRAG_RENDER_INCLUDED
#define KSP2_DRAG_RENDER_INCLUDED

// Set by DragRenderer before the draw.
float _dragCubeNearClip;
float _dragCubeFarClip;

TEXTURE2D(_BumpMap);    SAMPLER(sampler_BumpMap);

struct KSP2DragRenderAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS   : NORMAL;
    float2 uv         : TEXCOORD0;
};

struct KSP2DragRenderVaryings
{
    float4 positionCS : SV_POSITION;
    float2 uv         : TEXCOORD0;
    float3 normalWS   : TEXCOORD1;
    float  viewDepth  : TEXCOORD2;
};

KSP2DragRenderVaryings KSP2DragRender_Vertex(KSP2DragRenderAttributes input)
{
    KSP2DragRenderVaryings output;
    float3 positionWS = mul(UNITY_MATRIX_M, input.positionOS).xyz;
    output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
    output.viewDepth = -dot(UNITY_MATRIX_V[2], mul(UNITY_MATRIX_M, input.positionOS));
    output.uv = input.uv;
    // Unnormalized, rotated by the object-to-world matrix (not its inverse transpose).
    output.normalWS = mul((float3x3)UNITY_MATRIX_M, input.normalOS);
    return output;
}

float4 KSP2DragRender_Fragment(KSP2DragRenderVaryings input) : SV_Target
{
    // DXT5nm decode, z + 1 kept for the blend below.
    float4 packed = SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv * _BumpMap_ST.xy + _BumpMap_ST.zw);
    float2 xy = float2(packed.w * packed.x, packed.y) * 2.0 - 1.0;
    float3 t = float3(xy, sqrt(1.0 - min(dot(xy, xy), 1.0)) + 1.0);

    // Blend the tangent normal onto the world normal without a TBN (stock formula).
    float3 n = normalize(input.normalWS);
    float3 u = float3(-n.x, -n.y, n.z);
    float3 bumped = float3(n.x, n.y, -n.z) + t * dot(t, u) / t.z;

    // Row 2 of the view matrix: the camera z axis in world space.
    float facing = dot(bumped, UNITY_MATRIX_V[2].xyz);
    float depth01 = max(input.viewDepth - _dragCubeNearClip, 0.0) / (_dragCubeFarClip - _dragCubeNearClip);
    return float4(exp2(log2(facing) * 2.2), exp2(log2(depth01) * 2.2), 0.0, 1.0);
}

#endif // KSP2_DRAG_RENDER_INCLUDED