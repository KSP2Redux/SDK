using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ksp2UnityTools.Editor.PlanetAuthoring
{
    /// <summary>
    /// Gives every SceneView a black background, like space, while a planet preview session is active, and resets
    /// clip planes plus skybox to defaults on session end.
    /// </summary>
    /// <remarks>
    /// The SceneView fills its camera's target with the Scene Background preference before the camera renders, and
    /// the deferred path draws over that rather than clearing it, so the camera's own clear color never shows. A
    /// command buffer clears the target to black before the G-buffer pass instead.
    ///
    /// No other prior state is preserved. Session end always lands the SceneView in a known-good default (skybox on,
    /// dynamicClip on) so the user never gets stuck with our tight body-bracketed clip planes after preview.
    /// </remarks>
    [InitializeOnLoad]
    internal static class SessionSceneViewSkyboxOverride
    {
        private const string BLACK_CLEAR_BUFFER_NAME = "Redux preview black clear";
        private const CameraEvent BLACK_CLEAR_EVENT = CameraEvent.BeforeGBuffer;

        private static CommandBuffer _blackClear;

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
            if (sv == null)
                return;

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

        // The skybox goes off so it does not draw over the black.
        private static void EnsureOverride(SceneView sv)
        {
            EnsureBlackClear(sv.camera);
            if (!sv.sceneViewState.showSkybox)
                return;

            sv.sceneViewState.showSkybox = false;
            sv.Repaint();
        }

        private static void RestoreDefaults(SceneView sv)
        {
            sv.sceneViewState.showSkybox = true;
            // dynamicClip = true makes SceneView recompute near/far per paint, so no explicit
            // clip-plane reset is needed. The values that SceneViewFraming wrote get overwritten
            // on the next paint.
            sv.cameraSettings.dynamicClip = true;
            RemoveBlackClear(sv.camera);
            sv.Repaint();
        }

        // Found by name rather than tracked, so a camera that kept the buffer across a domain reload is not given a
        // second one.
        private static void EnsureBlackClear(Camera camera)
        {
            if (camera == null)
                return;

            foreach (CommandBuffer buffer in camera.GetCommandBuffers(BLACK_CLEAR_EVENT))
            {
                if (buffer.name == BLACK_CLEAR_BUFFER_NAME)
                    return;
            }

            if (_blackClear == null)
            {
                _blackClear = new CommandBuffer { name = BLACK_CLEAR_BUFFER_NAME };
                _blackClear.SetRenderTarget(BuiltinRenderTextureType.CameraTarget);
                _blackClear.ClearRenderTarget(false, true, Color.black);
            }

            camera.AddCommandBuffer(BLACK_CLEAR_EVENT, _blackClear);
        }

        private static void RemoveBlackClear(Camera camera)
        {
            if (camera == null)
                return;

            foreach (CommandBuffer buffer in camera.GetCommandBuffers(BLACK_CLEAR_EVENT))
            {
                if (buffer.name == BLACK_CLEAR_BUFFER_NAME)
                {
                    camera.RemoveCommandBuffer(BLACK_CLEAR_EVENT, buffer);
                }
            }
        }
    }
}
