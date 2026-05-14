using System;
using System.Collections.Generic;
using System.Text;
using KSP;
using KSP.Game.Science;
using KSP.Rendering.Planets;
using Ksp2UnityTools.Editor.PlanetAuthoring.Science;
using Ksp2UnityTools.Editor.PlanetAuthoring.Tools;
using Ksp2UnityTools.Editor.ScriptableObjects;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Windows
{
    /// <summary>
    /// Combined manager window for every surface-attached authoring object on the active body:
    /// decals, standalone prefab spawners, standalone discoverables, and surface landmarks.
    /// </summary>
    /// <remarks>
    /// Two creation paths only: New Decal (opens the existing decal prompt) and New Surface
    /// Landmark (opens the existing landmark prompt). Standalone prefabs and discoverables aren't
    /// directly creatable from here because the surface-landmark flow supersedes them. Every row
    /// gets a Surface and Above framing button plus a Delete. The above-frame altitude scales with
    /// the item's radius so the camera lands at a useful inspection distance, matching how the
    /// Discoverable Manager already framed.
    /// </remarks>
    public class SurfaceManagerWindow : EditorWindow
    {
        private const string UxmlPath = "/Assets/Windows/PlanetAuthoring/Windows/SurfaceManagerWindow.uxml";

        private Label _statusLabel;
        private Button _newLandmarkButton;
        private Button _bakeDecalsButton;
        private TextField _filterField;
        private Foldout _decalsFold;
        private Foldout _prefabsFold;
        private Foldout _discoverablesFold;
        private Foldout _landmarksFold;
        private VisualElement _decalsList;
        private VisualElement _prefabsList;
        private VisualElement _discoverablesList;
        private VisualElement _landmarksList;
        private string _lastFingerprint;

        /// <summary>Opens the Surface Manager window.</summary>
        public static void ShowWindow()
        {
            var window = GetWindow<SurfaceManagerWindow>();
            window.titleContent = new GUIContent("Surface Manager");
            window.minSize = new Vector2(380f, 360f);
        }

        /// <inheritdoc />
        private void CreateGUI()
        {
            var root = rootVisualElement;
            root.style.paddingLeft = 8f;
            root.style.paddingRight = 8f;
            root.style.paddingTop = 8f;
            root.style.paddingBottom = 8f;

            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(SDKConfiguration.BasePath + UxmlPath);
            if (tree == null)
            {
                root.Add(new Label("Failed to load SurfaceManagerWindow.uxml"));
                return;
            }
            tree.CloneTree(root);

            Ksp2UnityToolsStyles.Apply(root);

            _statusLabel = root.Q<Label>("status-label");
            _newLandmarkButton = root.Q<Button>("new-landmark-button");
            _bakeDecalsButton = root.Q<Button>("bake-decals-button");
            _filterField = root.Q<TextField>("filter-field");
            _decalsFold = root.Q<Foldout>("decals-fold");
            _prefabsFold = root.Q<Foldout>("prefabs-fold");
            _discoverablesFold = root.Q<Foldout>("discoverables-fold");
            _landmarksFold = root.Q<Foldout>("landmarks-fold");
            _decalsList = root.Q<VisualElement>("decals-list");
            _prefabsList = root.Q<VisualElement>("prefabs-list");
            _discoverablesList = root.Q<VisualElement>("discoverables-list");
            _landmarksList = root.Q<VisualElement>("landmarks-list");

            _newLandmarkButton.clicked += OnNewLandmarkClicked;
            _bakeDecalsButton.clicked += OnBakeDecalsClicked;
            _filterField.RegisterValueChangedCallback(_ => Refresh(force: true));

            root.schedule.Execute(() => Refresh(force: false)).Every(500);
            Refresh(force: true);
        }

        private void Refresh(bool force)
        {
            if (_statusLabel == null) return;
            var pqs = ResolvePqs(out var bodyName, out var statusHint);
            if (pqs == null)
            {
                _statusLabel.text = statusHint;
                _newLandmarkButton.SetEnabled(false);
                _bakeDecalsButton.SetEnabled(false);
                _decalsList.Clear();
                _prefabsList.Clear();
                _discoverablesList.Clear();
                _landmarksList.Clear();
                SetFoldoutCount(_decalsFold, "Decals", 0);
                SetFoldoutCount(_prefabsFold, "Surface Prefabs", 0);
                SetFoldoutCount(_discoverablesFold, "Discoverables", 0);
                SetFoldoutCount(_landmarksFold, "Surface Landmarks", 0);
                _lastFingerprint = null;
                return;
            }
            _newLandmarkButton.SetEnabled(true);
            _bakeDecalsButton.SetEnabled(true);

            var landmarks = pqs.GetComponentsInChildren<SurfaceLandmark>(true);
            var allSpawners = pqs.GetComponentsInChildren<PrefabSpawner>(true);
            var landmarkSpawnerIds = new HashSet<int>();
            var landmarkRegionIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var landmark in landmarks)
            {
                if (landmark == null) continue;
                if (landmark.ManagedSpawner != null)
                {
                    landmarkSpawnerIds.Add(landmark.ManagedSpawner.GetInstanceID());
                }
                if (!string.IsNullOrEmpty(landmark.DiscoverableRegionId))
                {
                    landmarkRegionIds.Add(landmark.DiscoverableRegionId);
                }
            }
            var standaloneSpawners = new List<PrefabSpawner>();
            foreach (var spawner in allSpawners)
            {
                if (spawner == null) continue;
                if (landmarkSpawnerIds.Contains(spawner.GetInstanceID())) continue;
                standaloneSpawners.Add(spawner);
            }

            var controller = DecalControllerHelper.Resolve(pqs);
            var decals = controller?.PqsDecalInstanceList;
            var data = ResolveScienceRegionData(bodyName);
            var allDiscoverables = data?.discoverables;
            var standaloneDiscoverables = new List<CelestialBodyDiscoverablePosition>();
            if (allDiscoverables != null)
            {
                foreach (var d in allDiscoverables)
                {
                    if (d == null) continue;
                    if (landmarkRegionIds.Contains(d.ScienceRegionId)) continue;
                    standaloneDiscoverables.Add(d);
                }
            }

            var filter = _filterField.value ?? string.Empty;
            var fingerprint = ComputeFingerprint(decals, standaloneSpawners, standaloneDiscoverables, landmarks, filter);
            if (!force && string.Equals(fingerprint, _lastFingerprint)) return;
            _lastFingerprint = fingerprint;

            _statusLabel.text = $"Surface objects on {bodyName}";
            RebuildDecals(pqs, decals, filter);
            RebuildSpawners(pqs, standaloneSpawners, filter);
            RebuildDiscoverables(pqs, data, standaloneDiscoverables, filter);
            RebuildLandmarks(pqs, landmarks, filter);
        }

        // -- Decals --------------------------------------------------------------

        private void RebuildDecals(PQS pqs, List<PQSDecalInstance> decals, string filter)
        {
            _decalsList.Clear();
            var visible = 0;
            if (decals != null)
            {
                foreach (var inst in decals)
                {
                    if (inst == null) continue;
                    var name = inst.gameObject.name ?? string.Empty;
                    if (!MatchesFilter(name, filter)) continue;
                    visible++;
                    _decalsList.Add(BuildRow(name,
                        $"({inst.LatLong.x:0.00}°, {inst.LatLong.y:0.00}°)",
                        () => FrameAt(pqs, inst.LatLong.x, inst.LatLong.y, SceneFramingMode.Surface, SurfaceFramingPrefs.AltitudeMeters),
                        () => FrameAt(pqs, inst.LatLong.x, inst.LatLong.y, SceneFramingMode.Side, Math.Max(100.0, inst.Scale * 0.5 * 3.0)),
                        () => DeleteDecal(inst),
                        () => SelectGameObject(inst.gameObject)));
                }
            }
            SetFoldoutCount(_decalsFold, "Decals", visible);
            EnsureEmpty(_decalsList, visible, "No decals yet. Click '+ New Decal' to add one.");
        }

        private void DeleteDecal(PQSDecalInstance inst)
        {
            if (inst == null) return;
            if (!EditorUtility.DisplayDialog("Delete decal",
                $"Delete decal '{inst.gameObject.name}'?",
                "Delete", "Cancel")) return;
            Undo.DestroyObjectImmediate(inst.gameObject);
            Refresh(force: true);
        }

        // -- Surface prefabs -----------------------------------------------------

        private void RebuildSpawners(PQS pqs, List<PrefabSpawner> spawners, string filter)
        {
            _prefabsList.Clear();
            var bodyTransform = BodyResolver.FindBody(pqs)?.transform ?? pqs.transform;
            var visible = 0;
            foreach (var spawner in spawners)
            {
                var name = spawner.gameObject.name ?? string.Empty;
                if (!MatchesFilter(name, filter)) continue;
                visible++;
                var (lat, lon) = LatLonFromTransform(spawner.transform, bodyTransform);
                _prefabsList.Add(BuildRow(name,
                    $"({lat:0.00}°, {lon:0.00}°)",
                    () => FrameAt(pqs, lat, lon, SceneFramingMode.Surface, SurfaceFramingPrefs.AltitudeMeters),
                    () => FrameAt(pqs, lat, lon, SceneFramingMode.Side, 300.0),
                    () => DeleteSpawner(spawner),
                    () => SelectGameObject(spawner.gameObject)));
            }
            SetFoldoutCount(_prefabsFold, "Surface Prefabs", visible);
            EnsureEmpty(_prefabsList, visible, "No standalone prefabs. Use Surface Landmark instead for new placements.");
        }

        private void DeleteSpawner(PrefabSpawner spawner)
        {
            if (spawner == null) return;
            if (!EditorUtility.DisplayDialog("Delete prefab spawner",
                $"Delete spawner '{spawner.gameObject.name}'?",
                "Delete", "Cancel")) return;
            Undo.DestroyObjectImmediate(spawner.gameObject);
            Refresh(force: true);
        }

        // -- Discoverables -------------------------------------------------------

        private void RebuildDiscoverables(PQS pqs, ScienceRegionData data, List<CelestialBodyDiscoverablePosition> discoverables, string filter)
        {
            _discoverablesList.Clear();
            var visible = 0;
            foreach (var d in discoverables)
            {
                var name = string.IsNullOrEmpty(d.ScienceRegionId) ? "(unset)" : d.ScienceRegionId;
                if (!MatchesFilter(name, filter)) continue;
                visible++;
                var (lat, lon) = LatLonFromBodyLocal(d.Position);
                var capturedD = d;
                _discoverablesList.Add(BuildRow(
                    $"{name}  r {d.Radius:0}m",
                    $"({lat:0.00}°, {lon:0.00}°)",
                    () => FrameAt(pqs, lat, lon, SceneFramingMode.Surface, SurfaceFramingPrefs.AltitudeMeters),
                    () => FrameAt(pqs, lat, lon, SceneFramingMode.Side, Math.Max(100.0, capturedD.Radius * 3.0)),
                    () => DeleteDiscoverable(data, capturedD),
                    null));
            }
            SetFoldoutCount(_discoverablesFold, "Discoverables", visible);
            EnsureEmpty(_discoverablesList, visible, "No standalone discoverables. Use Surface Landmark for new placements.");
        }

        private void DeleteDiscoverable(ScienceRegionData data, CelestialBodyDiscoverablePosition d)
        {
            if (data == null || d == null) return;
            var label = string.IsNullOrEmpty(d.ScienceRegionId) ? "(unset)" : d.ScienceRegionId;
            if (!EditorUtility.DisplayDialog("Delete discoverable",
                $"Delete discoverable '{label}'?",
                "Delete", "Cancel")) return;
            Undo.RecordObject(data, "Delete discoverable");
            data.discoverables.Remove(d);
            EditorUtility.SetDirty(data);
            Refresh(force: true);
        }

        // -- Surface landmarks ---------------------------------------------------

        private void RebuildLandmarks(PQS pqs, SurfaceLandmark[] landmarks, string filter)
        {
            _landmarksList.Clear();
            var bodyTransform = BodyResolver.FindBody(pqs)?.transform ?? pqs.transform;
            var visible = 0;
            foreach (var landmark in landmarks)
            {
                if (landmark == null) continue;
                var name = landmark.gameObject.name ?? string.Empty;
                if (!MatchesFilter(name, filter)) continue;
                visible++;
                double lat = landmark.Latitude;
                double lon = landmark.Longitude;
                var glyphs = $"{(landmark.EnableDecal ? "D" : "-")}{(landmark.EnablePrefab ? "P" : "-")}{(landmark.EnableDiscoverable ? "X" : "-")}";
                _landmarksList.Add(BuildRow(
                    $"{name}  [{glyphs}]",
                    $"({lat:0.00}°, {lon:0.00}°)",
                    () => FrameAt(pqs, lat, lon, SceneFramingMode.Surface, SurfaceFramingPrefs.AltitudeMeters),
                    () => FrameAt(pqs, lat, lon, SceneFramingMode.Side, Math.Max(100.0, landmark.SmoothingRadius * 3.0)),
                    () => DeleteLandmark(landmark),
                    () => SelectGameObject(landmark.gameObject)));
            }
            SetFoldoutCount(_landmarksFold, "Surface Landmarks", visible);
            EnsureEmpty(_landmarksList, visible, "No surface landmarks. Click '+ New Surface Landmark' to add one.");
        }

        private void DeleteLandmark(SurfaceLandmark landmark)
        {
            if (landmark == null) return;
            if (!EditorUtility.DisplayDialog("Delete surface landmark",
                $"Delete landmark '{landmark.gameObject.name}'? Its decal and prefab children go with it. The discoverable entry stays in the ScienceRegionData.",
                "Delete", "Cancel")) return;
            Undo.DestroyObjectImmediate(landmark.gameObject);
            Refresh(force: true);
        }

        // -- Row + helpers -------------------------------------------------------

        private static VisualElement BuildRow(string title, string coords, Action onSurface, Action onAbove, Action onDelete, Action onSelect)
        {
            var row = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    paddingTop = 2f, paddingBottom = 2f, paddingLeft = 4f, paddingRight = 4f,
                    marginBottom = 1f,
                    backgroundColor = new Color(0.16f, 0.18f, 0.22f, 0.45f),
                    borderTopLeftRadius = 2f, borderTopRightRadius = 2f,
                    borderBottomLeftRadius = 2f, borderBottomRightRadius = 2f,
                },
            };
            var titleLabel = new Label(title)
            {
                style =
                {
                    flexGrow = 1f, minWidth = 80f,
                    unityFontStyleAndWeight = FontStyle.Bold,
                },
            };
            row.Add(titleLabel);
            row.Add(new Label(coords)
            {
                style = { width = 140f, color = new Color(0.7f, 0.72f, 0.78f) },
            });
            if (onSelect != null)
            {
                row.Add(new Button(onSelect)
                {
                    text = "Sel",
                    style = { width = 32f, marginRight = 2f },
                    tooltip = "Select this object in the scene hierarchy.",
                });
            }
            row.Add(new Button(onSurface)
            {
                text = "S",
                style = { width = 24f, marginRight = 2f },
                tooltip = "Look from surface: frame at the configured surface altitude.",
            });
            row.Add(new Button(onAbove)
            {
                text = "A",
                style = { width = 24f, marginRight = 4f },
                tooltip = "Look from above: frame at an altitude scaled to this item's radius.",
            });
            row.Add(new Button(onDelete)
            {
                text = "X",
                style = { width = 22f },
                tooltip = "Delete this item.",
            });
            return row;
        }

        private static void EnsureEmpty(VisualElement list, int visible, string emptyText)
        {
            if (visible > 0) return;
            list.Add(new Label(emptyText)
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Italic,
                    color = new Color(0.65f, 0.66f, 0.7f),
                    marginTop = 4f, marginBottom = 4f, whiteSpace = WhiteSpace.Normal,
                },
            });
        }

        private static void SetFoldoutCount(Foldout fold, string baseText, int count)
        {
            if (fold == null) return;
            fold.text = $"{baseText} ({count})";
        }

        private static bool MatchesFilter(string name, string filter)
        {
            if (string.IsNullOrEmpty(filter)) return true;
            return name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void FrameAt(PQS pqs, double lat, double lon, SceneFramingMode mode, double altitude)
        {
            SceneViewFraming.FrameAtLatLonAndAltitude(pqs, lat, lon, altitude, mode);
        }

        private static void SelectGameObject(GameObject go)
        {
            Selection.activeGameObject = go;
            EditorGUIUtility.PingObject(go);
        }

        private static (double lat, double lon) LatLonFromTransform(Transform t, Transform bodyTransform)
        {
            Vector3d p = bodyTransform.InverseTransformPoint(t.position);
            return LatLonFromBodyLocal(p);
        }

        private static (double lat, double lon) LatLonFromBodyLocal(Vector3 local)
        {
            Vector3d p = local;
            return LatLonFromBodyLocal(p);
        }

        private static (double lat, double lon) LatLonFromBodyLocal(Vector3d p)
        {
            var r = Math.Sqrt(p.x * p.x + p.y * p.y + p.z * p.z);
            if (r < 1e-3) return (0, 0);
            var lat = Math.Asin(p.y / r) * 180.0 / Math.PI;
            var lon = Math.Atan2(p.z, p.x) * 180.0 / Math.PI;
            return (lat, lon);
        }

        // -- Resolution helpers --------------------------------------------------

        private static PQS ResolvePqs(out string bodyName, out string statusHint)
        {
            var session = PlanetAuthoringSession.Active;
            if (session == null || !session.IsAlive)
            {
                bodyName = null;
                statusHint = "No active preview. Enable a planet preview to manage its surface objects.";
                return null;
            }
            if (session.Pqs == null)
            {
                bodyName = null;
                statusHint = "Active session has no PQS bound.";
                return null;
            }
            var body = BodyResolver.FindBody(session.Pqs);
            bodyName = body?.Data?.bodyName ?? session.Body?.name ?? "(unknown)";
            statusHint = null;
            return session.Pqs;
        }

        private static ScienceRegionData ResolveScienceRegionData(string bodyName)
        {
            return string.IsNullOrEmpty(bodyName) ? null : ScienceRegionAssetLocator.FindForBody(bodyName);
        }

        // -- Actions -------------------------------------------------------------

        private void OnNewLandmarkClicked()
        {
            NewSurfaceLandmarkPromptWindow.Show(settings =>
            {
                PlaceSurfaceLandmarkTool.Begin(settings);
            });
        }

        private void OnBakeDecalsClicked()
        {
            var pqs = PlanetAuthoringSession.Active?.Pqs;
            var controller = DecalControllerHelper.Resolve(pqs);
            if (controller == null) return;
            DecalBaker.QueueRebuild(controller);
        }

        // -- Fingerprint ---------------------------------------------------------

        private static string ComputeFingerprint(
            List<PQSDecalInstance> decals,
            List<PrefabSpawner> spawners,
            List<CelestialBodyDiscoverablePosition> discoverables,
            SurfaceLandmark[] landmarks,
            string filter)
        {
            var sb = new StringBuilder(256);
            sb.Append("f=").Append(filter ?? string.Empty).Append('|');
            if (decals != null)
            {
                foreach (var d in decals)
                {
                    if (d == null) { sb.Append('|'); continue; }
                    sb.Append(d.GetInstanceID()).Append(',').Append(d.LatLong.x.ToString("0.0000")).Append(',').Append(d.LatLong.y.ToString("0.0000")).Append('|');
                }
            }
            sb.Append(";");
            foreach (var s in spawners)
            {
                if (s == null) continue;
                sb.Append(s.GetInstanceID()).Append(',').Append(s.transform.position.x.ToString("0.0")).Append(',').Append(s.transform.position.y.ToString("0.0")).Append(',').Append(s.transform.position.z.ToString("0.0")).Append('|');
            }
            sb.Append(";");
            foreach (var d in discoverables)
            {
                if (d == null) continue;
                sb.Append(d.ScienceRegionId ?? string.Empty).Append(',').Append(d.Radius.ToString("0.0")).Append(',').Append(d.Position.x.ToString("0.0")).Append(',').Append(d.Position.y.ToString("0.0")).Append(',').Append(d.Position.z.ToString("0.0")).Append('|');
            }
            sb.Append(";");
            foreach (var l in landmarks)
            {
                if (l == null) continue;
                sb.Append(l.GetInstanceID()).Append(',').Append(l.SmoothingRadius.ToString("0.0")).Append(',').Append(l.EnableDecal ? '1' : '0').Append(l.EnablePrefab ? '1' : '0').Append(l.EnableDiscoverable ? '1' : '0').Append('|');
            }
            return sb.ToString();
        }
    }
}
