using System.Globalization;
using KSP;
using KSP.Rendering.Planets;
using KSP.Tools.PQSFreeCamUtils;
using Ksp2UnityTools.Editor.PlanetAuthoring.Tools;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Inspectors
{
    /// <summary>
    /// Custom inspector for <see cref="PrefabSpawner" /> that exposes the spawned prefab as a typed
    /// asset slot, surfaces the resolved addressable key, and provides keyable lat/lon fields with
    /// the standard pick / copy / paste / framing buttons.
    /// </summary>
    /// <remarks>
    /// PrefabSpawner stores its prefab as a string addressable key. Authoring against a string is
    /// fragile (typos, key drift on rename), so the inspector accepts a GameObject prefab and
    /// resolves its addressable key via <see cref="AddressableKeyLookup" />. Lat/lon edits snap the
    /// spawner's transform to the body's surface (terrain-included) at the new lat/lon. Framing
    /// buttons match the discoverable pattern: above (default mode) and surface (Surface mode).
    /// </remarks>
    [CustomEditor(typeof(PrefabSpawner))]
    public sealed class PrefabSpawnerEditor : UnityEditor.Editor
    {
        private const string UxmlPath = "/Assets/Windows/PrefabSpawnerInspector.uxml";

        private Toggle _rawKeyToggle;
        private VisualElement _prefabModeGroup;
        private VisualElement _rawKeyModeGroup;
        private ObjectField _prefabField;
        private HelpBox _addressableWarning;
        private Button _makeAddressableButton;
        private Label _keyLabel;
        private TextField _rawKeyField;
        private Label _rawKeyStatus;
        private FloatField _latField;
        private FloatField _lonField;
        private FloatField _altField;

        // Lat/lon/altitude fields are the source of truth. They're populated once when the
        // inspector first becomes valid (PQS available) and after that only change via direct
        // user edits or the scene-view move handle. The polled refresh never writes them back
        // from the transform.
        private bool _fieldsPrimed;

        private void OnEnable()
        {
            EditorApplication.update += SyncToolsHidden;
            _fieldsPrimed = false;
        }

        private void OnDisable()
        {
            EditorApplication.update -= SyncToolsHidden;
            UnityEditor.Tools.hidden = false;
            _fieldsPrimed = false;
        }

        private static void SyncToolsHidden()
        {
            UnityEditor.Tools.hidden = PlanetAuthoringSession.Active != null;
        }

        private void OnSceneGUI()
        {
            if (PlanetAuthoringSession.Active == null) return;
            var spawner = (PrefabSpawner)target;
            if (spawner == null) return;
            var pqs = spawner.GetComponentInParent<PQS>();
            if (pqs == null) return;
            var bodyTransform = BodyResolver.FindBody(spawner)?.transform ?? pqs.transform;

            if (SurfaceTransformHandles.DrawSurfaceMoveHandle(spawner.transform, pqs, bodyTransform, _altField?.value ?? 0f, "Move PrefabSpawner", out var newLatLon))
            {
                // Write the dropped lat/lon back to the fields so they remain the source of truth.
                // Altitude is unchanged by the drag (it stays at the typed value).
                _latField?.SetValueWithoutNotify(newLatLon.x);
                _lonField?.SetValueWithoutNotify(newLatLon.y);
                EditorUtility.SetDirty(spawner);
            }
            if (SurfaceTransformHandles.DrawSurfaceYawHandle(spawner.transform, "Rotate PrefabSpawner"))
            {
                EditorUtility.SetDirty(spawner);
            }
        }

        /// <inheritdoc />
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(SDKConfiguration.BasePath + UxmlPath);
            if (tree == null)
            {
                root.Add(new Label("Failed to load PrefabSpawnerInspector.uxml"));
                return root;
            }
            tree.CloneTree(root);

            Ksp2UnityToolsStyles.Apply(root);

            var spawner = (PrefabSpawner)target;
            _rawKeyToggle = root.Q<Toggle>("raw-key-toggle");
            _prefabModeGroup = root.Q<VisualElement>("prefab-mode-group");
            _rawKeyModeGroup = root.Q<VisualElement>("raw-key-mode-group");
            _rawKeyToggle.SetValueWithoutNotify(spawner.UseRawAddressableKeyAuthoring);
            ApplyAuthoringMode(spawner.UseRawAddressableKeyAuthoring);
            _rawKeyToggle.RegisterValueChangedCallback(OnRawKeyToggleChanged);

            _rawKeyField = root.Q<TextField>("raw-key-field");
            _rawKeyField.isDelayed = true;
            _rawKeyField.SetValueWithoutNotify(spawner.prefabName ?? string.Empty);
            _rawKeyField.RegisterValueChangedCallback(OnRawKeyFieldChanged);
            _rawKeyStatus = root.Q<Label>("raw-key-status");

            _prefabField = root.Q<ObjectField>("prefab-field");
            _prefabField.RegisterValueChangedCallback(OnPrefabChanged);

            _addressableWarning = new HelpBox(string.Empty, HelpBoxMessageType.Warning)
            {
                style = { display = DisplayStyle.None },
            };
            root.Q<VisualElement>("addressable-warning-slot").Add(_addressableWarning);

            _makeAddressableButton = root.Q<Button>("make-addressable-btn");
            _makeAddressableButton.clicked += OnMakeAddressableClicked;

            _keyLabel = root.Q<Label>("key-label");

            _latField = root.Q<FloatField>("lat-field");
            _lonField = root.Q<FloatField>("lon-field");
            _altField = root.Q<FloatField>("alt-field");
            _latField.isDelayed = true;
            _lonField.isDelayed = true;
            _altField.isDelayed = true;
            _latField.RegisterValueChangedCallback(_ => ApplyLatLonAltFromFields());
            _lonField.RegisterValueChangedCallback(_ => ApplyLatLonAltFromFields());
            _altField.RegisterValueChangedCallback(_ => ApplyLatLonAltFromFields());

            root.Q<Button>("pick-button").clicked += OnPickClicked;
            root.Q<Button>("copy-button").clicked += OnCopyClicked;
            root.Q<Button>("paste-button").clicked += OnPasteClicked;
            root.Q<Button>("frame-above-button").clicked += OnFrameAboveClicked;
            root.Q<Button>("frame-surface-button").clicked += OnFrameSurfaceClicked;

            root.schedule.Execute(RefreshDisplay).Every(500);
            RefreshDisplay();
            return root;
        }

        private void RefreshDisplay()
        {
            if (target == null) return;
            var spawner = (PrefabSpawner)target;
            var prefab = AddressableKeyLookup.GetPrefab(spawner.prefabName);
            if (_prefabField.value != prefab)
            {
                _prefabField.SetValueWithoutNotify(prefab);
            }
            UpdateKeyLabel(spawner.prefabName, prefab);
            UpdateAddressableWarning(_prefabField.value as GameObject);
            if (_rawKeyField != null && _rawKeyField.value != (spawner.prefabName ?? string.Empty))
            {
                _rawKeyField.SetValueWithoutNotify(spawner.prefabName ?? string.Empty);
            }
            UpdateRawKeyStatus(spawner.prefabName, prefab);

            // One-time field population: when the inspector first opens, derive lat/lon/alt from
            // the transform. After that, the fields are the source of truth and the polled refresh
            // never writes them back. The move handle and Pick on planet write to the fields
            // directly, then re-derive the transform.
            if (!_fieldsPrimed)
            {
                var (lat, lon, alt, hasTerrain) = ComputeLatLonAlt(spawner);
                _latField.SetValueWithoutNotify((float)lat);
                _lonField.SetValueWithoutNotify((float)lon);
                if (hasTerrain)
                {
                    _altField.SetValueWithoutNotify((float)alt);
                }
                _altField.SetEnabled(hasTerrain);
                _fieldsPrimed = hasTerrain;
            }
        }

        private void OnPrefabChanged(ChangeEvent<Object> evt)
        {
            var spawner = (PrefabSpawner)target;
            var prefab = evt.newValue as GameObject;
            Undo.RecordObject(spawner, "Set PrefabSpawner prefab");
            if (prefab == null)
            {
                spawner.prefabName = string.Empty;
            }
            else
            {
                var key = AddressableKeyLookup.GetKey(prefab);
                spawner.prefabName = string.IsNullOrEmpty(key) ? string.Empty : key;
            }
            EditorUtility.SetDirty(spawner);
            SurfacePrefabPreviewSync.Refresh();
            RefreshDisplay();
        }

        private void OnMakeAddressableClicked()
        {
            var prefab = _prefabField.value as GameObject;
            if (prefab == null) return;
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                EditorUtility.DisplayDialog(
                    "Make addressable",
                    "No AddressableAssetSettings configured. Open Window > Asset Management > Addressables > Groups and initialize the project first.",
                    "OK");
                return;
            }
            var path = AssetDatabase.GetAssetPath(prefab);
            var guid = AssetDatabase.AssetPathToGUID(path);
            if (string.IsNullOrEmpty(guid)) return;
            var group = PlanetAuthoringAddressables.ResolveCelestialBodiesGroup(prefab) ?? settings.DefaultGroup;
            if (group == null)
            {
                EditorUtility.DisplayDialog(
                    "Make addressable",
                    "No default addressables group exists. Create the celestial-bodies group or set a default group first.",
                    "OK");
                return;
            }
            settings.CreateOrMoveEntry(guid, group);
            AddressableKeyLookup.InvalidateCaches();
            var spawner = (PrefabSpawner)target;
            Undo.RecordObject(spawner, "Make PrefabSpawner prefab addressable");
            spawner.prefabName = AddressableKeyLookup.GetKey(prefab);
            EditorUtility.SetDirty(spawner);
            SurfacePrefabPreviewSync.Refresh();
            RefreshDisplay();
        }

        private void ApplyLatLonAltFromFields()
        {
            var spawner = (PrefabSpawner)target;
            if (spawner == null) return;
            if (!TryGetBody(spawner, out var pqs, out var bodyTransform, out _)) return;
            Undo.RecordObject(spawner.transform, "Edit PrefabSpawner lat/lon");
            ApplyLatLonAltToTransform(spawner.transform, pqs, bodyTransform, _latField.value, _lonField.value, _altField.value);
            EditorUtility.SetDirty(spawner);
        }

        private void OnPickClicked()
        {
            var spawner = (PrefabSpawner)target;
            if (spawner == null) return;
            PlanetSurfacePickTool.Begin(latLon =>
            {
                if (!TryGetBody(spawner, out var pqs, out var bodyTransform, out _)) return;
                Undo.RecordObject(spawner.transform, "Pick PrefabSpawner location");
                // Pick snaps to surface (altitude 0). Preserves the ergonomic that the place tool
                // and Pick on planet behave identically.
                _latField.SetValueWithoutNotify(latLon.x);
                _lonField.SetValueWithoutNotify(latLon.y);
                _altField.SetValueWithoutNotify(0f);
                ApplyLatLonAltToTransform(spawner.transform, pqs, bodyTransform, latLon.x, latLon.y, 0f);
                EditorUtility.SetDirty(spawner);
            });
        }

        private void OnCopyClicked()
        {
            EditorGUIUtility.systemCopyBuffer =
                $"{_latField.value.ToString("0.000000", CultureInfo.InvariantCulture)},{_lonField.value.ToString("0.000000", CultureInfo.InvariantCulture)}";
        }

        private void OnPasteClicked()
        {
            var clip = EditorGUIUtility.systemCopyBuffer;
            if (string.IsNullOrWhiteSpace(clip)) return;
            var parts = clip.Split(',');
            if (parts.Length != 2) return;
            if (!float.TryParse(parts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var lat)) return;
            if (!float.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var lon)) return;
            _latField.value = lat;
            _lonField.value = lon;
        }

        private void OnFrameAboveClicked()
        {
            var spawner = (PrefabSpawner)target;
            var pqs = spawner.GetComponentInParent<PQS>();
            if (pqs == null) return;
            SceneViewFraming.FrameAtLatLon(pqs, _latField.value, _lonField.value);
        }

        private void OnFrameSurfaceClicked()
        {
            var spawner = (PrefabSpawner)target;
            var pqs = spawner.GetComponentInParent<PQS>();
            if (pqs == null) return;
            SceneViewFraming.FrameAtLatLonAndAltitude(pqs, _latField.value, _lonField.value, SurfaceFramingPrefs.AltitudeMeters, SceneFramingMode.Surface);
        }

        private void OnRawKeyToggleChanged(ChangeEvent<bool> evt)
        {
            var spawner = (PrefabSpawner)target;
            Undo.RecordObject(spawner, "Toggle PrefabSpawner authoring mode");
            spawner.UseRawAddressableKeyAuthoring = evt.newValue;
            EditorUtility.SetDirty(spawner);
            ApplyAuthoringMode(evt.newValue);
        }

        private void ApplyAuthoringMode(bool rawKey)
        {
            if (_prefabModeGroup != null)
            {
                _prefabModeGroup.style.display = rawKey ? DisplayStyle.None : DisplayStyle.Flex;
            }
            if (_rawKeyModeGroup != null)
            {
                _rawKeyModeGroup.style.display = rawKey ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        private void OnRawKeyFieldChanged(ChangeEvent<string> evt)
        {
            var spawner = (PrefabSpawner)target;
            Undo.RecordObject(spawner, "Set PrefabSpawner addressable key");
            spawner.prefabName = evt.newValue ?? string.Empty;
            EditorUtility.SetDirty(spawner);
            SurfacePrefabPreviewSync.Refresh();
            RefreshDisplay();
        }

        private void UpdateRawKeyStatus(string rawKey, GameObject prefab)
        {
            if (_rawKeyStatus == null) return;
            if (string.IsNullOrEmpty(rawKey))
            {
                _rawKeyStatus.text = "Key is empty. Nothing will load at runtime.";
                return;
            }
            _rawKeyStatus.text = prefab != null
                ? $"Resolves to '{prefab.name}' in this project."
                : "Key does not resolve to a prefab in this project. The runtime will still try to load it from addressables.";
        }

        private void UpdateKeyLabel(string rawKey, GameObject prefab)
        {
            if (string.IsNullOrEmpty(rawKey))
            {
                _keyLabel.text = "Addressable key: (none)";
                return;
            }
            _keyLabel.text = prefab == null
                ? $"Addressable key: {rawKey}  (unresolved)"
                : $"Addressable key: {rawKey}";
        }

        private void UpdateAddressableWarning(GameObject prefab)
        {
            var key = AddressableKeyLookup.GetKey(prefab);
            var notAddressable = prefab != null && string.IsNullOrEmpty(key);
            if (notAddressable)
            {
                _addressableWarning.text = $"'{prefab.name}' is not in any addressables group. PrefabSpawner needs an addressable key to load at runtime.";
                _addressableWarning.style.display = DisplayStyle.Flex;
                _makeAddressableButton.style.display = DisplayStyle.Flex;
            }
            else
            {
                _addressableWarning.style.display = DisplayStyle.None;
                _makeAddressableButton.style.display = DisplayStyle.None;
            }
        }

        private static (double lat, double lon, double alt, bool hasTerrain) ComputeLatLonAlt(PrefabSpawner spawner)
        {
            var pqs = spawner.GetComponentInParent<PQS>();
            if (pqs == null) return (0, 0, 0, false);
            var bodyTransform = BodyResolver.FindBody(spawner)?.transform ?? pqs.transform;
            Vector3d p = bodyTransform.InverseTransformPoint(spawner.transform.position);
            var r = System.Math.Sqrt(p.x * p.x + p.y * p.y + p.z * p.z);
            if (r < 1e-3) return (0, 0, 0, false);
            var lat = System.Math.Asin(p.y / r) * 180.0 / System.Math.PI;
            var lon = System.Math.Atan2(p.z, p.x) * 180.0 / System.Math.PI;
            var localDir = LatLon.GetRelSurfaceNVector(lat, lon);
            var hasTerrain = TrySurfaceHeight(pqs, localDir, out var terrainHeight);
            return (lat, lon, hasTerrain ? r - terrainHeight : 0.0, hasTerrain);
        }

        /// <summary>
        /// Calls <see cref="PQS.GetSurfaceHeight" /> guarded by the runtime's renderer + decal
        /// controller readiness. Falls back to body radius when the PQS isn't fully alive (no
        /// preview session active), so the inspector renders without throwing.
        /// </summary>
        private static bool TrySurfaceHeight(PQS pqs, Vector3 localDir, out double height)
        {
            height = 0.0;
            if (pqs.PQSRenderer == null || pqs.PQSRenderer.PqsDecalController == null
                || pqs.PQSRenderer.PqsDecalController.PqsDecalData == null)
            {
                return false;
            }
            // Try/catch covers the domain-reload window where PQS native buffers are disposed but
            // the managed PQS reference is still live. The polled refresh would otherwise spam.
            try
            {
                height = pqs.GetSurfaceHeight(localDir, includeDecals: true);
                return true;
            }
            catch (System.ObjectDisposedException)
            {
                return false;
            }
        }

        private static bool TryGetBody(PrefabSpawner spawner, out PQS pqs, out Transform bodyTransform, out float radius)
        {
            bodyTransform = null;
            radius = 0;
            pqs = spawner != null ? spawner.GetComponentInParent<PQS>() : null;
            if (pqs == null) return false;
            bodyTransform = BodyResolver.FindBody(spawner)?.transform ?? pqs.transform;
            radius = (float)(pqs.CoreCelestialBodyData?.Data?.radius ?? 0.0);
            return radius > 0;
        }

        private static void ApplyLatLonAltToTransform(Transform t, PQS pqs, Transform bodyTransform, double lat, double lon, double altitude)
        {
            Vector3 localDir = LatLon.GetRelSurfaceNVector(lat, lon);
            // Snap to terrain (decals included) so altitude=0 sits on the rendered surface, matching
            // where the place tool drops fresh spawners. Altitude offsets along local-up from there.
            // Bail when terrain isn't sampleable rather than writing a position derived from body
            // radius, which would silently teleport the spawner to sea level.
            if (!TrySurfaceHeight(pqs, localDir, out var terrainHeight)) return;
            var radius = terrainHeight + altitude;
            t.position = bodyTransform.position + bodyTransform.rotation * (localDir * (float)radius);
        }
    }
}
