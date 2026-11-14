# KSP2 Redux SDK
Unity package for developing mods for KSP2 Redux.

## Installation
Install by adding a package into Unity from Git (https://github.com/KSP2Redux/SDK.git)

## Linked Addressables

Open `Modding > Linked Addressables > Open Browser...` to create a
`.bkaddressable` asset from the Addressables catalog imported by ThunderKit.
The imported asset has a persistent Unity editor identity, while player content
keeps references to the original external CAB and path ID instead of copying the
source payload into the mod bundle.

The shared editor pipeline uses an editor-only build of AssetTools.NET 3.0.5 and
Unity's Scriptable Build Pipeline. The AssetTools.NET license is included under
`ThirdParty`; the Scriptable Build Pipeline dependency is declared by this
package.

## Prefab patches

Visual prefab-patch authoring uses the `PatchManager.PrefabPatching` assembly
imported from KSP2 Redux by ThunderKit, just like the other game assemblies. The
editor creates variants from linked prefabs and compiles them to Patch Manager's
public declarative manifest format; Patch Manager is not a separate Unity package.

## Procedural flare shaders

Custom flare styles can include
`Packages/ksp2redux.sdk/Assets/Shaders/URP/Include/FlareCommon.hlsl` and implement
`GetFlareElement(uint index)` plus `EvaluateFlareElement(uint index, float2 coordinate)`.
The game supplies source tint, scale, visibility and depth coverage. A shader's
named `Flare` pass draws bounded quads, with its `FlareElements` SubShader tag
setting their count. Declare `_FlareLayer_Name` Vector properties to expose
`Flares:Profile("MyMod.Style"):Layer("Name")` in Lua. Shader properties supply
defaults, and `SetFloat` / `SetVector` provide additional parameter overrides.

Use `FlareLayer(float4(width, height, brightness, rotationDegrees), flags)` to
create an element centered on the source. Width and height are fractions of
image height before source scaling. Change its `center` for offset ghosts
(bottom-origin viewport XY), or its `tint` for a linear RGB multiplier. The
evaluation function returns linear RGB at local coordinates from -1 to 1.
The wrapper uses `FlareVertex` / `FlareFragment`, `Blend One One`, `ZWrite Off`
and `ZTest Always`. Source intensity retains the game's 3x normalization.

Flags combine with `|`: `FLARE_VISIBILITY` fades optical spread with source
visibility, `FLARE_DEPTH` clips luminous geometry at the output pixel, and
`FLARE_ARTIFACT` responds to the optical-artifact debug toggle.
An optional `FlareDepthLayer` tag names a `_FlareLayer_Name` vector enclosing
all clipped elements in source-centered rotated bounds. This enables cropped
temporal depth coverage for directional sources in flight. Without it, clipping
uses current depth. Map view skips temporal coverage.
An optional `FlareResources` tag binds textures once from Resources, for example
`"_Curve=Redux/FlareCurves/Angular"`. This addresses Resources included in the
game's player build. Loading a mod AssetBundle does not add its textures to
that Resources catalog. Declare the texture properties in ShaderLab. Custom
textures distributed in mod bundles require separate asset-loading integration,
which the current float/vector profile API does not provide.

Select `ShaderName` before editing a profile. Changing it clears shader
overrides and invalidates existing layer references. `Reset()` keeps the
selected shader and restores its defaults. Parameter names are case-sensitive,
must be declared in ShaderLab, and values must be finite. Named layer width,
height and brightness must also be nonnegative when assigned through SetVector.
Other vectors accept signed components. Float ranges clamp
to their limits. The renderer calls only the pass named `Flare`, with one
through 64 instances from `FlareElements` (one when omitted).

## Snapshot releases

SDK snapshots use a game-versioned SemVer tag and are published to the public Unity registry. See [.github/RELEASING.md](.github/RELEASING.md) for the release process.
