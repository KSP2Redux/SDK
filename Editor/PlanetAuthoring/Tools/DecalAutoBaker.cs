using System.Collections.Generic;
using Ksp2UnityTools.Editor.PlanetAuthoring.Authoring;
using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Tools
{
    /// <summary>
    /// Watches PQSDecal asset modifications and re-bakes the active body's PQSDecalData so changes
    /// reflect in the live preview without a manual Bake click.
    /// </summary>
    /// <remarks>
    /// Only fires when a planet preview session is active and the modified decal is referenced by
    /// at least one instance on the body's controller. No-op otherwise. Bake requests route
    /// through <see cref="DecalBaker.QueueRebuild" /> so same-tick triggers (a template change
    /// plus an instance edit) coalesce into one bake.
    /// </remarks>
    internal class DecalAutoBaker : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            var session = PlanetAuthoringSession.Active;
            if (session == null || session.Pqs == null)
                return;
            var controller = DecalControllerHelper.Resolve(session.Pqs, false);
            if (controller == null || controller.PqsDecalData == null)
                return;

            var changed = new HashSet<string>(importedAssets, System.StringComparer.OrdinalIgnoreCase);
            changed.UnionWith(deletedAssets);
            changed.UnionWith(movedAssets);
            changed.UnionWith(movedFromAssetPaths);
            bool needsBake = HasChanged(controller.SharedHeightmap, changed) || HasChanged(controller.SharedAlphaMap, changed);
            foreach (PQSDecal template in DecalBaker.CollectTemplates(controller))
            {
                var authoring = AuthoringSidecars.Find(template);
                if (HasChanged(template, changed) || HasChanged(authoring, changed))
                {
                    needsBake = true;
                    break;
                }
                if (authoring == null)
                    continue;
                if (HasChanged(authoring.HeightMap, changed) || HasChanged(authoring.AlphaMap, changed)
                    || HasChanged(authoring.Diffuse, changed) || HasChanged(authoring.Normal, changed)
                    || HasChanged(authoring.AlphaMaskTexture, changed) || HasChanged(authoring.Peak, changed) || HasChanged(authoring.Slope, changed))
                {
                    needsBake = true;
                    break;
                }
            }
            if (needsBake)
            {
                DecalBaker.QueueRebuild(controller);
            }
        }

        private static bool HasChanged(Object source, HashSet<string> changed)
        {
            if (source == null)
                return false;
            string path = AssetDatabase.GetAssetPath(source);
            if (string.IsNullOrEmpty(path))
                return false;
            // Include source blobs declared by custom importers, without opening any pixel buffers.
            foreach (string dependency in AssetDatabase.GetDependencies(path, true))
            {
                if (changed.Contains(dependency))
                    return true;
            }
            return false;
        }
    }
}
