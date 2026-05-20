using KSP.Rendering.Planets;
using UnityEditor;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Tools
{
    /// <summary>
    /// Auto-bakes the active body's PQSDecalData when any of its scene-instance
    /// <see cref="PQSDecalInstance" /> components is modified or removed.
    /// </summary>
    /// <remarks>
    /// Complements <see cref="DecalAutoBaker" />, which handles PQSDecal template asset changes.
    /// Property-level edits arrive via <see cref="Undo.postprocessModifications" />. Instance
    /// deletes are caught via <see cref="EditorApplication.hierarchyChanged" /> by comparing the
    /// controller's serialized list count against live child components. Bake requests route
    /// through <see cref="DecalBaker.QueueRebuild" /> so same-tick triggers coalesce.
    /// The hierarchy-change count-tracking is known to false-positive on scene loads and prefab
    /// unpacks where the live-instance count shifts without an actual artist-driven add or remove.
    /// </remarks>
    [InitializeOnLoad]
    internal static class DecalInstanceAutoBaker
    {
        private static int _lastLiveInstanceCount = -1;

        static DecalInstanceAutoBaker()
        {
            Undo.postprocessModifications += OnPostprocess;
            EditorApplication.hierarchyChanged += OnHierarchyChanged;
        }

        private static UndoPropertyModification[] OnPostprocess(UndoPropertyModification[] modifications)
        {
            var session = PlanetAuthoringSession.Active;
            if (session?.Pqs == null) return modifications;
            var controller = DecalControllerHelper.Resolve(session.Pqs);
            if (controller?.PqsDecalInstanceList == null) return modifications;

            foreach (var mod in modifications)
            {
                var obj = mod.currentValue?.target;
                if (obj is PQSDecalInstance instance && controller.PqsDecalInstanceList.Contains(instance))
                {
                    DecalBaker.QueueRebuild(controller);
                    break;
                }
            }
            return modifications;
        }

        private static void OnHierarchyChanged()
        {
            var session = PlanetAuthoringSession.Active;
            if (session?.Pqs == null) return;
            var controller = DecalControllerHelper.Resolve(session.Pqs);
            if (controller == null) return;

            // Live count excludes destroyed GameObjects. The serialized list keeps stale Unity-null
            // entries until something prunes them. A drift indicates an instance was added or removed.
            var live = controller.GetComponentsInChildren<PQSDecalInstance>(includeInactive: true);
            var liveCount = live != null ? live.Length : 0;

            if (_lastLiveInstanceCount < 0)
            {
                _lastLiveInstanceCount = liveCount;
                return;
            }
            if (liveCount == _lastLiveInstanceCount) return;
            _lastLiveInstanceCount = liveCount;

            // Prune dead references before baking so the controller's list reflects reality.
            controller.PqsDecalInstanceList?.RemoveAll(inst => inst == null);
            DecalBaker.QueueRebuild(controller);
        }
    }
}
