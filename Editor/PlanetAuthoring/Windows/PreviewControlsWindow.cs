using System;
using System.Globalization;
using KSP;
using KSP.Rendering.Planets;
using Ksp2UnityTools.Editor.PlanetAuthoring.Overlays;
using Ksp2UnityTools.Editor.PlanetAuthoring.Tools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Windows
{
    /// <summary>
    /// Editor window that surfaces the active planet preview session, surface-point picking and
    /// keying, and quick-jump altitude framing for the SceneView camera.
    /// </summary>
    public class PreviewControlsWindow : EditorWindow
    {
        private const string UxmlPath = "/Assets/Windows/PlanetAuthoring/Windows/PreviewControlsWindow.uxml";
        private const string UssPath = "/Assets/Windows/PlanetAuthoring/Windows/PreviewControlsWindow.uss";

        private Label _statusLabel;
        private Label _bodyLabel;
        private Label _altitudeLabel;
        private Button _previewButton;
        private FloatField _latField;
        private FloatField _lonField;
        private Button _pickButton;
        private Button _lookAtButton;
        private Button _copyButton;
        private Button _pasteButton;
        private VisualElement _jumpRow;
        private Slider _surfaceAltitudeSlider;
        private Button _jumpSurfaceButton;
        private Button _jumpTransitionButton;
        private Button _jumpLowOrbitButton;
        private Button _jumpHighOrbitButton;
        private Button _jumpFarButton;
        private ObjectField _sunLightField;
        private Slider _sunAzimuthSlider;
        private Slider _sunElevationSlider;
        private Slider _sunIntensitySlider;
        private Button _sunDayButton;
        private Button _sunNightButton;
        private bool _suppressSunUpdates;
        private Button _todPlayButton;
        private Button _todResetButton;
        private Slider _todSpeedSlider;
        private Toggle _overlayBiomeToggle;
        private Toggle _overlaySubzoneToggle;
        private Toggle _overlaySlopeToggle;
        private Toggle _overlayAltitudeToggle;
        private Toggle _overlayActiveLayerToggle;
        private VisualElement _overlayActiveLayerGrid;
        private VisualElement[,] _overlayActiveLayerCells;
        private Toggle _overlayScienceRegionToggle;
        private DropdownField _overlayScienceRegionMode;
        private Label _overlayScienceRegionStatus;
        private Slider _overlayStrengthSlider;
        private FloatField _overlayBandHeightField;
        private bool _suppressOverlayUpdates;
        private static readonly string[] ScienceRegionModeChoices = { "Baked palette", "Source texture" };

        // Mirrors the shader Property defaults so the grid swatches match what the overlay draws.
        private static readonly Color[] BiomeColors =
        {
            new(1.00f, 0.30f, 0.30f),
            new(0.30f, 1.00f, 0.30f),
            new(0.30f, 0.50f, 1.00f),
            new(1.00f, 0.95f, 0.30f),
        };
        private static readonly float[] LayerBrightness = { 0.35f, 0.55f, 0.80f, 1.00f };
        private static readonly string[] BiomeLabels = { "R", "G", "B", "A" };
        private bool _todPlaying;
        private double _todLastTime;
        private Quaternion _todBaseSunRotation;
        private bool _todBaseValid;

        /// <summary>
        /// Opens the Planet Preview controls window.
        /// </summary>
        [MenuItem(PlanetAuthoringWindows.MenuRoot + "Preview Controls", priority = PlanetAuthoringWindows.PriorityPreviewControls)]
        public static void ShowWindow()
        {
            var window = GetWindow<PreviewControlsWindow>();
            window.titleContent = new GUIContent("Planet Preview");
            window.minSize = new Vector2(280f, 220f);
        }

        /// <inheritdoc />
        private void CreateGUI()
        {
            var root = rootVisualElement;

            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(SDKConfiguration.BasePath + UxmlPath);
            if (tree == null)
            {
                root.Add(new Label("Failed to load PreviewControlsWindow.uxml"));
                return;
            }
            tree.CloneTree(root);

            Ksp2UnityToolsStyles.Apply(root, UssPath);

            _statusLabel = root.Q<Label>("status-label");
            _bodyLabel = root.Q<Label>("body-label");
            _altitudeLabel = root.Q<Label>("altitude-label");
            _previewButton = root.Q<Button>("preview-button");
            _latField = root.Q<FloatField>("lat-field");
            _lonField = root.Q<FloatField>("lon-field");
            _pickButton = root.Q<Button>("pick-button");
            _lookAtButton = root.Q<Button>("lookat-button");
            _copyButton = root.Q<Button>("copy-button");
            _pasteButton = root.Q<Button>("paste-button");
            _jumpRow = root.Q<VisualElement>("jump-row");
            _surfaceAltitudeSlider = root.Q<Slider>("surface-altitude-slider");
            _surfaceAltitudeSlider.lowValue = SurfaceFramingPrefs.MinMeters;
            _surfaceAltitudeSlider.highValue = SurfaceFramingPrefs.MaxMeters;
            _surfaceAltitudeSlider.SetValueWithoutNotify(SurfaceFramingPrefs.AltitudeMeters);
            _surfaceAltitudeSlider.RegisterValueChangedCallback(evt => SurfaceFramingPrefs.AltitudeMeters = evt.newValue);
            _jumpSurfaceButton = root.Q<Button>("jump-surface-button");
            _jumpTransitionButton = root.Q<Button>("jump-transition-button");
            _jumpLowOrbitButton = root.Q<Button>("jump-loworbit-button");
            _jumpHighOrbitButton = root.Q<Button>("jump-highorbit-button");
            _jumpFarButton = root.Q<Button>("jump-far-button");

            _previewButton.clicked += OnPreviewButtonClicked;
            _pickButton.clicked += OnPickButtonClicked;
            _lookAtButton.clicked += OnLookAtClicked;
            _copyButton.clicked += OnCopyClicked;
            _pasteButton.clicked += OnPasteClicked;
            _jumpSurfaceButton.clicked += () => JumpToAltitude(JumpAltitude.Surface);
            _jumpTransitionButton.clicked += () => JumpToAltitude(JumpAltitude.Transition);
            _jumpLowOrbitButton.clicked += () => JumpToAltitude(JumpAltitude.LowOrbit);
            _jumpHighOrbitButton.clicked += () => JumpToAltitude(JumpAltitude.HighOrbit);
            _jumpFarButton.clicked += () => JumpToAltitude(JumpAltitude.Far);

            _sunLightField = root.Q<ObjectField>("sun-light-field");
            _sunAzimuthSlider = root.Q<Slider>("sun-azimuth-slider");
            _sunElevationSlider = root.Q<Slider>("sun-elevation-slider");
            _sunIntensitySlider = root.Q<Slider>("sun-intensity-slider");
            _sunDayButton = root.Q<Button>("sun-day-button");
            _sunNightButton = root.Q<Button>("sun-night-button");
            _sunLightField.objectType = typeof(Light);
            _sunLightField.RegisterValueChangedCallback(_ => RefreshSunFromLight());
            _sunAzimuthSlider.RegisterValueChangedCallback(_ => ApplySunSliders());
            _sunElevationSlider.RegisterValueChangedCallback(_ => ApplySunSliders());
            _sunIntensitySlider.RegisterValueChangedCallback(_ => ApplySunSliders());
            _sunDayButton.clicked += () => JumpToSunRelative(antiSolar: false);
            _sunNightButton.clicked += () => JumpToSunRelative(antiSolar: true);

            _todPlayButton = root.Q<Button>("tod-play-button");
            _todResetButton = root.Q<Button>("tod-reset-button");
            _todSpeedSlider = root.Q<Slider>("tod-speed-slider");
            _todPlayButton.clicked += OnTodPlayClicked;
            _todResetButton.clicked += OnTodResetClicked;
            _todSpeedSlider.SetValueWithoutNotify(30f);

            _overlayBiomeToggle = root.Q<Toggle>("overlay-biome-toggle");
            _overlaySubzoneToggle = root.Q<Toggle>("overlay-subzone-toggle");
            _overlaySlopeToggle = root.Q<Toggle>("overlay-slope-toggle");
            _overlayAltitudeToggle = root.Q<Toggle>("overlay-altitude-toggle");
            _overlayActiveLayerToggle = root.Q<Toggle>("overlay-active-layer-toggle");
            _overlayActiveLayerGrid = root.Q<VisualElement>("overlay-active-layer-grid");
            _overlayScienceRegionToggle = root.Q<Toggle>("overlay-science-region-toggle");
            _overlayScienceRegionMode = root.Q<DropdownField>("overlay-science-region-mode");
            _overlayScienceRegionStatus = root.Q<Label>("overlay-science-region-status");
            _overlayStrengthSlider = root.Q<Slider>("overlay-strength-slider");
            _overlayBandHeightField = root.Q<FloatField>("overlay-band-height-field");
            BindOverlayToggle(_overlayBiomeToggle, PreviewOverlayKind.BiomeMask);
            BindOverlayToggle(_overlaySubzoneToggle, PreviewOverlayKind.SubzoneMask);
            BindOverlayToggle(_overlaySlopeToggle, PreviewOverlayKind.Slope);
            BindOverlayToggle(_overlayAltitudeToggle, PreviewOverlayKind.AltitudeBands);
            BindOverlayToggle(_overlayActiveLayerToggle, PreviewOverlayKind.ActiveLayer);
            BindOverlayToggle(_overlayScienceRegionToggle, PreviewOverlayKind.ScienceRegion);
            BuildActiveLayerGrid();
            _overlayScienceRegionMode.choices = new System.Collections.Generic.List<string>(ScienceRegionModeChoices);
            _overlayScienceRegionMode.RegisterValueChangedCallback(evt =>
            {
                if (_suppressOverlayUpdates) return;
                int idx = System.Array.IndexOf(ScienceRegionModeChoices, evt.newValue);
                if (idx < 0) return;
                PreviewOverlayManager.ScienceRegionMode = (ScienceRegionPreviewOverlay.Mode)idx;
            });
            _overlayStrengthSlider.RegisterValueChangedCallback(evt =>
            {
                if (_suppressOverlayUpdates) return;
                PreviewOverlayManager.Strength = evt.newValue;
            });
            _overlayBandHeightField.RegisterValueChangedCallback(evt =>
            {
                if (_suppressOverlayUpdates) return;
                PreviewOverlayManager.BandHeightMeters = evt.newValue;
            });
            PreviewOverlayManager.StateChanged += RefreshOverlayControls;
            RefreshOverlayControls();

            EditorApplication.update += OnEditorUpdate;
            PlanetPreviewState.ActiveChanged += OnPreviewStateChanged;
            // Catch scene swaps and hierarchy edits that change whether a body is available to start.
            root.schedule.Execute(RefreshUI).Every(500);
            RefreshUI();
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            PlanetPreviewState.ActiveChanged -= OnPreviewStateChanged;
            PreviewOverlayManager.StateChanged -= RefreshOverlayControls;
        }

        private void BindOverlayToggle(Toggle toggle, PreviewOverlayKind kind)
        {
            if (toggle == null)
                return;
            toggle.RegisterValueChangedCallback(evt =>
            {
                if (_suppressOverlayUpdates) return;
                PreviewOverlayManager.SetEnabled(kind, evt.newValue);
            });
        }

        private void BuildActiveLayerGrid()
        {
            if (_overlayActiveLayerGrid == null)
                return;

            _overlayActiveLayerGrid.Clear();
            _overlayActiveLayerGrid.AddToClassList("sdk-tile-grid");
            _overlayActiveLayerCells = new VisualElement[4, 4];

            // Header row: empty corner + L1..L4 labels.
            var headerRow = new VisualElement();
            headerRow.AddToClassList("sdk-tile-grid-row");
            var corner = new Label(string.Empty);
            corner.AddToClassList("sdk-tile-grid-corner");
            headerRow.Add(corner);
            for (int layer = 0; layer < 4; layer++)
            {
                var colHeader = new Label($"L{layer + 1}");
                colHeader.AddToClassList("sdk-tile-grid-col-header");
                headerRow.Add(colHeader);
            }
            _overlayActiveLayerGrid.Add(headerRow);

            // One row per biome (R/G/B/A) with a label + 4 clickable cells.
            for (int biome = 0; biome < 4; biome++)
            {
                var row = new VisualElement();
                row.AddToClassList("sdk-tile-grid-row");

                var rowHeader = new Label(BiomeLabels[biome]);
                rowHeader.AddToClassList("sdk-tile-grid-row-header");
                row.Add(rowHeader);

                for (int layer = 0; layer < 4; layer++)
                {
                    int capturedBiome = biome;
                    int capturedLayer = layer;
                    var cell = new VisualElement
                    {
                        tooltip = $"Biome {BiomeLabels[biome]} Layer {layer + 1} — click to toggle visibility in the Active layer overlay.",
                    };
                    cell.AddToClassList("sdk-tile-grid-cell");
                    cell.style.backgroundColor = BiomeColors[biome] * LayerBrightness[layer];
                    cell.RegisterCallback<ClickEvent>(_ =>
                    {
                        bool newState = !PreviewOverlayManager.IsLayerEnabled(capturedBiome, capturedLayer);
                        PreviewOverlayManager.SetLayerEnabled(capturedBiome, capturedLayer, newState);
                    });
                    _overlayActiveLayerCells[biome, layer] = cell;
                    row.Add(cell);
                }
                _overlayActiveLayerGrid.Add(row);
            }
            RefreshActiveLayerGrid();
        }

        private void RefreshActiveLayerGrid()
        {
            if (_overlayActiveLayerCells == null || _overlayActiveLayerGrid == null)
                return;
            // Hide the grid entirely when the Active layer overlay isn't enabled.
            bool overlayOn = PreviewOverlayManager.IsEnabled(PreviewOverlayKind.ActiveLayer);
            _overlayActiveLayerGrid.style.display = overlayOn ? DisplayStyle.Flex : DisplayStyle.None;
            if (!overlayOn)
                return;
            for (int biome = 0; biome < 4; biome++)
            {
                for (int layer = 0; layer < 4; layer++)
                {
                    bool enabled = PreviewOverlayManager.IsLayerEnabled(biome, layer);
                    _overlayActiveLayerCells[biome, layer].EnableInClassList("sdk-tile-grid-cell--disabled", !enabled);
                }
            }
        }

        private void RefreshOverlayControls()
        {
            if (_overlayBiomeToggle == null)
                return;
            _suppressOverlayUpdates = true;
            _overlayBiomeToggle.SetValueWithoutNotify(PreviewOverlayManager.IsEnabled(PreviewOverlayKind.BiomeMask));
            _overlaySubzoneToggle.SetValueWithoutNotify(PreviewOverlayManager.IsEnabled(PreviewOverlayKind.SubzoneMask));
            _overlaySlopeToggle.SetValueWithoutNotify(PreviewOverlayManager.IsEnabled(PreviewOverlayKind.Slope));
            _overlayAltitudeToggle.SetValueWithoutNotify(PreviewOverlayManager.IsEnabled(PreviewOverlayKind.AltitudeBands));
            _overlayActiveLayerToggle.SetValueWithoutNotify(PreviewOverlayManager.IsEnabled(PreviewOverlayKind.ActiveLayer));
            _overlayScienceRegionToggle.SetValueWithoutNotify(PreviewOverlayManager.IsEnabled(PreviewOverlayKind.ScienceRegion));
            _overlayScienceRegionMode.SetValueWithoutNotify(ScienceRegionModeChoices[(int)PreviewOverlayManager.ScienceRegionMode]);
            _overlayStrengthSlider.SetValueWithoutNotify(PreviewOverlayManager.Strength);
            _overlayBandHeightField.SetValueWithoutNotify(PreviewOverlayManager.BandHeightMeters);
            _suppressOverlayUpdates = false;
            RefreshActiveLayerGrid();
            RefreshScienceRegionStatus();
        }

        private void RefreshScienceRegionStatus()
        {
            if (_overlayScienceRegionStatus == null) return;
            bool overlayOn = PreviewOverlayManager.IsEnabled(PreviewOverlayKind.ScienceRegion);
            _overlayScienceRegionMode.style.display = overlayOn ? DisplayStyle.Flex : DisplayStyle.None;
            if (!overlayOn)
            {
                _overlayScienceRegionStatus.style.display = DisplayStyle.None;
                return;
            }
            ScienceRegionPreviewOverlay overlay = PreviewOverlayManager.TryGetScienceRegionOverlay();
            if (overlay == null || !overlay.HasScienceData)
            {
                _overlayScienceRegionStatus.style.display = DisplayStyle.Flex;
                _overlayScienceRegionStatus.text = "No ScienceRegionData asset matches the active body. Create one to populate the overlay.";
                return;
            }
            if (!overlay.HasBakedMap)
            {
                _overlayScienceRegionStatus.style.display = DisplayStyle.Flex;
                _overlayScienceRegionStatus.text = "Baked region map missing - bake from the inspector before the runtime can load it.";
                return;
            }
            if (overlay.IsBakeStale)
            {
                _overlayScienceRegionStatus.style.display = DisplayStyle.Flex;
                _overlayScienceRegionStatus.text = "Source map newer than baked asset - re-bake recommended.";
                return;
            }
            _overlayScienceRegionStatus.style.display = DisplayStyle.None;
        }

        private void OnPreviewStateChanged()
        {
            // Snapshot the body's starting rotation when a session begins. Stop the animator when it ends.
            var session = PlanetAuthoringSession.Active;
            if (session != null)
                SnapshotTodBase(session);
            else
            {
                _todPlaying = false;
                _todBaseValid = false;
                if (_todPlayButton != null)
                    _todPlayButton.text = "Play";
            }
            RefreshUI();
        }

        private void SnapshotTodBase(PlanetAuthoringSession session)
        {
            var sun = SunCoupling.CurrentSun;
            if (sun == null) return;
            _todBaseSunRotation = sun.transform.rotation;
            _todBaseValid = true;
        }

        private static Transform ResolveBodyRoot(PlanetAuthoringSession session)
        {
            if (session?.Pqs == null)
                return null;
            var body = BodyResolver.FindBody(session.Pqs);
            return body != null ? body.transform : null;
        }

        private void OnTodPlayClicked()
        {
            _todPlaying = !_todPlaying;
            _todLastTime = EditorApplication.timeSinceStartup;
            _todPlayButton.text = _todPlaying ? "Pause" : "Play";
        }

        private void OnTodResetClicked()
        {
            _todPlaying = false;
            _todPlayButton.text = "Play";
            if (!_todBaseValid) return;
            var sun = SunCoupling.CurrentSun;
            if (sun == null) return;
            Undo.RecordObject(sun.transform, "Reset sun rotation");
            sun.transform.rotation = _todBaseSunRotation;
            EditorUtility.SetDirty(sun);
            // Reflect the restored sun direction in the azimuth/elevation/intensity sliders.
            RefreshSunFromLight();
            SceneView.RepaintAll();
        }

        private void OnEditorUpdate()
        {
            if (!_todPlaying)
            {
                _todLastTime = EditorApplication.timeSinceStartup;
                return;
            }
            var session = PlanetAuthoringSession.Active;
            var bodyRoot = ResolveBodyRoot(session);
            var sun = SunCoupling.CurrentSun;
            if (bodyRoot == null || sun == null) return;
            var now = EditorApplication.timeSinceStartup;
            var dt = (float)(now - _todLastTime);
            _todLastTime = now;
            var speed = _todSpeedSlider != null ? _todSpeedSlider.value : 30f;
            // Rotate the sun around the body's polar axis (local +Y projected to world). Visually
            // this is the same day/night sweep as rotating the body around its own +Y, but the
            // framing rework wants the body to stay put. Only the sun moves.
            var polarAxisWorld = bodyRoot.rotation * Vector3.up;
            sun.transform.rotation = Quaternion.AngleAxis(speed * dt, polarAxisWorld) * sun.transform.rotation;
            RefreshSunFromLight();
            SceneView.RepaintAll();
        }

        private void RefreshUI()
        {
            if (_statusLabel == null) return;

            var session = PlanetAuthoringSession.Active;
            var active = session != null && session.IsAlive;
            var sceneBody = active ? null : FindBodyInActiveScene();

            if (active)
            {
                _previewButton.text = "Stop Preview";
                _previewButton.SetEnabled(true);
            }
            else if (sceneBody != null)
            {
                _previewButton.text = "Start Preview";
                _previewButton.SetEnabled(true);
            }
            else
            {
                _previewButton.text = "Start Preview";
                _previewButton.SetEnabled(false);
            }

            _pickButton.SetEnabled(active);
            _lookAtButton.SetEnabled(active);
            _copyButton.SetEnabled(true);
            _pasteButton.SetEnabled(true);

            var hasPqs = active && session.Pqs != null;
            _jumpSurfaceButton.SetEnabled(hasPqs);
            _jumpTransitionButton.SetEnabled(hasPqs && session.Pqs.data?.heightMapInfo != null);
            _jumpLowOrbitButton.SetEnabled(hasPqs);
            _jumpHighOrbitButton.SetEnabled(hasPqs);
            _jumpFarButton.SetEnabled(hasPqs);
            SortJumpButtonsByAltitude(session?.Pqs);
            EnsureSunLightAssigned();
            RefreshSunFromLight();

            if (!active)
            {
                _statusLabel.text = sceneBody != null
                    ? $"Ready to preview '{sceneBody.name}' in the active scene."
                    : "No active preview and no celestial body found in the active scene. Open an authoring scene to start.";
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

            // Stale-bake state can change while the window is open if the artist edits the source map.
            RefreshScienceRegionStatus();
        }

        private void OnPreviewButtonClicked()
        {
            var session = PlanetAuthoringSession.Active;
            if (session != null)
            {
                session.End();
                RefreshUI();
                return;
            }

            var body = FindBodyInActiveScene();
            if (body == null) return;
            PlanetAuthoringSession.Begin(body);
            RefreshUI();
        }

        private void EnsureSunLightAssigned()
        {
            if (_sunLightField.value is Light) return;
            var brightest = FindBrightestDirectionalLight();
            if (brightest != null)
            {
                _sunLightField.SetValueWithoutNotify(brightest);
            }
        }

        private static Light FindBrightestDirectionalLight()
        {
            Light brightest = null;
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (l.type != LightType.Directional) continue;
                if (brightest == null || l.intensity > brightest.intensity)
                {
                    brightest = l;
                }
            }
            return brightest;
        }

        private void RefreshSunFromLight()
        {
            var light = _sunLightField.value as Light;
            // Body-rotation framing needs to rotate the sun in lock-step. Publish the currently
            // tracked Light here so SceneViewFraming / PlanetAuthoringSession can read it.
            SunCoupling.CurrentSun = light;
            var hasLight = light != null;
            _sunAzimuthSlider.SetEnabled(hasLight);
            _sunElevationSlider.SetEnabled(hasLight);
            _sunIntensitySlider.SetEnabled(hasLight);
            _sunDayButton.SetEnabled(hasLight);
            _sunNightButton.SetEnabled(hasLight);
            if (!hasLight) return;

            // Sun direction = direction from sun toward origin = -light.transform.forward.
            var sunDir = -light.transform.forward;
            var elevation = Mathf.Asin(Mathf.Clamp(sunDir.y, -1f, 1f)) * Mathf.Rad2Deg;
            var azimuth = Mathf.Atan2(sunDir.x, sunDir.z) * Mathf.Rad2Deg;
            if (azimuth < 0) azimuth += 360f;

            _suppressSunUpdates = true;
            _sunAzimuthSlider.SetValueWithoutNotify(azimuth);
            _sunElevationSlider.SetValueWithoutNotify(elevation);
            _sunIntensitySlider.SetValueWithoutNotify(light.intensity);
            _suppressSunUpdates = false;
        }

        private void JumpToSunRelative(bool antiSolar)
        {
            if (_sunLightField.value is not Light light)
                return;
            PQS planet = PlanetAuthoringSession.Active?.Pqs;
            if (planet == null)
                return;
            // Direction from body center toward the sun (or anti-solar).
            Vector3 toSun = -light.transform.forward.normalized;
            if (toSun.sqrMagnitude < 1e-6f)
                return;
            SceneViewFraming.FrameAtDirection(planet, antiSolar ? -toSun : toSun);
        }

        private void ApplySunSliders()
        {
            if (_suppressSunUpdates)
                return;
            if (_sunLightField.value is not Light light)
                return;

            float azimRad = _sunAzimuthSlider.value * Mathf.Deg2Rad;
            float elevRad = _sunElevationSlider.value * Mathf.Deg2Rad;
            float cosElev = Mathf.Cos(elevRad);
            Vector3 sunDir = new(cosElev * Mathf.Sin(azimRad), Mathf.Sin(elevRad), cosElev * Mathf.Cos(azimRad));

            Undo.RecordObject(light.transform, "Set Sun Direction");
            Undo.RecordObject(light, "Set Sun Intensity");
            light.transform.rotation = Quaternion.LookRotation(-sunDir);
            light.intensity = _sunIntensitySlider.value;
            EditorUtility.SetDirty(light);
            EditorUtility.SetDirty(light.transform);
        }

        private static CoreCelestialBodyData FindBodyInActiveScene()
        {
            Scene scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
                return null;
            foreach (GameObject go in scene.GetRootGameObjects())
            {
                var body = go.GetComponentInChildren<CoreCelestialBodyData>(true);
                if (body != null)
                    return body;
            }
            return null;
        }

        private void OnPickButtonClicked()
        {
            PlanetSurfacePickTool.Begin(latLon =>
            {
                _latField.value = latLon.x;
                _lonField.value = latLon.y;
            });
        }

        private void OnLookAtClicked()
        {
            LookAtLatLon(_latField.value, _lonField.value);
        }

        private void OnCopyClicked()
        {
            EditorGUIUtility.systemCopyBuffer =
                $"{_latField.value.ToString("0.000000", CultureInfo.InvariantCulture)},{_lonField.value.ToString("0.000000", CultureInfo.InvariantCulture)}";
        }

        private void OnPasteClicked()
        {
            string clip = EditorGUIUtility.systemCopyBuffer;
            if (string.IsNullOrWhiteSpace(clip))
                return;
            string[] parts = clip.Split(',');
            if (parts.Length != 2)
                return;
            if (!float.TryParse(parts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float lat))
                return;
            if (!float.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float lon))
                return;
            _latField.value = lat;
            _lonField.value = lon;
        }

        private enum JumpAltitude { Surface, Transition, LowOrbit, HighOrbit, Far }

        private static void JumpToAltitude(JumpAltitude target)
        {
            var session = PlanetAuthoringSession.Active;
            PQS planet = session?.Pqs;
            if (planet == null)
                return;
            var body = BodyResolver.FindBody(planet);
            double radius = body?.Data?.radius ?? 0;
            if (radius <= 0)
                return;

            double altitudeAboveSurface = ComputeAltitudeMeters(target, planet, body, radius);
            SceneFramingMode mode = target == JumpAltitude.Surface ? SceneFramingMode.Surface : SceneFramingMode.Side;
            SceneViewFraming.FrameAtAltitude(planet, altitudeAboveSurface, mode);
        }

        private static double ComputeAltitudeMeters(JumpAltitude target, PQS planet, CoreCelestialBodyData body, double radius, bool forSorting = false)
        {
            switch (target)
            {
                case JumpAltitude.Surface:
                    // The slider may sit anywhere in [1, 10] m, but the sort key stays at a constant
                    // 1 m so dragging the slider doesn't reshuffle the button row underneath the
                    // user's cursor.
                    return forSorting ? 1.0 : SurfaceFramingPrefs.AltitudeMeters;
                case JumpAltitude.Transition:
                {
                    var info = planet?.data?.heightMapInfo;
                    return info != null ? info.scaledToLocalTransition : radius * 0.4;
                }
                case JumpAltitude.LowOrbit:
                {
                    double atmoTop = body?.Data?.hasAtmosphere == true ? body.Data.atmosphereDepth + 30000.0 : 0;
                    return Math.Max(atmoTop, radius * 0.1);
                }
                case JumpAltitude.HighOrbit:
                    return radius * 1.0;
                case JumpAltitude.Far:
                    return radius * 5.0;
                default:
                    return radius * 0.1;
            }
        }

        private void SortJumpButtonsByAltitude(PQS planet)
        {
            if (planet == null)
                return;
            var body = BodyResolver.FindBody(planet);
            double radius = body?.Data?.radius ?? 0;

            var entries = new (Button btn, double alt)[]
            {
                (_jumpSurfaceButton, ComputeAltitudeMeters(JumpAltitude.Surface, planet, body, radius, forSorting: true)),
                (_jumpTransitionButton, ComputeAltitudeMeters(JumpAltitude.Transition, planet, body, radius)),
                (_jumpLowOrbitButton, ComputeAltitudeMeters(JumpAltitude.LowOrbit, planet, body, radius)),
                (_jumpHighOrbitButton, ComputeAltitudeMeters(JumpAltitude.HighOrbit, planet, body, radius)),
                (_jumpFarButton, ComputeAltitudeMeters(JumpAltitude.Far, planet, body, radius)),
            };
            Array.Sort(entries, (a, b) => a.alt.CompareTo(b.alt));

            for (int i = 0; i < entries.Length; i++)
            {
                Button btn = entries[i].btn;
                if (_jumpRow.IndexOf(btn) != i)
                {
                    _jumpRow.Remove(btn);
                    _jumpRow.Insert(i, btn);
                }
            }
        }


        private static void LookAtLatLon(double lat, double lon)
        {
            SceneViewFraming.FrameAtLatLon(PlanetAuthoringSession.Active?.Pqs, lat, lon);
        }

        private static string FormatMeters(float m)
        {
            return Mathf.Abs(m) >= 1000f ? $"{m / 1000f:0.##} km" : $"{m:0.#} m";
        }
    }
}
