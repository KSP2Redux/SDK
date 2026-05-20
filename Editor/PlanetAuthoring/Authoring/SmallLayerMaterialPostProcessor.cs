using System.Collections.Generic;
using Ksp2UnityTools.Editor.PlanetAuthoring.Inspectors;
using KSP.Rendering.Planets;
using UnityEditor;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Authoring
{
    /// <summary>
    /// Recompiles every body whose <see cref="PQSDataAuthoring" /> references a <see cref="SmallLayerMaterial" /> asset when that asset is saved.
    /// </summary>
    /// <remarks>
    /// Catches edits that happen outside any one body's inspector, where the SO is opened on its own, edited, and saved. Walks <see cref="AuthoringSidecars.AllPQSDataAuthorings" /> to find every PQSData sidecar that references the changed SO, then runs <see cref="SmallLayerMaterialCompiler.Compile" /> and <see cref="Texture2DArrayPacker.RepackSmallTiles" /> against each one's surface material.
    /// </remarks>
    internal class SmallLayerMaterialPostProcessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            var changedMaterials = CollectChangedSmallLayerMaterials(importedAssets);
            if (changedMaterials.Count == 0) return;

            // Drop cached preview thumbnails so the next paint of the inspector preview pane,
            // project window thumbnail, or matrix cell regenerates against the new SO state.
            foreach (var changed in changedMaterials)
                SmallLayerMaterialPreview.Invalidate(changed);

            foreach (var authoring in AuthoringSidecars.AllPQSDataAuthorings())
            {
                if (authoring?.smallLayerSlots == null) continue;
                if (!ReferencesAny(authoring, changedMaterials)) continue;

                var pqsData = ResolvePQSData(authoring);
                var material = pqsData?.materialSettings?.surfaceMaterial;
                if (pqsData == null || material == null) continue;

                SmallLayerMaterialCompiler.Compile(authoring, material);
                Texture2DArrayPacker.RepackSmallTiles(pqsData, material);
            }
        }

        private static HashSet<SmallLayerMaterial> CollectChangedSmallLayerMaterials(string[] importedAssets)
        {
            var set = new HashSet<SmallLayerMaterial>();
            foreach (var path in importedAssets)
            {
                var asset = AssetDatabase.LoadAssetAtPath<SmallLayerMaterial>(path);
                if (asset != null) set.Add(asset);
            }
            return set;
        }

        private static bool ReferencesAny(PQSDataAuthoring authoring, HashSet<SmallLayerMaterial> changed)
        {
            foreach (var slot in authoring.smallLayerSlots)
            {
                if (slot?.Material != null && changed.Contains(slot.Material))
                    return true;
            }
            return false;
        }

        private static PQSData ResolvePQSData(PQSDataAuthoring authoring)
        {
            return AuthoringSidecars.GetRuntimeAsset<PQSData>(authoring);
        }
    }
}
