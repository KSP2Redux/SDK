using Redux.Rendering;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Ksp2UnityTools.Editor.PlanetAuthoring
{
    /// <summary>
    /// Gives every SceneView a black background, like space, while a planet preview session is active, and resets
    /// clip planes plus skybox to defaults on session end.
    /// </summary>
    /// <remarks>
    /// The SceneView clears its camera to the Scene Background preference, so the camera's own clear color never
    /// shows. A render hook clears the SceneView camera color to black before the opaques instead.
    ///
    /// No other prior state is preserved. Session end always lands the SceneView in a known-good default (skybox on,
    /// dynamicClip on) so the user never gets stuck with our tight body-bracketed clip planes after preview.
    /// </remarks>
    [InitializeOnLoad]
    internal static class SessionSceneViewSkyboxOverride
    {
        private static readonly BlackClearHook _blackClear = new();

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
            EnsureBlackClear();
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
            RemoveBlackClear();
            sv.Repaint();
        }

        // Registered on every paint rather than once, because entering play mode clears the hook registry.
        // Registering again is a no-op.
        private static void EnsureBlackClear() => CameraRenderHooks.Register(_blackClear);

        private static void RemoveBlackClear() => CameraRenderHooks.Unregister(_blackClear);

        // Clears each SceneView camera's color to black before its opaques while a session is active.
        private sealed class BlackClearHook : CommandCameraRenderHook
        {
            public BlackClearHook() : base("Redux preview black clear")
            {
            }

            public override RenderPassEvent Event => RenderPassEvent.BeforeRenderingOpaques;

            public override bool ShouldRender(Camera camera) =>
                camera.cameraType == CameraType.SceneView && PlanetAuthoringSession.Active != null;

            protected override void Execute(CommandBuffer cmd, in CameraRenderTargets targets) =>
                cmd.ClearRenderTarget(false, true, Color.black);
        }
    }
}
