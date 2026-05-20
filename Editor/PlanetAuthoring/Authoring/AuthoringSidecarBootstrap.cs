using KSP.Game.Science;
using KSP.Rendering.Planets;
using Ksp2UnityTools.Editor.PlanetAuthoring.Science;
using Ksp2UnityTools.Editor.PlanetAuthoring.Tools;
using Ksp2UnityTools.Editor.ScriptableObjects;
using UnityEditor;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Authoring
{
    /// <summary>
    /// Auto-creates authoring sidecars when assets that need them are imported and invalidates the science-region locator cache so artists never have to think about whether the sidecar got made.
    /// </summary>
    /// <remarks>
    /// Imports route through <see cref="AuthoringSidecars.GetOrCreate(PQSData)" /> (and its sibling overloads), which is idempotent so re-imports don't duplicate sidecars. Deletes leave the sidecar in the body's <c>Data/</c> folder for explicit cleanup, since there is no registry index to keep in sync. A single batched SaveAssets at the end of the pass avoids per-entry save-thrash during bulk imports.
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
                    AuthoringSidecars.GetOrCreate(decal);
                    dirty = true;
                    continue;
                }
                var pqsData = AssetDatabase.LoadAssetAtPath<PQSData>(path);
                if (pqsData != null)
                {
                    AuthoringSidecars.GetOrCreate(pqsData);
                    dirty = true;
                    continue;
                }
                var scienceRegion = AssetDatabase.LoadAssetAtPath<ScienceRegionData>(path);
                if (scienceRegion != null)
                {
                    AuthoringSidecars.GetOrCreate(scienceRegion);
                    dirty = true;
                }
            }

            // Sidecars are standalone .asset files now, so an orphan cleanup pass on delete is no
            // longer needed - the artist deletes the runtime asset, the sidecar in Data/ stays
            // until they delete it directly. (No registry index to keep in sync.)

            if (dirty)
            {
                AssetDatabase.SaveAssets();
            }
        }
    }
}
