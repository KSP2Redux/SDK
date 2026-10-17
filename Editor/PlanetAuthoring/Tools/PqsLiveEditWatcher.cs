using KSP.Rendering.Planets;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

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
                PQSData data = PlanetAuthoringSession.AuthoredData(pqs);
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
                        QueueRefresh();
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

        /// <summary>
        /// Queues a refresh of the active preview on the next editor tick.
        /// </summary>
        /// <remarks>
        /// For edits that do not pass through <see cref="Undo.postprocessModifications" />, such as a direct
        /// <c>Material.SetFloat</c>. Repeated calls in one tick coalesce into one refresh.
        /// </remarks>
        internal static void QueueRefresh()
        {
            EditorApplication.delayCall -= RefreshPreview;
            EditorApplication.delayCall += RefreshPreview;
        }

        private static void RefreshPreview()
        {
            PlanetAuthoringSession session = PlanetAuthoringSession.Active;
            if (session?.Pqs == null)
                return;
            try
            {
                SyncPreviewCopy(session.Pqs.data, PlanetAuthoringSession.AuthoredData(session.Pqs));
                session.Pqs.UpdateSurfaceMaterial();
                // Re-push per-tier height-scale uniforms so live edits update the sphere displacement.
                session.Pqs.PQSRenderer?.RefreshHeightScalesForEditor();
                session.Pqs.UpdateSphere();
                SceneView.RepaintAll();
            }
            catch (System.Exception ex)
            {
                // Don't spam the console - one log line per failed refresh.
                Debug.LogWarning($"[PqsLiveEditWatcher] Refresh failed: {ex.GetType().Name} {ex.Message}");
            }
        }

        // The preview renders a copy of the PQSData and its surface material when the material was authored on a
        // stand-in shader. Edits land on the assets, so they are carried onto the copy before the renderer re-reads.
        internal static void SyncPreviewCopy(PQSData preview, PQSData authored)
        {
            if (authored == null || preview == null || preview == authored)
                return;

            Material previewMaterial = preview.materialSettings.surfaceMaterial;
            EditorUtility.CopySerialized(authored, preview);
            preview.materialSettings.surfaceMaterial = previewMaterial;
            CopyShaderProperties(authored.materialSettings.surfaceMaterial, previewMaterial);
        }

        // Copies only what the authored material's shader declares. The renderer sets undeclared properties and
        // buffers on the copy at boot, and copying the whole material would wipe them.
        private static void CopyShaderProperties(Material source, Material target)
        {
            if (source == null || target == null || source.shader == null)
                return;

            Shader shader = source.shader;
            int count = shader.GetPropertyCount();
            for (int i = 0; i < count; i++)
            {
                int id = shader.GetPropertyNameId(i);
                if (!target.HasProperty(id))
                    continue;

                switch (shader.GetPropertyType(i))
                {
                    case ShaderPropertyType.Color:
                        target.SetColor(id, source.GetColor(id));
                        break;
                    case ShaderPropertyType.Vector:
                        target.SetVector(id, source.GetVector(id));
                        break;
                    case ShaderPropertyType.Float or ShaderPropertyType.Range:
                        target.SetFloat(id, source.GetFloat(id));
                        break;
                    case ShaderPropertyType.Int:
                        target.SetInteger(id, source.GetInteger(id));
                        break;
                    case ShaderPropertyType.Texture:
                        target.SetTexture(id, source.GetTexture(id));
                        target.SetTextureScale(id, source.GetTextureScale(id));
                        target.SetTextureOffset(id, source.GetTextureOffset(id));
                        break;
                }
            }

            foreach (LocalKeyword keyword in shader.keywordSpace.keywords)
            {
                var targetKeyword = new LocalKeyword(target.shader, keyword.name);
                if (targetKeyword.isValid)
                    target.SetKeyword(targetKeyword, source.IsKeywordEnabled(keyword));
            }
        }
    }
}
