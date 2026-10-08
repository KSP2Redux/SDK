using KSP.Rendering.Planets;
using UniLinq;
using UnityEditor;

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
            if (session?.Pqs == null) return;
            var controller = DecalControllerHelper.Resolve(session.Pqs);
            if (controller == null) return;

            var needsBake = false;
            foreach (var path in importedAssets)
            {
                if (!path.EndsWith(".asset")) continue;
                var decal = AssetDatabase.LoadAssetAtPath<PQSDecal>(path);
                if (decal == null) continue;
                if (controller.PqsDecalInstanceList != null && controller.PqsDecalInstanceList.Any(inst => inst != null && inst.PQSDecal == decal))
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
    }
}
