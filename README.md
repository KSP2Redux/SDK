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
