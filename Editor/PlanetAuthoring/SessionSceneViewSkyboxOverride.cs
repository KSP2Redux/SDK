using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring
{
    /// <summary>
    /// Switches every open SceneView to a black skybox while a planet preview session is active and
    /// resets clip planes plus skybox to defaults on session end.
    /// </summary>
    /// <remarks>
    /// No prior state is preserved. Session end always lands the SceneView in a known-good default
    /// (skybox on, dynamicClip on) so the user never gets stuck with our tight body-bracketed
    /// clip planes after preview.
    /// </remarks>
    [InitializeOnLoad]
    internal static class SessionSceneViewSkyboxOverride
    {
        // Per-SceneView capture of the original background color so reset restores what the artist
        // actually had configured rather than a global default.
        private static readonly Dictionary<EntityId, Color> OriginalBgColors = new();

        static SessionSceneViewSkyboxOverride()
        {
            PlanetPreviewState.ActiveChanged += OnSessionChanged;
            // SceneViews opened mid-session miss the ActiveChanged event entirely, so hook the
            // per-paint callback so they get the override on their first paint.
            SceneView.duringSceneGui += OnSceneGui;
        }

        private static void OnSessionChanged()
        {
            if (PlanetAuthoringSession.Active != null)
                ApplyToAllSceneViews();
            else
                ResetAllSceneViews();
        }

        private static void OnSceneGui(SceneView sv)
        {
            if (sv == null) return;
            if (PlanetAuthoringSession.Active != null)
                EnsureOverride(sv);
        }

        private static void ApplyToAllSceneViews()
        {
            foreach (var obj in SceneView.sceneViews)
            {
                if (obj is SceneView sv && sv != null)
                {
                    EnsureOverride(sv);
                }
            }
        }

        private static void ResetAllSceneViews()
        {
            foreach (var obj in SceneView.sceneViews)
            {
                if (obj is SceneView sv && sv != null)
                {
                    RestoreDefaults(sv);
                }
            }
        }

        private static void EnsureOverride(SceneView sv)
        {
            var id = sv.GetEntityId();
            // Only capture the artist's original color the first time we touch this SceneView so
            // re-applying (e.g. mid-session refresh) doesn't store our own override color.
            if (!OriginalBgColors.ContainsKey(id))
                OriginalBgColors[id] = sv.camera.backgroundColor;
            if (!sv.sceneViewState.showSkybox && sv.camera.backgroundColor == Color.black) return;
            sv.sceneViewState.showSkybox = false;
            sv.camera.backgroundColor = Color.black;
            sv.Repaint();
        }

        private static void RestoreDefaults(SceneView sv)
        {
            sv.sceneViewState.showSkybox = true;
            // dynamicClip = true makes SceneView recompute near/far per paint, so no explicit
            // clip-plane reset is needed. The values that SceneViewFraming wrote get overwritten
            // on the next paint.
            sv.cameraSettings.dynamicClip = true;
            var id = sv.GetEntityId();
            if (OriginalBgColors.TryGetValue(id, out var bg))
            {
                sv.camera.backgroundColor = bg;
                OriginalBgColors.Remove(id);
            }
            sv.Repaint();
        }
    }
}
