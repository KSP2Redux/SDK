// KSP2OctahedronImpostor.hlsl
//
// Passes of Hidden/Amplify Impostors/Octahedron Impostor, the far LOD of the terrain scatter that
// Vegetation Studio draws as a custom billboard with Graphics.DrawMeshInstancedIndirect. A camera
// facing quad picks the three nearest of _Frames x _Frames baked octahedral views, blends them, clips
// on albedo alpha and writes the baked depth through SV_Depth.
// Math from the stock disassembly (ksp2-shaders/Reference/Hidden_Amplify Impostors_Octahedron Impostor.shader),
// as already replicated by Redux's BRP stand-in Redux/Environment/Impostor/Octahedron_Impostor.
// The atlas layout is a square grid of _Frames x _Frames views. Frame (x, y) looks along the
// octahedrally decoded direction of its cell, with the object Y axis as the pole. The stock register
// names read as if _Specular and _Emission were swapped; the texels settle it: _Specular holds
// specular and smoothness, _Emission holds emission and occlusion.
// The including shader declares the UnityPerMaterial block. Include KSP2Lighting.hlsl first in the
// forward pass.

#ifndef KSP2_OCTAHEDRON_IMPOSTOR_INCLUDED
#define KSP2_OCTAHEDRON_IMPOSTOR_INCLUDED

#include "KSP2ScatterCommon.hlsl"

TEXTURE2D(_Albedo);     SAMPLER(sampler_Albedo);
TEXTURE2D(_Normals);    SAMPLER(sampler_Normals);
TEXTURE2D(_Specular);   SAMPLER(sampler_Specular);
TEXTURE2D(_Emission);   SAMPLER(sampler_Emission);

struct ImpostorAttributes
{
    float4 positionOS : POSITION;   // ignored, the UVs place the vertex on the quad
    float4 uv0        : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct ImpostorVaryings
{
    float4 positionCS   : SV_POSITION;
    float4 frame0       : TEXCOORD0; // xy parallax step, zw atlas UV
    float4 frame1       : TEXCOORD1;
    float4 frame2       : TEXCOORD2;
    float2 grid         : TEXCOORD3;
    float3 viewPosition : TEXCOORD4;
    float3 positionWS   : TEXCOORD5;
    float  fogFactor    : TEXCOORD6;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

// ---------------------------------------------------------------------------
// Octahedral mapping. The 2D coordinates are object X and Z, Y is the pole.
// ---------------------------------------------------------------------------
float2 ImpostorOctahedronEncode(float3 direction)
{
    direction /= dot(1.0, abs(direction));
    float2 octahedron = direction.xz;
    if (direction.y <= 0.0)
        octahedron = (1.0 - abs(octahedron.yx)) * (octahedron >= 0.0 ? 1.0 : -1.0);
    return octahedron;
}

float3 ImpostorOctahedronDecode(float2 octahedron)
{
    float pole = 1.0 - dot(1.0, abs(octahedron));
    if (pole < 0.0)
        octahedron = (1.0 - abs(octahedron.yx)) * (octahedron >= 0.0 ? 1.0 : -1.0);
    return normalize(float3(octahedron.x, pole, octahedron.y));
}

// Projects the view ray onto the plane of one atlas frame.
// Returns the parallax step in xy and the atlas UV in zw.
float4 ImpostorFrame(float2 cell, float3 rayOrigin, float3 rayDirection)
{
    float3 frameNormal = ImpostorOctahedronDecode(cell / (_Frames - 1.0) * 2.0 - 1.0);
    float3 frameTangent = normalize(cross(float3(0.0, 1.0, 0.0), frameNormal) + float3(-0.001, 0.0, 0.0));
    float3 frameBitangent = cross(frameTangent, frameNormal);

    float rayDotNormal = dot(rayDirection, frameNormal);
    float hitDistance = -dot(rayOrigin, frameNormal) / rayDotNormal;
    float3 hit = rayDirection * hitDistance + rayOrigin;

    // A ray that never reaches the plane samples the frame centre.
    float2 planeUV = hitDistance <= 0.0 ? 0.0 : -float2(dot(hit, frameTangent), dot(hit, frameBitangent));
    float3 localRay = normalize(float3(dot(rayDirection, frameTangent), dot(rayDirection, frameBitangent), rayDotNormal));

    float frameScale = 1.0 / _Frames;
    float2 atlasUV = (planeUV / _ImpostorSize + 0.5) * frameScale + cell * frameScale;
    float2 parallaxStep = localRay.xy * frameScale * -_Parallax;
    return float4(parallaxStep, atlasUV);
}

struct ImpostorBillboard
{
    float3 objectPosition;
    float3 objectViewDirection;
    float4 frame0;
    float4 frame1;
    float4 frame2;
    float2 grid;
};

// Builds the view facing quad from the mesh UVs and picks the three atlas frames to blend. In the
// shadow caster pass the view is the light's, so the quad faces the light.
ImpostorBillboard ImpostorBuildBillboard(float2 uv)
{
    ImpostorBillboard billboard;

    // An orthographic view has no position, so the view is taken from far along its forward axis.
    // The tiny side terms keep the view off the exact pole.
    bool isOrthographic = UNITY_MATRIX_P[3][3] == 1.0;
    float4 cameraWeights = isOrthographic ? float4(1e-5, 1e-5, 100.0, 1e-5) : float4(1e-5, 1e-5, 1e-5, 1.0);
    float3 cameraWorld = mul(UNITY_MATRIX_I_V, cameraWeights).xyz;
    if (isOrthographic)
        cameraWorld += UNITY_MATRIX_M._m03_m13_m23;

    float3 cameraObject = mul(UNITY_MATRIX_I_M, float4(cameraWorld, 1.0)).xyz - _Offset.xyz;
    float3 viewDirection = normalize(cameraObject);
    float3 right = normalize(cross(viewDirection, float3(0.0, 1.0, 0.0)));
    float3 up = cross(right, viewDirection);

    float2 corner = (uv - 0.5) * _ImpostorSize;
    float3 quadPosition = right * corner.x + up * corner.y;
    float3 rayDirection = quadPosition - cameraObject;
    billboard.objectPosition = quadPosition + _Offset.xyz;
    billboard.objectViewDirection = viewDirection;

    // The view direction lands inside one grid triangle. Its three corners are the frames blended,
    // the second picked by which side of the cell diagonal the view falls.
    float2 grid = (_Frames - 1.0) * (ImpostorOctahedronEncode(viewDirection) * 0.5 + 0.5);
    float2 cell = floor(grid);
    float2 fraction = frac(grid);
    float diagonalStep = ceil(fraction.x - fraction.y);
    billboard.frame0 = ImpostorFrame(cell, cameraObject, rayDirection);
    billboard.frame1 = ImpostorFrame(cell + float2(diagonalStep, 1.0 - diagonalStep), cameraObject, rayDirection);
    billboard.frame2 = ImpostorFrame(cell + 1.0, cameraObject, rayDirection);
    billboard.grid = grid;
    return billboard;
}

// Barycentric weights of frame0, frame1 and frame2 for the interpolated grid position.
float3 ImpostorFrameWeights(float2 grid)
{
    float2 fraction = frac(grid);
    return float3(
        min(1.0 - fraction.y, 1.0 - fraction.x),
        abs(fraction.x - fraction.y),
        min(fraction.y, fraction.x));
}

// Offsets a frame's UV by its stored depth, returning the depth through the out parameter.
float2 ImpostorParallaxUV(float4 frame, out float depth)
{
    depth = SAMPLE_TEXTURE2D_BIAS(_Normals, sampler_Normals, frame.zw, -1.0).a;
    return frame.zw + (0.5 - depth) * frame.xy;
}

struct ImpostorFragmentUV
{
    float2 uv0;
    float2 uv1;
    float2 uv2;
    float3 weights;
    float depth;
};

ImpostorFragmentUV ImpostorResolveUV(float4 frame0, float4 frame1, float4 frame2, float2 grid)
{
    ImpostorFragmentUV result;
    float depth0;
    float depth1;
    float depth2;
    result.uv0 = ImpostorParallaxUV(frame0, depth0);
    result.uv1 = ImpostorParallaxUV(frame1, depth1);
    result.uv2 = ImpostorParallaxUV(frame2, depth2);
    result.weights = ImpostorFrameWeights(grid);
    result.depth = dot(float3(depth0, depth1, depth2), result.weights);
    return result;
}

#define IMPOSTOR_BLEND(atlas, uv) \
    (SAMPLE_TEXTURE2D_BIAS(atlas, sampler##atlas, uv.uv0, _TextureBias) * uv.weights.x \
    + SAMPLE_TEXTURE2D_BIAS(atlas, sampler##atlas, uv.uv1, _TextureBias) * uv.weights.y \
    + SAMPLE_TEXTURE2D_BIAS(atlas, sampler##atlas, uv.uv2, _TextureBias) * uv.weights.z)

// Discards the fragment where the blended albedo alpha falls under the clip threshold.
void ImpostorClipAlpha(ImpostorFragmentUV uv)
{
    clip(IMPOSTOR_BLEND(_Albedo, uv).a - _ClipMask);
}

// View space length of the baked depth range, scaled by the instance.
float ImpostorDepthOffset(float bakedDepth)
{
    return (bakedDepth - 0.5) * _DepthSize * length(UNITY_MATRIX_M._m20_m21_m22);
}

// Device depth of the fragment once the baked depth pushes it off the quad.
float ImpostorDeviceDepth(float3 viewPosition, float viewDepthOffset)
{
    float4 clipPosition = mul(UNITY_MATRIX_P, float4(viewPosition.xy, viewPosition.z + viewDepthOffset, 1.0));
    return clipPosition.z / (clipPosition.w + 1e-5);
}

// World normal of the blended baked object space normals.
float3 ImpostorNormalWS(ImpostorFragmentUV uv)
{
    float3 objectNormal = IMPOSTOR_BLEND(_Normals, uv).rgb * 2.0 - 1.0;
    return normalize(mul((float3x3)UNITY_MATRIX_M, objectNormal));
}

// ---------------------------------------------------------------------------
// Vertex stages
// ---------------------------------------------------------------------------
ImpostorVaryings ImpostorVertex(ImpostorAttributes input)
{
    ImpostorVaryings output = (ImpostorVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);

    ImpostorBillboard billboard = ImpostorBuildBillboard(input.uv0.xy);
    float3 positionWS = TransformObjectToWorld(billboard.objectPosition);
    output.positionCS = TransformWorldToHClip(positionWS);
    output.frame0 = billboard.frame0;
    output.frame1 = billboard.frame1;
    output.frame2 = billboard.frame2;
    output.grid = billboard.grid;
    output.viewPosition = TransformWorldToView(positionWS);
    output.positionWS = positionWS;
    output.fogFactor = ComputeFogFactor(output.positionCS.z);
    return output;
}

ImpostorVaryings ImpostorShadowVertex(ImpostorAttributes input)
{
    ImpostorVaryings output = (ImpostorVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);

    ImpostorBillboard billboard = ImpostorBuildBillboard(input.uv0.xy);
    float3 positionWS = TransformObjectToWorld(billboard.objectPosition);
    float3 normalWS = normalize(mul((float3x3)UNITY_MATRIX_M, billboard.objectViewDirection));
#if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
#else
    float3 lightDirectionWS = _LightDirection;
#endif
    positionWS = ApplyShadowBias(positionWS, normalWS, lightDirectionWS);
    output.positionCS = ApplyShadowClamping(TransformWorldToHClip(positionWS));
    output.frame0 = billboard.frame0;
    output.frame1 = billboard.frame1;
    output.frame2 = billboard.frame2;
    output.grid = billboard.grid;
    output.viewPosition = TransformWorldToView(positionWS);
    output.positionWS = positionWS;
    return output;
}

// ---------------------------------------------------------------------------
// Fragment stages
// ---------------------------------------------------------------------------
struct ImpostorColorOutput
{
    float4 color : SV_Target;
    float  depth : SV_Depth;
};

#if defined(KSP2_URP_LIGHTING_INCLUDED)
#if defined(KSP2_GBUFFER_PASS)
    #include "../Include/KSP2GBuffer.hlsl"
#endif

KSP2Surface ImpostorSurface(ImpostorVaryings input, out float3 normalWS, out float3 viewDirWS, out float depth)
{
    UNITY_SETUP_INSTANCE_ID(input);
    KSP2Scatter_LODCrossFade(input.positionCS);

    ImpostorFragmentUV uv = ImpostorResolveUV(input.frame0, input.frame1, input.frame2, input.grid);
    float4 albedo = IMPOSTOR_BLEND(_Albedo, uv);
    clip(albedo.a - _ClipMask);

    // Tint, brightness, saturation and contrast grade, as on the scatter it stands in for.
    float3 tinted = saturate(albedo.rgb * _Color.rgb * _LODDebugColor.rgb * _Brightness);
    float luminance = dot(tinted, float3(0.2125, 0.7154, 0.0721));
    float3 saturated = lerp(luminance, tinted, _Saturation);
    float3 graded = saturated * saturate(_Contrast * (saturated - 0.5) + 0.5);

    float4 specular = IMPOSTOR_BLEND(_Specular, uv);
    float4 emission = IMPOSTOR_BLEND(_Emission, uv);
    float3 specularColor = saturate(specular.rgb + _SpecularValue) * _SpecularTintColor.rgb;
    float smoothness = saturate(specular.a + _Smoothness);

    KSP2Surface surface = KSP2Scatter_MakeSpecularSurface(graded, specularColor, smoothness, emission.a, emission.rgb);
    normalWS = ImpostorNormalWS(uv);
    viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
    depth = ImpostorDeviceDepth(input.viewPosition, ImpostorDepthOffset(uv.depth));
    return surface;
}

ImpostorColorOutput ImpostorForwardFragment(ImpostorVaryings input)
{
    float3 normalWS, viewDirWS;
    float depth;
    KSP2Surface surface = ImpostorSurface(input, normalWS, viewDirWS, depth);
    float3 color = KSP2_Shade(input.positionWS, input.positionCS, normalWS, viewDirWS, surface);

    ImpostorColorOutput output;
    output.color = float4(MixFog(color, input.fogFactor), 1.0);
    output.depth = depth;
    return output;
}

#if defined(KSP2_GBUFFER_PASS)
GBufferFragOutput ImpostorGBufferFragment(ImpostorVaryings input, out float depth : SV_Depth)
{
    float3 normalWS, viewDirWS;
    KSP2Surface surface = ImpostorSurface(input, normalWS, viewDirWS, depth);
    GBufferFragOutput output = KSP2_GBufferOutput(surface, input.positionWS, input.positionCS, normalWS, viewDirWS);
#if defined(GBUFFER_FEATURE_DEPTH)
    output.depth = depth;
#endif
    return output;
}
#endif
#endif

// A directional light scales the baked depth by Shadow View and pulls the caster back by Shadow Bias.
float4 ImpostorShadowFragment(ImpostorVaryings input, out float outDepth : SV_Depth) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    KSP2Scatter_LODCrossFade(input.positionCS);
    ImpostorFragmentUV uv = ImpostorResolveUV(input.frame0, input.frame1, input.frame2, input.grid);
    ImpostorClipAlpha(uv);

    float depthOffset = ImpostorDepthOffset(uv.depth);
    if (IsDirectionalLight())
        depthOffset = depthOffset * _AI_ShadowView - _AI_ShadowBias;

    float4 clipPosition = mul(UNITY_MATRIX_P, float4(input.viewPosition.xy, input.viewPosition.z + depthOffset, 1.0));
    clipPosition = ApplyShadowClamping(clipPosition);
    outDepth = clipPosition.z / (clipPosition.w + 1e-5);
    return 0;
}

ImpostorColorOutput ImpostorDepthFragment(ImpostorVaryings input)
{
    UNITY_SETUP_INSTANCE_ID(input);
    KSP2Scatter_LODCrossFade(input.positionCS);
    ImpostorFragmentUV uv = ImpostorResolveUV(input.frame0, input.frame1, input.frame2, input.grid);
    ImpostorClipAlpha(uv);

    ImpostorColorOutput output;
    output.depth = ImpostorDeviceDepth(input.viewPosition, ImpostorDepthOffset(uv.depth));
    output.color = output.depth;
    return output;
}

ImpostorColorOutput ImpostorDepthNormalsFragment(ImpostorVaryings input)
{
    UNITY_SETUP_INSTANCE_ID(input);
    KSP2Scatter_LODCrossFade(input.positionCS);
    ImpostorFragmentUV uv = ImpostorResolveUV(input.frame0, input.frame1, input.frame2, input.grid);
    ImpostorClipAlpha(uv);

    ImpostorColorOutput output;
    output.color = float4(ImpostorNormalWS(uv), 0.0);
    output.depth = ImpostorDeviceDepth(input.viewPosition, ImpostorDepthOffset(uv.depth));
    return output;
}

#endif // KSP2_OCTAHEDRON_IMPOSTOR_INCLUDED