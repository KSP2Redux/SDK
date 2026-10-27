// KSP2CelestialBodyLocalInput.hlsl
//
// UnityPerMaterial block of KSP2/Environment/CelestialBody/CelestialBody_Local. A file of its own so the
// port and the shaders that stand in for it share one layout: the SDK's
// Redux/Environment/CelestialBody_Local and Redux/Dummy/CelestialBody_Local.
//
// Included from the SubShader HLSLINCLUDE after URP Core.hlsl and before
// KSP2CelestialBodyLocalCommon.hlsl.

#ifndef KSP2_CELESTIALBODY_LOCAL_INPUT_INCLUDED
#define KSP2_CELESTIALBODY_LOCAL_INPUT_INCLUDED

CBUFFER_START(UnityPerMaterial)
    float4 _AlbedoScaledFadeParams;
    float4 _NormalScaledFadeParams;
    float4 _PackedScaledFadeParams;
    float4 _EmissionScaledFadeParams;
    float4 _BiomeCutoffs;
    float4 _LargeHeightMapUVScales;
    float4 _MediumHeightMapUVScales;
    float4 _Subzone3HeightMapUVScales;
    float4 _Subzone4HeightMapUVScales;
    float4 _TriplanarUVScaleOffset;
    float4 _Subzone1NormalIndices;
    float4 _Subzone2NormalIndices;
    float4 _Subzone3NormalIndices;
    float4 _Subzone4NormalIndices;
    float4 _LargeNormalRUVParams;
    float4 _LargeNormalGUVParams;
    float4 _LargeNormalBUVParams;
    float4 _LargeNormalAUVParams;
    float4 _LargeNormalRFadeParams;
    float4 _LargeNormalGFadeParams;
    float4 _LargeNormalBFadeParams;
    float4 _LargeNormalAFadeParams;
    float4 _LargeRSubzoneFilter;
    float4 _LargeGSubzoneFilter;
    float4 _LargeBSubzoneFilter;
    float4 _LargeASubzoneFilter;
    float4 _MidNormalRUVParams;
    float4 _MidNormalGUVParams;
    float4 _MidNormalBUVParams;
    float4 _MidNormalAUVParams;
    float4 _MidNormalRFadeParams;
    float4 _MidNormalGFadeParams;
    float4 _MidNormalBFadeParams;
    float4 _MidNormalAFadeParams;
    float4 _MidRSubzoneFilter;
    float4 _MidGSubzoneFilter;
    float4 _MidBSubzoneFilter;
    float4 _MidASubzoneFilter;
    float4 _Subzone3NormalRUVParams;
    float4 _Subzone3NormalGUVParams;
    float4 _Subzone3NormalBUVParams;
    float4 _Subzone3NormalAUVParams;
    float4 _Subzone3NormalRFadeParams;
    float4 _Subzone3NormalGFadeParams;
    float4 _Subzone3NormalBFadeParams;
    float4 _Subzone3NormalAFadeParams;
    float4 _Subzone3RSubzoneFilter;
    float4 _Subzone3GSubzoneFilter;
    float4 _Subzone3BSubzoneFilter;
    float4 _Subzone3ASubzoneFilter;
    float4 _Subzone4NormalRUVParams;
    float4 _Subzone4NormalGUVParams;
    float4 _Subzone4NormalBUVParams;
    float4 _Subzone4NormalAUVParams;
    float4 _Subzone4NormalRFadeParams;
    float4 _Subzone4NormalGFadeParams;
    float4 _Subzone4NormalBFadeParams;
    float4 _Subzone4NormalAFadeParams;
    float4 _Subzone4RSubzoneFilter;
    float4 _Subzone4GSubzoneFilter;
    float4 _Subzone4BSubzoneFilter;
    float4 _Subzone4ASubzoneFilter;
    float4 _DecalFadeParams;
    // The biome detail material. The material stores it as named _Small properties, which stay in the Properties
    // blocks for stock bundles, authoring and the inspector. PqsRender packs them and binds the arrays with every draw,
    // so the shader can index them by the biome channel it is shading instead of branching over four sets. Each
    // array is the property family's name with _Small replaced by _CBL, channels R G B A, layers 1 to 4:
    //   [channel]                              _Small<Family><C>, xyzw = layers
    //   [channel * 4 + layer]                  _Small<Family><C><L>, or _SmallBiome<C><Family><L>
    //   [(channel * 4 + layer) * 4 + subzone]  _SmallSubzoneTint<C><L>_<S>, subzones R G B A
    // Tints are colour properties, packed in linear space as Unity binds a colour property.
    float4 _CBLBiome[4];
    float4 _CBLEnable[4];
    float4 _CBLHeightWeight[4];
    float4 _CBLWeightSoftness[4];
    float4 _CBLUVScale[4];
    float4 _CBLUVOffset[4];
    float4 _CBLBrightness[4];
    float4 _CBLContrast[4];
    float4 _CBLSaturation[4];
    float4 _CBLNormalStrength[4];
    float4 _CBLMetallicStrength[4];
    float4 _CBLGlossStrength[4];
    float4 _CBLAOStrength[4];
    float4 _CBLDistanceResampleMax[4];
    float4 _CBLBiomeHeightEnable[4];
    float4 _CBLBiomeSlopeEnable[4];
    float4 _CBLTint[16];
    float4 _CBLSubzoneWeight[16];
    float4 _CBLSubzoneBrightness[16];
    float4 _CBLBiomeHeightParams[16];
    float4 _CBLBiomeSlopeParams[16];
    float4 _CBLBiomeGradMapWeights[16];
    float4 _CBLSubzoneTint[64];
    float4 _HeightblendFactor;
    float4 _DistanceResampleDistances;
    float4 _DistanceResampleUVScales;
    float4 _DistanceResampleAlbedoOpacity;
    float4 _DistanceResampleNormalOpacity;
    float4 _GlobalBlend;
    float4 _AlphaToHeightFadeParams;
    float4 _DistanceResampleFades;
    float4 _DistanceResampleFadeRangesPos;
    float4 _DistanceResampleFadeRangesNeg;
    float  _EmissionScale;
    float  _Transition;
    float  _TriplanarContrast;
    float  _Radius;
    float  _StochasticScale;
    float  _HighQualityEnabled;
    // Never set, so always 0. The compiler cannot prove that, so a term scaled by it keeps a value from being hoisted.
    float  _CBLOpaqueZero;
CBUFFER_END

#endif // KSP2_CELESTIALBODY_LOCAL_INPUT_INCLUDED