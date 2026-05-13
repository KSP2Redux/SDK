using KSP.Rendering.Planets;
using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Tools
{
    /// <summary>
    /// Pumps the active <see cref="PlanetAuthoringSession" />'s PQS whenever the artist edits the
    /// PQS, its bound <see cref="PQSData" />, or the surface material, so the preview updates in
    /// realtime without a Disable / Enable cycle.
    /// </summary>
    /// <remarks>
    /// Subscribes to <see cref="Undo.postprocessModifications" />, the global "something just got
    /// modified through a SerializedProperty" hook. On any modification whose target is one of the
    /// three relevant assets, re-pumps the surface material binding and asks the sphere to
    /// resubdivide on the next tick.
    ///
    /// UpdateSurfaceMaterial re-pushes shader property bindings so PQSData edits reach the GPU but
    /// doesn't re-sample. UpdateSphere resubdivides so quads re-sample but doesn't on its own re-bind
    /// changed properties. Both are needed to cover the typical material / heightmap-parameter tweak
    /// without a full Disable / Enable cycle.
    ///
    /// Changes that need a heavier refresh (re-binding the heightmap NativeArrays inside
    /// PQSRenderer, swapping a layer texture asset reference) still require a Disable / Enable
    /// cycle - <see cref="PQS.UpdateSurfaceMaterial" /> only re-pushes shader properties, it
    /// doesn't rebuild the native heightmap caches the renderer holds.
    /// </remarks>
    [InitializeOnLoad]
    internal static class PqsLiveEditWatcher
    {
        static PqsLiveEditWatcher()
        {
            Undo.postprocessModifications -= OnModifications;
            Undo.postprocessModifications += OnModifications;
        }

        private static UndoPropertyModification[] OnModifications(UndoPropertyModification[] modifications)
        {
            PlanetAuthoringSession session = PlanetAuthoringSession.Active;
            if (session?.Pqs == null)
                return modifications;

            try
            {
                PQS pqs = session.Pqs;
                PQSData data = pqs.data;
                Material surfaceMaterial = data != null && data.materialSettings != null
                    ? data.materialSettings.surfaceMaterial
                    : null;

                for (int i = 0; i < modifications.Length; i++)
                {
                    Object target = modifications[i].currentValue?.target;
                    if (target == null)
                        continue;
                    if (target == pqs || target == data || (surfaceMaterial != null && target == surfaceMaterial))
                    {
                        // Defer to the next editor tick so the SerializedProperty write completes
                        // before the renderer re-reads. Coalesces multiple modifications in the same
                        // frame into one refresh.
                        EditorApplication.delayCall -= RefreshPreview;
                        EditorApplication.delayCall += RefreshPreview;
                        break;
                    }
                }
            }
            catch (System.Exception ex)
            {
                // Never let a watcher exception break Unity's Undo subsystem.
                Debug.LogWarning($"[PqsLiveEditWatcher] OnModifications threw: {ex.GetType().Name} {ex.Message}");
            }
            return modifications;
        }

        private static void RefreshPreview()
        {
            PlanetAuthoringSession session = PlanetAuthoringSession.Active;
            if (session?.Pqs == null)
                return;
            try
            {
                session.Pqs.UpdateSurfaceMaterial();
                session.Pqs.UpdateSphere();
                SceneView.RepaintAll();
            }
            catch (System.Exception ex)
            {
                // Don't spam the console - one log line per failed refresh.
                Debug.LogWarning($"[PqsLiveEditWatcher] Refresh failed: {ex.GetType().Name} {ex.Message}");
            }
        }
    }
}
