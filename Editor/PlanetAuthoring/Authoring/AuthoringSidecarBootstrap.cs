using KSP.Game.Science;
using KSP.Rendering.Planets;
using Ksp2UnityTools.Editor.PlanetAuthoring.Science;
using Ksp2UnityTools.Editor.PlanetAuthoring.Tools;
using Ksp2UnityTools.Editor.ScriptableObjects;
using UnityEditor;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Authoring
{
    /// <summary>
    /// Auto-creates authoring sidecar entries when assets that need them are imported, removes
    /// them when the source assets are deleted, and invalidates the science-region locator cache
    /// so the user never has to think about "did the sidecar get made?".
    /// </summary>
    /// <remarks>
    /// Single hook: an <see cref="AssetPostprocessor" /> that creates the matching sidecar entry
    /// when a PQSDecal, PQSData, or ScienceRegionData asset lands, and prunes orphans on delete.
    /// GetOrCreate is idempotent so re-imports don't duplicate entries. A single batched
    /// SaveAssets at the end of the pass avoids per-entry save-thrash during bulk imports.
    /// </remarks>
    internal class AuthoringSidecarBootstrap : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            // Any asset movement invalidates the science-region body-name lookup and the parent-mod
            // and project-group caches on the addressables helper. Conservatively clear on every
            // postprocess pass. Everything repopulates lazily on the next call.
            if (importedAssets.Length > 0 || deletedAssets.Length > 0 || movedAssets.Length > 0)
            {
                ScienceRegionAssetLocator.InvalidateCache();
                PlanetAuthoringAddressables.InvalidateCaches();
                AddressableKeyLookup.InvalidateCaches();
            }

            var dirty = false;
            foreach (var path in importedAssets)
            {
                if (!path.EndsWith(".asset")) continue;
                var decal = AssetDatabase.LoadAssetAtPath<PQSDecal>(path);
                if (decal != null && !string.IsNullOrEmpty(decal.DecalID))
                {
                    PlanetAuthoringRegistry.Instance.GetOrCreateDecalTemplate(decal.DecalID);
                    dirty = true;
                    continue;
                }
                var pqsData = AssetDatabase.LoadAssetAtPath<PQSData>(path);
                if (pqsData != null)
                {
                    PlanetAuthoringRegistry.Instance.GetOrCreatePQSData(pqsData);
                    dirty = true;
                    continue;
                }
                var scienceRegion = AssetDatabase.LoadAssetAtPath<ScienceRegionData>(path);
                if (scienceRegion != null)
                {
                    PlanetAuthoringRegistry.Instance.GetOrCreateScienceRegion(scienceRegion);
                    dirty = true;
                }
            }

            // Deleted GUIDs no longer resolve to a path, so the registry walks its entries and
            // removes any whose stored GUID is now orphaned. DecalTemplateAuthoring is keyed by
            // DecalID (not GUID) and is intentionally left for explicit cleanup.
            if (deletedAssets.Length > 0)
            {
                PlanetAuthoringRegistry.Instance.RemoveOrphanedSidecars();
                dirty = true;
            }

            if (dirty)
            {
                AssetDatabase.SaveAssets();
            }
        }
    }
}
