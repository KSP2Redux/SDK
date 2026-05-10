using KSP.Rendering.Planets;
using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring
{
    /// <summary>
    /// Binds the active SceneView camera to a PQS target provider so subdivision follows scene-view navigation.
    /// </summary>
    public sealed class PreviewCameraDriver
    {
        private readonly PQS _pqs;
        private SceneView _boundSceneView;
        private Vector3 _lastCameraPosition;
        private Quaternion _lastCameraRotation;
        private bool _previousCameraEnabled;

        /// <summary>
        /// Creates a driver for the given PQS instance.
        /// </summary>
        /// <param name="pqs">The PQS component to drive.</param>
        public PreviewCameraDriver(PQS pqs)
        {
            _pqs = pqs;
        }

        /// <summary>
        /// Gets the SceneView currently bound as the subdivision target.
        /// </summary>
        public SceneView BoundSceneView => _boundSceneView;

        /// <summary>
        /// Gets the camera of the bound SceneView, or null if no SceneView is bound or its camera is unavailable.
        /// </summary>
        public Camera BoundCamera => _boundSceneView != null ? _boundSceneView.camera : null;

        /// <summary>
        /// Gets a value indicating whether the bound camera moved or rotated since the last
        /// <see cref="Sync" /> call.
        /// </summary>
        public bool CameraMoved { get; private set; }

        /// <summary>
        /// Binds a SceneView as the active subdivision target.
        /// </summary>
        /// <remarks>
        /// Replaces any previously bound SceneView.
        /// </remarks>
        /// <param name="sceneView">The SceneView to bind. Pass null to detach (equivalent to <see cref="Unbind" />).</param>
        public void Bind(SceneView sceneView)
        {
            if (sceneView == null || sceneView.camera == null)
            {
                Unbind();
                return;
            }

            // Idempotent rebind to the same SceneView. Sync handles per-frame work.
            if (_boundSceneView == sceneView)
                return;

            // Switching SceneViews: restore the previous one before capturing the new one's
            // camera.enabled state. Without the Unbind, _previousCameraEnabled would be
            // overwritten with the value we set ourselves, leaking enabled=true on End.
            if (_boundSceneView != null)
                Unbind();

            _boundSceneView = sceneView;
            _pqs.SetTarget(new PQSTargetProvider(_pqs.transform, sceneView.camera));
            if (_pqs.PQSRenderer != null)
                _pqs.PQSRenderer.SourceCamera = sceneView.camera;

            // SceneView cameras default to enabled=false. DrawPQSQuads gates on Camera.isActiveAndEnabled.
            // Force-enable while bound and restore on Unbind.
            _previousCameraEnabled = sceneView.camera.enabled;
            sceneView.camera.enabled = true;

            Transform t = sceneView.camera.transform;
            _lastCameraPosition = t.position;
            _lastCameraRotation = t.rotation;
            CameraMoved = true;
        }

        /// <summary>
        /// Detaches from the current SceneView and clears the PQS target provider.
        /// </summary>
        public void Unbind()
        {
            if (_boundSceneView != null && _boundSceneView.camera != null)
                _boundSceneView.camera.enabled = _previousCameraEnabled;
            _boundSceneView = null;
            _pqs.SetTarget(null);
            if (_pqs.PQSRenderer != null)
                _pqs.PQSRenderer.SourceCamera = null;
            CameraMoved = false;
        }

        /// <summary>
        /// Updates camera-motion tracking and rebinds the target provider if the SceneView's camera was destroyed
        /// and recreated (e.g. after a layout change).
        /// </summary>
        public void Sync()
        {
            if (_boundSceneView == null)
            {
                CameraMoved = false;
                return;
            }

            Camera cam = _boundSceneView.camera;
            if (cam == null)
            {
                CameraMoved = false;
                return;
            }

            if (_pqs.PrimaryTargetProvider == null || _pqs.PrimaryTargetProvider.RenderCamera != cam)
            {
                _pqs.SetTarget(new PQSTargetProvider(_pqs.transform, cam));
            }
            if (_pqs.PQSRenderer != null && _pqs.PQSRenderer.SourceCamera != cam)
                _pqs.PQSRenderer.SourceCamera = cam;

            Transform t = cam.transform;
            CameraMoved = t.position != _lastCameraPosition || t.rotation != _lastCameraRotation;
            _lastCameraPosition = t.position;
            _lastCameraRotation = t.rotation;
        }
    }
}
