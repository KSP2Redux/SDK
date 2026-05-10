using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Windows
{
    /// <summary>
    /// Editor window that surfaces the active planet preview session and its terrain sample readouts.
    /// </summary>
    public class PreviewControlsWindow : EditorWindow
    {
        private Label _statusLabel;
        private Label _bodyLabel;
        private Label _altitudeLabel;
        private Button _previewButton;

        /// <summary>
        /// Opens the Planet Preview controls window.
        /// </summary>
        [MenuItem(PlanetAuthoringWindows.MenuRoot + "Preview Controls", priority = PlanetAuthoringWindows.PriorityPreviewControls)]
        public static void ShowWindow()
        {
            var window = GetWindow<PreviewControlsWindow>();
            window.titleContent = new GUIContent("Planet Preview");
            window.minSize = new Vector2(280f, 160f);
        }

        private void CreateGUI()
        {
            var root = rootVisualElement;
            root.style.paddingLeft = 8f;
            root.style.paddingRight = 8f;
            root.style.paddingTop = 8f;
            root.style.paddingBottom = 8f;

            root.Add(new Label("Redux Planet Authoring") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 4f } });

            _statusLabel = new Label { style = { marginBottom = 6f, whiteSpace = WhiteSpace.Normal } };
            root.Add(_statusLabel);

            _bodyLabel = new Label { style = { marginBottom = 2f } };
            root.Add(_bodyLabel);

            _altitudeLabel = new Label { style = { marginBottom = 6f, unityFontStyleAndWeight = FontStyle.Bold } };
            root.Add(_altitudeLabel);

            _previewButton = new Button(OnPreviewButtonClicked);
            root.Add(_previewButton);

            PlanetPreviewState.ActiveChanged += OnPreviewStateChanged;
            RefreshUI();
        }

        private void OnDisable()
        {
            PlanetPreviewState.ActiveChanged -= OnPreviewStateChanged;
        }

        private void OnPreviewStateChanged()
        {
            RefreshUI();
        }

        private void RefreshUI()
        {
            if (_statusLabel == null) return;

            var session = PlanetAuthoringSession.Active;
            bool active = session != null && session.IsAlive;

            _previewButton.text = active ? "Stop Preview" : "(no active preview)";
            _previewButton.SetEnabled(active);

            if (!active)
            {
                _statusLabel.text = "No active preview. Select a celestial body and click 'Enable preview' on its inspector to start.";
                _bodyLabel.text = string.Empty;
                _altitudeLabel.text = string.Empty;
                return;
            }

            _statusLabel.text = $"Preview active. Class: {session.Class}";
            _bodyLabel.text = session.Body != null
                ? $"Body: {session.Body.name}"
                : "Body: (destroyed)";

            var state = PlanetPreviewState.Active;
            if (state != null && state.HasTerrainSample)
            {
                _altitudeLabel.text =
                    $"Distance to terrain: {FormatMeters(state.CameraDistanceFromSurface)}\n" +
                    $"Terrain elevation here: {FormatMeters(state.TerrainElevationAtCamera)}";
            }
            else if (state != null)
            {
                _altitudeLabel.text = "Aim the SceneView camera at the body to sample terrain.";
            }
            else
            {
                _altitudeLabel.text = string.Empty;
            }
        }

        private void OnPreviewButtonClicked()
        {
            var session = PlanetAuthoringSession.Active;
            if (session != null)
                session.End();
            RefreshUI();
        }

        private static string FormatMeters(float m)
        {
            return Mathf.Abs(m) >= 1000f ? $"{m / 1000f:0.##} km" : $"{m:0.#} m";
        }
    }
}
