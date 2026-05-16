using System;
using System.Globalization;
using System.IO;
using KSP;
using KSP.Game.Science;
using KSP.Rendering.Planets;
using KSP.Tools.PQSFreeCamUtils;
using Ksp2UnityTools.Editor.PlanetAuthoring.Science;
using Ksp2UnityTools.Editor.PlanetAuthoring.Tools;
using Ksp2UnityTools.Editor.PlanetAuthoring.Windows;
using Ksp2UnityTools.Editor.ScriptableObjects;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Inspectors
{
    /// <summary>
    /// Custom inspector for <see cref="SurfaceLandmark" />.
    /// </summary>
    /// <remarks>
    /// Renders three foldable sections (Decal / Prefab / Discoverable), each with an Enabled toggle
    /// and the subset of fields that drive its managed child, plus the standard keyable
    /// lat/lon/altitude location block with pick / copy / paste / framing buttons. The
    /// lat/lon/altitude doubles on <see cref="SurfaceLandmark" /> are the source of truth for
    /// placement. PropertyField bindings push edits through the SerializedObject, and the per-field
    /// change callback runs <see cref="SurfaceLandmarkSync.Sync" />, which derives the wrapper
    /// transform and updates the managed children. The terrain readout polls every 500ms to stay
    /// current with handle drags.
    /// </remarks>
    [CustomEditor(typeof(SurfaceLandmark))]
    public sealed class SurfaceLandmarkEditor : UnityEditor.Editor
    {
        private const string UxmlPath = "/Assets/Windows/PlanetAuthoring/Inspectors/SurfaceLandmarkInspector.uxml";

        private FloatField _atmField;
        private FloatField _splField;
        private FloatField _lndField;
        private Label _terrainLabel;
        private Label _insetLabel;
        private Label _scienceStatusLabel;
        private VisualElement _decalHelpSlot;
        private VisualElement _decalTemplateSlot;
        private VisualElement _decalOverridesSlot;
        private Toggle _prefabRawKeyToggle;
        private VisualElement _prefabModeGroup;
        private VisualElement _prefabRawKeyModeGroup;
        private Label _prefabRawKeyStatus;
        private UnityEditor.Editor _decalTemplateEditor;
        private PQSDecal _trackedDecalTemplate;
        private PQSDecalInstance _trackedManagedDecal;
        private bool _trackedSmoothingEnabled;

        private void OnEnable()
        {
            EditorApplication.update += SyncToolsHidden;
        }

        private void OnDisable()
        {
            EditorApplication.update -= SyncToolsHidden;
            UnityEditor.Tools.hidden = false;
            if (_decalTemplateEditor != null)
            {
                DestroyImmediate(_decalTemplateEditor);
                _decalTemplateEditor = null;
            }
        }

        private static void SyncToolsHidden()
        {
            // Letting hidden=true while a pick/place EditorTool is active stops the tool from drawing or receiving input - Tools.hidden suppresses the entire tool layer, not just the built-in transform gizmos.
            if (PlanetAuthoringTools.IsExclusiveToolActive())
            {
                UnityEditor.Tools.hidden = false;
                return;
            }
            UnityEditor.Tools.hidden = PlanetAuthoringSession.Active != null;
        }

        private void OnSceneGUI()
        {
            if (PlanetAuthoringSession.Active == null) return;
            // Suppress when a pick/place EditorTool is active so its clicks aren't stolen by this handle.
            if (PlanetAuthoringTools.IsExclusiveToolActive()) return;
            var landmark = (SurfaceLandmark)target;
            if (landmark == null) return;
            if (!TryGetBody(landmark, out var pqs, out var bodyTransform, out _)) return;

            if (SurfaceTransformHandles.DrawSurfaceMoveHandle(landmark.transform, pqs, bodyTransform, (float)landmark.Altitude, "Move SurfaceLandmark", out var newLatLon))
            {
                Undo.RecordObject(landmark, "Move SurfaceLandmark");
                landmark.Latitude = newLatLon.x;
                landmark.Longitude = newLatLon.y;
                EditorUtility.SetDirty(landmark);
                SurfaceLandmarkSync.Sync(landmark);
            }
            if (SurfaceTransformHandles.DrawSurfaceYawHandle(landmark.transform, "Rotate SurfaceLandmark"))
            {
                EditorUtility.SetDirty(landmark);
                SurfaceLandmarkSync.Sync(landmark);
            }
        }

        /// <inheritdoc />
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(SDKConfiguration.BasePath + UxmlPath);
            if (tree == null)
            {
                root.Add(new Label("Failed to load SurfaceLandmarkInspector.uxml"));
                return root;
            }
            tree.CloneTree(root);

            Ksp2UnityToolsStyles.Apply(root);

            _terrainLabel = root.Q<Label>("terrain-label");
            _insetLabel = root.Q<Label>("inset-label");
            _decalHelpSlot = root.Q<VisualElement>("decal-help-slot");
            _decalHelpSlot.Add(new HelpBox(
                "Smoothing mode: replace blend, height offset 1, height scale tracks terrain, circular fade configurable. Cosmetic mode: decal template's own appearance, no height modification.",
                HelpBoxMessageType.None));
            _decalTemplateSlot = root.Q<VisualElement>("decal-template-slot");
            _decalOverridesSlot = root.Q<VisualElement>("decal-overrides-slot");

            _atmField = root.Q<FloatField>("atm-field");
            _splField = root.Q<FloatField>("spl-field");
            _lndField = root.Q<FloatField>("lnd-field");
            _scienceStatusLabel = root.Q<Label>("science-region-status");
            _atmField.isDelayed = true;
            _splField.isDelayed = true;
            _lndField.isDelayed = true;
            _atmField.RegisterValueChangedCallback(evt => ApplyScienceScalar(d => d.AtmosphereScalar = evt.newValue));
            _splField.RegisterValueChangedCallback(evt => ApplyScienceScalar(d => d.SplashedScalar = evt.newValue));
            _lndField.RegisterValueChangedCallback(evt => ApplyScienceScalar(d => d.LandedScalar = evt.newValue));

            root.Q<Button>("pick-button").clicked += OnPickClicked;
            root.Q<Button>("copy-button").clicked += OnCopyClicked;
            root.Q<Button>("paste-button").clicked += OnPasteClicked;
            root.Q<Button>("frame-above-button").clicked += OnFrameAboveClicked;
            root.Q<Button>("frame-surface-button").clicked += OnFrameSurfaceClicked;
            root.Q<Button>("create-decal-button").clicked += OnCreateDecalClicked;

            var landmark = (SurfaceLandmark)target;
            _prefabRawKeyToggle = root.Q<Toggle>("prefab-raw-key-toggle");
            _prefabModeGroup = root.Q<VisualElement>("prefab-mode-group");
            _prefabRawKeyModeGroup = root.Q<VisualElement>("prefab-raw-key-mode-group");
            _prefabRawKeyStatus = root.Q<Label>("prefab-raw-key-status");
            _prefabRawKeyToggle.SetValueWithoutNotify(landmark.UseRawAddressableKey);
            ApplyPrefabAuthoringMode(landmark.UseRawAddressableKey);
            _prefabRawKeyToggle.RegisterValueChangedCallback(OnPrefabRawKeyToggleChanged);

            root.schedule.Execute(RefreshReadouts).Every(500);
            root.schedule.Execute(MaybeRebuildDecalTemplateSection).Every(500);
            RefreshReadouts();
            RebuildDecalTemplateSection();

            root.Bind(serializedObject);

            // PropertyField edits write to the SerializedObject but don't otherwise notify the
            // editor. Without these callbacks the managed children would only sync when the user
            // drags the lat/lon/alt fields. RegisterValueChangeCallback runs per-field after the
            // binding system applies the change.
            foreach (var field in root.Query<PropertyField>().ToList())
            {
                field.RegisterValueChangeCallback(_ => SyncTarget());
            }

            return root;
        }

        private void SyncTarget()
        {
            if (target is SurfaceLandmark landmark)
            {
                SurfaceLandmarkSync.Sync(landmark);
            }
        }

        private void OnPrefabRawKeyToggleChanged(ChangeEvent<bool> evt)
        {
            var landmark = (SurfaceLandmark)target;
            Undo.RecordObject(landmark, "Toggle SurfaceLandmark prefab authoring mode");
            landmark.UseRawAddressableKey = evt.newValue;
            EditorUtility.SetDirty(landmark);
            ApplyPrefabAuthoringMode(evt.newValue);
            SurfaceLandmarkSync.Sync(landmark);
        }

        private void ApplyPrefabAuthoringMode(bool rawKey)
        {
            if (_prefabModeGroup != null)
            {
                _prefabModeGroup.style.display = rawKey ? DisplayStyle.None : DisplayStyle.Flex;
            }
            if (_prefabRawKeyModeGroup != null)
            {
                _prefabRawKeyModeGroup.style.display = rawKey ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        private void UpdatePrefabRawKeyStatus(SurfaceLandmark landmark)
        {
            if (_prefabRawKeyStatus == null) return;
            var key = landmark.PrefabAddressableKey;
            if (string.IsNullOrEmpty(key))
            {
                _prefabRawKeyStatus.text = "Key is empty. Nothing will load at runtime.";
                return;
            }
            var resolved = AddressableKeyLookup.GetPrefab(key);
            _prefabRawKeyStatus.text = resolved != null
                ? $"Resolves to '{resolved.name}' in this project."
                : "Key does not resolve to a prefab in this project. The runtime will still try to load it from addressables.";
        }

        private void RefreshReadouts()
        {
            if (target == null) return;
            var landmark = (SurfaceLandmark)target;
            if (!TryGetBody(landmark, out var pqs, out _, out var bodyRadius))
            {
                _terrainLabel.text = "Lat/Lon: not under a PQS.";
                _insetLabel.text = string.Empty;
                return;
            }
            // Readouts derive from the landmark's authored doubles. Lat/lon/altitude themselves are
            // bound to PropertyFields - the binding system handles undo and dirty automatically.
            var localDir = LatLon.GetRelSurfaceNVector(landmark.Latitude, landmark.Longitude);
            if (TrySurfaceHeight(pqs, localDir, out var terrainHeight))
            {
                _terrainLabel.text = $"Terrain (no decal): {terrainHeight - bodyRadius:0.0} m";
            }
            else
            {
                _terrainLabel.text = "Terrain (no decal): unavailable. Enable a planet preview to query terrain.";
            }
            var inset = SurfaceLandmarkSync.ComputeCurvatureInset(bodyRadius, Mathf.Max(0f, landmark.PrefabWidth * 0.5f));
            _insetLabel.text = $"Curvature inset: {inset:0.000} m";
            RefreshScienceFields(landmark);
            UpdatePrefabRawKeyStatus(landmark);
        }

        private void RefreshScienceFields(SurfaceLandmark landmark)
        {
            var def = ResolveScienceRegionDef(landmark, out var statusHint);
            var enabled = def != null;
            _atmField.SetEnabled(enabled);
            _splField.SetEnabled(enabled);
            _lndField.SetEnabled(enabled);
            _scienceStatusLabel.text = statusHint;
            if (enabled)
            {
                if (!Mathf.Approximately(_atmField.value, def.AtmosphereScalar)) _atmField.SetValueWithoutNotify(def.AtmosphereScalar);
                if (!Mathf.Approximately(_splField.value, def.SplashedScalar)) _splField.SetValueWithoutNotify(def.SplashedScalar);
                if (!Mathf.Approximately(_lndField.value, def.LandedScalar)) _lndField.SetValueWithoutNotify(def.LandedScalar);
            }
        }

        private void ApplyScienceScalar(Action<ScienceRegionData.ExtendedScienceRegionDefinition> mutate)
        {
            if (target is not SurfaceLandmark landmark) return;
            var def = ResolveScienceRegionDef(landmark, out _);
            if (def == null) return;
            var data = ResolveScienceRegionData(landmark);
            if (data == null) return;
            Undo.RecordObject(data, "Edit landmark science scalar");
            mutate(def);
            EditorUtility.SetDirty(data);
        }

        private static ScienceRegionData ResolveScienceRegionData(SurfaceLandmark landmark)
        {
            var pqs = landmark.GetComponentInParent<PQS>();
            var bodyName = pqs?.CoreCelestialBodyData?.Data?.bodyName;
            return string.IsNullOrEmpty(bodyName) ? null : ScienceRegionAssetLocator.FindForBody(bodyName);
        }

        private static ScienceRegionData.ExtendedScienceRegionDefinition ResolveScienceRegionDef(SurfaceLandmark landmark, out string statusHint)
        {
            statusHint = string.Empty;
            if (!landmark.EnableDiscoverable)
            {
                statusHint = "Enable the discoverable to edit its science scalars.";
                return null;
            }
            if (string.IsNullOrEmpty(landmark.DiscoverableRegionId))
            {
                statusHint = "Region Id not assigned yet.";
                return null;
            }
            var data = ResolveScienceRegionData(landmark);
            if (data?.information?.ScienceRegionDefinitions == null)
            {
                statusHint = "No ScienceRegionData asset for this body.";
                return null;
            }
            foreach (var def in data.information.ScienceRegionDefinitions)
            {
                if (def != null && string.Equals(def.Id, landmark.DiscoverableRegionId, System.StringComparison.Ordinal))
                {
                    return def;
                }
            }
            statusHint = "Region definition not found yet. The sync will create it on the next edit.";
            return null;
        }

        private void OnPickClicked()
        {
            var landmark = (SurfaceLandmark)target;
            if (landmark == null) return;
            PlanetSurfacePickTool.Begin(latLon =>
            {
                if (target == null) return;
                Undo.RecordObject(landmark, "Pick landmark location");
                landmark.Latitude = latLon.x;
                landmark.Longitude = latLon.y;
                landmark.Altitude = 0.0;
                EditorUtility.SetDirty(landmark);
                SurfaceLandmarkSync.Sync(landmark);
            });
        }

        private void OnCopyClicked()
        {
            var landmark = (SurfaceLandmark)target;
            if (landmark == null) return;
            EditorGUIUtility.systemCopyBuffer =
                $"{landmark.Latitude.ToString("0.000000", CultureInfo.InvariantCulture)},{landmark.Longitude.ToString("0.000000", CultureInfo.InvariantCulture)}";
        }

        private void OnPasteClicked()
        {
            var landmark = (SurfaceLandmark)target;
            if (landmark == null) return;
            var clip = EditorGUIUtility.systemCopyBuffer;
            if (string.IsNullOrWhiteSpace(clip)) return;
            var parts = clip.Split(',');
            if (parts.Length != 2) return;
            if (!double.TryParse(parts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var lat)) return;
            if (!double.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var lon)) return;
            Undo.RecordObject(landmark, "Paste SurfaceLandmark lat/lon");
            landmark.Latitude = lat;
            landmark.Longitude = lon;
            EditorUtility.SetDirty(landmark);
            SurfaceLandmarkSync.Sync(landmark);
        }

        private void OnFrameAboveClicked()
        {
            var landmark = (SurfaceLandmark)target;
            var pqs = landmark.GetComponentInParent<PQS>();
            if (pqs == null) return;
            SceneViewFraming.FrameAtLatLon(pqs, (float)landmark.Latitude, (float)landmark.Longitude);
        }

        private void RebuildDecalTemplateSection()
        {
            if (_decalTemplateSlot == null) return;
            _decalTemplateSlot.Clear();
            _decalOverridesSlot?.Clear();
            if (_decalTemplateEditor != null)
            {
                DestroyImmediate(_decalTemplateEditor);
                _decalTemplateEditor = null;
            }
            if (target is not SurfaceLandmark landmark) return;
            _trackedDecalTemplate = landmark.SmoothingDecal;
            _trackedManagedDecal = landmark.ManagedDecal;
            _trackedSmoothingEnabled = landmark.EnableSmoothing;
            // Per-instance overrides only show when smoothing is OFF. Smoothing-mode landmarks
            // have their height behavior driven entirely by sync-applied overrides, so exposing
            // the override toggles would be misleading. The template asset itself is never
            // editable inline (its values are shared across every instance referencing it); the
            // artist opens it via the project view if they need to tune template defaults.
            if (landmark.EnableSmoothing) return;
            if (landmark.ManagedDecal != null && _decalOverridesSlot != null)
            {
                BuildDecalOverrideRows(landmark.ManagedDecal);
            }
        }

        private void BuildDecalOverrideRows(PQSDecalInstance managedDecal)
        {
            _decalOverridesSlot.Add(new Label("Per-instance overrides")
            {
                style = { unityFontStyleAndWeight = FontStyle.Bold, marginTop = 6f, marginBottom = 2f },
            });
            var so = new SerializedObject(managedDecal);
            foreach (var pair in PQSDecalInstanceEditor.OverridePairs)
            {
                var overrideProp = so.FindProperty(pair.overrideField);
                var valueProp = so.FindProperty(pair.valueField);
                if (overrideProp == null || valueProp == null) continue;
                _decalOverridesSlot.Add(PQSDecalInstanceEditor.BuildOverrideRow(overrideProp, valueProp, pair.label, pair.tooltip));
            }
            _decalOverridesSlot.Bind(so);
        }

        private void MaybeRebuildDecalTemplateSection()
        {
            if (target is not SurfaceLandmark landmark) return;
            if (landmark.SmoothingDecal == _trackedDecalTemplate
                && landmark.ManagedDecal == _trackedManagedDecal
                && landmark.EnableSmoothing == _trackedSmoothingEnabled)
            {
                return;
            }
            RebuildDecalTemplateSection();
        }

        private void OnCreateDecalClicked()
        {
            var landmark = (SurfaceLandmark)target;
            if (landmark == null) return;
            var pqs = landmark.GetComponentInParent<PQS>();
            var body = BodyResolver.FindBody(landmark);
            var folder = ResolveBodyFolder(body);
            var defaultName = (body?.name ?? "Body") + "_Landmark_Decal";
            NewDecalPromptWindow.Show(defaultName, result =>
            {
                var template = result.ExistingTemplate != null
                    ? result.ExistingTemplate
                    : CreatePqsDecalAsset.CreateConfigured(folder, result);
                if (template == null) return;
                Undo.RecordObject(landmark, "Assign landmark decal template");
                landmark.SmoothingDecal = template;
                EditorUtility.SetDirty(landmark);
                SurfaceLandmarkSync.Sync(landmark);
            });
        }

        private static string ResolveBodyFolder(CoreCelestialBodyData body)
        {
            if (body == null) return "Assets";
            var scenePath = body.gameObject.scene.path;
            if (string.IsNullOrEmpty(scenePath)) return "Assets";
            var dir = Path.GetDirectoryName(scenePath)?.Replace('\\', '/');
            return !string.IsNullOrEmpty(dir) && AssetDatabase.IsValidFolder(dir) ? dir : "Assets";
        }

        private void OnFrameSurfaceClicked()
        {
            var landmark = (SurfaceLandmark)target;
            var pqs = landmark.GetComponentInParent<PQS>();
            if (pqs == null) return;
            SceneViewFraming.FrameAtLatLonAndAltitude(pqs, (float)landmark.Latitude, (float)landmark.Longitude, SurfaceFramingPrefs.AltitudeMeters, SceneFramingMode.Surface);
        }

        private static bool TryGetBody(SurfaceLandmark landmark, out PQS pqs, out Transform bodyTransform, out float radius)
        {
            bodyTransform = null;
            radius = 0;
            pqs = landmark != null ? landmark.GetComponentInParent<PQS>() : null;
            if (pqs == null) return false;
            bodyTransform = BodyResolver.FindBody(landmark)?.transform ?? pqs.transform;
            radius = (float)(pqs.CoreCelestialBodyData?.Data?.radius ?? 0.0);
            return radius > 0;
        }

        /// <summary>
        /// Calls <see cref="PQS.GetSurfaceHeight" /> guarded by the runtime's renderer + decal
        /// controller readiness. Falls back to body radius when the PQS isn't fully alive.
        /// </summary>
        private static bool TrySurfaceHeight(PQS pqs, Vector3 localDir, out double height)
        {
            height = 0.0;
            if (pqs.PQSRenderer == null || pqs.PQSRenderer.PqsDecalController == null
                || pqs.PQSRenderer.PqsDecalController.PqsDecalData == null)
            {
                return false;
            }
            // Landmarks place their own smoothing decal that flattens the terrain to their own
            // height. Sampling with decals would feed the landmark's own contribution back in,
            // pinning the altitude readout to ~0. Sample bare terrain so altitude reflects the
            // natural surface beneath.
            // Try/catch covers the domain-reload window where PQS native buffers
            // (EmptyDecalInstanceList etc.) are disposed but the managed PQS reference is still
            // live. The polled refresh would otherwise spam the console.
            try
            {
                height = pqs.GetSurfaceHeight(localDir, includeDecals: false);
                return true;
            }
            catch (System.ObjectDisposedException)
            {
                return false;
            }
        }
    }
}
