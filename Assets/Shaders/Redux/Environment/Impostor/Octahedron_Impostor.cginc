// ============================================================================
// Octahedron_Impostor.cginc
//
// Shared code for Redux/Environment/Impostor/Octahedron_Impostor: instancing
// setup, the camera facing billboard, octahedral frame selection and the frame
// sampling every pass needs.
//
// The math replicates the stock KSP2 octahedron impostor, read from its
// disassembly, so textures baked in the stock layout render identically here.
// Atlas layout is a square grid of _Frames x _Frames views. Frame (x, y) looks
// along the octahedrally decoded direction of its cell, with the object Y axis
// as the pole, which is the surface up for a scatter instance.
// ============================================================================
#ifndef REDUX_OCTAHEDRON_IMPOSTOR_INCLUDED
#define REDUX_OCTAHEDRON_IMPOSTOR_INCLUDED

#include "UnityCG.cginc"

sampler2D _Albedo;
sampler2D _Normals;
sampler2D _Specular;
sampler2D _Emission;

float _Frames;
float _ImpostorSize;
float4 _Offset;
float _DepthSize;
float _TextureBias;
float _Parallax;
float _ClipMask;
float _AI_ShadowBias;
float _AI_ShadowView;

float4 _Color;
float4 _LODDebugColor;
float _Brightness;
float _Contrast;
float _Saturation;
float _Smoothness;
float _SpecularValue;
float4 _SpecularTintColor;

#ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
// One element of the merged billboard buffer Vegetation Studio binds for an
// indirect draw: the instance's object to world matrix, then the LOD fade.
struct ImpostorInstanceData
{
    float4x4 PositionMatrix;
    float4 ControlData;
};

StructuredBuffer<ImpostorInstanceData> VisibleShaderDataBuffer;
#endif

// Inverts a matrix holding only rotation, scale and translation.
float4x4 ImpostorInverseAffine(float4x4 m)
{
    float3 row0 = m._m00_m01_m02;
    float3 row1 = m._m10_m11_m12;
    float3 row2 = m._m20_m21_m22;
    float3x3 inverse = transpose(float3x3(cross(row1, row2), cross(row2, row0), cross(row0, row1)))
        / dot(row0, cross(row1, row2));
    float3 translation = -mul(inverse, m._m03_m13_m23);
    return float4x4(
        float4(inverse[0], translation.x),
        float4(inverse[1], translation.y),
        float4(inverse[2], translation.z),
        float4(0.0, 0.0, 0.0, 1.0));
}

// Procedural instancing entry point, named by instancing_options in each pass.
void setup()
{
#ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
    ImpostorInstanceData instance = VisibleShaderDataBuffer[unity_InstanceID];
    unity_ObjectToWorld = instance.PositionMatrix;
    unity_WorldToObject = ImpostorInverseAffine(instance.PositionMatrix);
    unity_LODFade = instance.ControlData;
#endif
}

struct ImpostorAppData
{
    float4 vertex : POSITION;
    float4 texcoord : TEXCOORD0;
    float4 texcoord1 : TEXCOORD1;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

// --- Octahedral mapping. The 2D coordinates are object X and Z, Y is the pole. ---

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

// Builds the camera facing quad from the mesh UVs and picks the three atlas frames to blend.
// The mesh vertex positions are ignored. Only the UVs place a vertex on the quad, so the
// baked outline mesh can trim the quad to the silhouette.
ImpostorBillboard ImpostorBuildBillboard(float2 uv)
{
    ImpostorBillboard billboard;

    // An orthographic camera has no position, so the view is taken from far along its
    // forward axis instead. The tiny side terms keep the view off the exact pole.
    bool isOrthographic = UNITY_MATRIX_P[3][3] == 1.0;
    float4 cameraWeights = isOrthographic ? float4(1e-5, 1e-5, 100.0, 1e-5) : float4(1e-5, 1e-5, 1e-5, 1.0);
    float3 cameraWorld = mul(UNITY_MATRIX_I_V, cameraWeights).xyz;
    if (isOrthographic)
        cameraWorld += unity_ObjectToWorld._m03_m13_m23;

    float3 cameraObject = mul(unity_WorldToObject, float4(cameraWorld, 1.0)).xyz - _Offset.xyz;
    float3 viewDirection = normalize(cameraObject);
    float3 right = normalize(cross(viewDirection, float3(0.0, 1.0, 0.0)));
    float3 up = cross(right, viewDirection);

    float2 corner = (uv - 0.5) * _ImpostorSize;
    float3 quadPosition = right * corner.x + up * corner.y;
    float3 rayDirection = quadPosition - cameraObject;
    billboard.objectPosition = quadPosition + _Offset.xyz;
    billboard.objectViewDirection = viewDirection;

    // The view direction lands inside one grid triangle. Its three corners are the frames
    // blended, the second picked by which side of the cell diagonal the view falls.
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

// --- Fragment side. ---

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
    depth = tex2Dbias(_Normals, float4(frame.zw, 0.0, -1.0)).a;
    return frame.zw + (0.5 - depth) * frame.xy;
}

float4 ImpostorBlend(sampler2D atlas, float2 uv0, float2 uv1, float2 uv2, float3 weights)
{
    return tex2Dbias(atlas, float4(uv0, 0.0, _TextureBias)) * weights.x
        + tex2Dbias(atlas, float4(uv1, 0.0, _TextureBias)) * weights.y
        + tex2Dbias(atlas, float4(uv2, 0.0, _TextureBias)) * weights.z;
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

// Discards the fragment where the blended albedo alpha falls under the clip threshold.
void ImpostorClipAlpha(ImpostorFragmentUV uv)
{
    float alpha = ImpostorBlend(_Albedo, uv.uv0, uv.uv1, uv.uv2, uv.weights).a;
    clip(alpha - _ClipMask);
}

// Dithered LOD crossfade, faded by the instance's fade value under procedural instancing.
void ImpostorApplyLodFade(float2 pixel)
{
#ifdef LOD_FADE_CROSSFADE
    float mask = tex2D(unity_DitherMask, pixel * 0.25).a;
    float fade = unity_LODFade.x;
    clip(fade - (fade > 0.0 ? 1.0 : -1.0) * mask);
#endif
}

// Device depth of the fragment once the baked depth pushes it off the quad.
float ImpostorDeviceDepth(float3 viewPosition, float viewDepthOffset)
{
    float4 clipPosition = mul(UNITY_MATRIX_P, float4(viewPosition.xy, viewPosition.z + viewDepthOffset, 1.0));
    return clipPosition.z / (clipPosition.w + 1e-5);
}

// View space length of the baked depth range, scaled by the instance.
float ImpostorDepthOffset(float bakedDepth)
{
    return (bakedDepth - 0.5) * _DepthSize * length(unity_ObjectToWorld._m20_m21_m22);
}

struct ImpostorSurface
{
    float3 albedo;
    float3 specular;
    float smoothness;
    float occlusion;
    float3 emission;
    float3 worldNormal;
    float depth;
};

// Samples and grades every atlas for a lit pass, clipping cut out texels.
// Stock's own register names read as if these two were swapped. The texels settle it:
// _Specular holds specular and smoothness, _Emission holds emission and occlusion.
ImpostorSurface ImpostorSampleSurface(float4 frame0, float4 frame1, float4 frame2, float2 grid)
{
    ImpostorFragmentUV uv = ImpostorResolveUV(frame0, frame1, frame2, grid);
    float4 albedo = ImpostorBlend(_Albedo, uv.uv0, uv.uv1, uv.uv2, uv.weights);
    clip(albedo.a - _ClipMask);

    float3 tinted = saturate(albedo.rgb * _Color.rgb * _LODDebugColor.rgb * _Brightness);
    float luminance = dot(tinted, float3(0.2125, 0.7154, 0.0721));
    float3 saturated = lerp(luminance, tinted, _Saturation);
    float3 graded = saturated * saturate(_Contrast * (saturated - 0.5) + 0.5);

    float4 specular = ImpostorBlend(_Specular, uv.uv0, uv.uv1, uv.uv2, uv.weights);
    float4 emission = ImpostorBlend(_Emission, uv.uv0, uv.uv1, uv.uv2, uv.weights);
    float3 objectNormal = ImpostorBlend(_Normals, uv.uv0, uv.uv1, uv.uv2, uv.weights).rgb * 2.0 - 1.0;

    ImpostorSurface surface;
    surface.albedo = graded;
    surface.specular = saturate(specular.rgb + _SpecularValue) * _SpecularTintColor.rgb;
    surface.smoothness = saturate(specular.a + _Smoothness);
    surface.occlusion = emission.a;
    surface.emission = emission.rgb;
    surface.worldNormal = normalize(mul((float3x3)unity_ObjectToWorld, objectNormal));
    surface.depth = uv.depth;
    return surface;
}

#endif // REDUX_OCTAHEDRON_IMPOSTOR_INCLUDED
