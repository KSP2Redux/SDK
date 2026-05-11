using System;
using System.Collections.Generic;
using KSP;
using KSP.Game.Science;
using KSP.Rendering.Planets;
using Ksp2UnityTools.Editor.PlanetAuthoring.Science;
using Ksp2UnityTools.Editor.PlanetAuthoring.Tools;
using Ksp2UnityTools.Editor.ScriptableObjects;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Windows
{
    /// <summary>
    /// Bulk-list authoring window for <see cref="CelestialBodyDiscoverablePosition" /> entries on
    /// the active planet preview session's bound <see cref="ScienceRegionData" />.
    /// </summary>
    /// <remarks>
    /// Resolves the asset for the active body via <see cref="ScienceRegionAssetLocator" />. Lists
    /// every discoverable with filter / region-dropdown / sort, a Go button per row that frames the
    /// scene camera at the discoverable's lat/lon, and a Delete button per row. The "+ Place new"
    /// button activates <see cref="PlaceDiscoverableTool" /> against the bound asset.
    /// </remarks>
    public class DiscoverableManagerWindow : EditorWindow
    {
        private const string RegionFilterAll = "All regions";
        private const string SortName = "Region Id";
        private const string SortCoords = "Latitude";
        private const string SortRadius = "Radius";

        private Label _statusLabel;
        private TextField _filterField;
        private DropdownField _regionFilter;
        private DropdownField _sortField;
        private Button _placeNewButton;
        private ScrollView _list;
        private string _lastFingerprint;

        /// <summary>
        /// Opens the Discoverable Manager window.
        /// </summary>
        public static void ShowWindow()
        {
            var window = GetWindow<DiscoverableManagerWindow>();
            window.titleContent = new GUIContent("Discoverable Manager");
            window.minSize = new Vector2(360f, 280f);
        }

        private void CreateGUI()
        {
            var root = rootVisualElement;
            root.style.paddingLeft = 8f;
            root.style.paddingRight = 8f;
            root.style.paddingTop = 8f;
            root.style.paddingBottom = 8f;

            _statusLabel = new Label { style = { whiteSpace = WhiteSpace.Normal, marginBottom = 6f } };
            root.Add(_statusLabel);

            var filterRow = new VisualElement { style = { flexDirection = FlexDirection.Row, marginBottom = 4f } };
            _filterField = new TextField("Filter") { style = { flexGrow = 1f, marginRight = 6f } };
            _filterField.tooltip = "Substring match against the discoverable's region Id (case-insensitive).";
            _filterField.RegisterValueChangedCallback(_ => Refresh(force: true));
            filterRow.Add(_filterField);
            _regionFilter = new DropdownField("Region") { style = { width = 200f } };
            _regionFilter.tooltip = "Show only discoverables whose ScienceRegionId matches the selected region.";
            _regionFilter.choices = new List<string> { RegionFilterAll };
            _regionFilter.SetValueWithoutNotify(RegionFilterAll);
            _regionFilter.RegisterValueChangedCallback(_ => Refresh(force: true));
            filterRow.Add(_regionFilter);
            root.Add(filterRow);

            var sortRow = new VisualElement { style = { flexDirection = FlexDirection.Row, marginBottom = 6f } };
            _sortField = new DropdownField("Sort by") { style = { width = 220f } };
            _sortField.tooltip = "Row order. Region Id is the default; Latitude groups by hemisphere; Radius surfaces large discoverables first.";
            _sortField.choices = new List<string> { SortName, SortCoords, SortRadius };
            _sortField.SetValueWithoutNotify(SortName);
            _sortField.RegisterValueChangedCallback(_ => Refresh(force: true));
            sortRow.Add(_sortField);
            sortRow.Add(new VisualElement { style = { flexGrow = 1f } });
            _placeNewButton = new Button(OnPlaceNewClicked) { text = "+ Place new", style = { height = 22f, width = 120f } };
            _placeNewButton.tooltip = "Activate the Place Discoverable scene tool against the bound Science Region asset. Click on the planet to drop a discoverable.";
            sortRow.Add(_placeNewButton);
            root.Add(sortRow);

            _list = new ScrollView(ScrollViewMode.Vertical) { style = { flexGrow = 1f } };
            root.Add(_list);

            root.schedule.Execute(() => Refresh(force: false)).Every(500);
            Refresh(force: true);
        }

        private void Refresh(bool force)
        {
            if (_statusLabel == null) return;

            var data = ResolveData(out var bodyName, out var statusHint);
            if (data == null)
            {
                _statusLabel.text = statusHint;
                _placeNewButton.SetEnabled(false);
                _list.Clear();
                _regionFilter.choices = new List<string> { RegionFilterAll };
                _regionFilter.SetValueWithoutNotify(RegionFilterAll);
                _lastFingerprint = null;
                return;
            }

            var count = data.discoverables?.Count ?? 0;
            _statusLabel.text = count == 0
                ? $"No discoverables on {bodyName}. Click '+ Place new' and drop one on the planet."
                : $"{count} discoverable{(count == 1 ? string.Empty : "s")} on {bodyName}.";
            _placeNewButton.SetEnabled(true);

            // Refresh the region-filter dropdown choices to track the asset's region table.
            var regionChoices = BuildRegionChoices(data);
            if (!ListsEqual(regionChoices, _regionFilter.choices))
            {
                var keep = _regionFilter.value;
                _regionFilter.choices = regionChoices;
                _regionFilter.SetValueWithoutNotify(regionChoices.Contains(keep) ? keep : RegionFilterAll);
            }

            var filter = _filterField.value ?? string.Empty;
            var region = _regionFilter.value ?? RegionFilterAll;
            var sort = _sortField.value ?? SortName;

            var rows = BuildRows(data, filter, region, sort);
            var fingerprint = ComputeFingerprint(rows);
            if (!force && string.Equals(fingerprint, _lastFingerprint)) return;
            _lastFingerprint = fingerprint;

            _list.Clear();
            if (rows.Count == 0)
            {
                var emptyLabel = new Label("No discoverables match the current filter.");
                emptyLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
                emptyLabel.style.color = new Color(0.65f, 0.66f, 0.7f);
                emptyLabel.style.marginTop = 6f;
                _list.Add(emptyLabel);
                return;
            }
            foreach (var (sourceIndex, d) in rows)
            {
                _list.Add(BuildRow(data, sourceIndex, d));
            }
        }

        private static List<string> BuildRegionChoices(ScienceRegionData data)
        {
            var choices = new List<string> { RegionFilterAll };
            if (data.information?.ScienceRegionDefinitions != null)
            {
                foreach (var def in data.information.ScienceRegionDefinitions)
                {
                    if (def == null || string.IsNullOrEmpty(def.Id)) continue;
                    if (!choices.Contains(def.Id))
                    {
                        choices.Add(def.Id);
                    }
                }
            }
            // Catch discoverables whose ScienceRegionId doesn't match any defined region (orphan / unmapped).
            if (data.discoverables != null)
            {
                foreach (var d in data.discoverables)
                {
                    if (d == null) continue;
                    var id = string.IsNullOrEmpty(d.ScienceRegionId) ? "(unset)" : d.ScienceRegionId;
                    if (!choices.Contains(id))
                    {
                        choices.Add(id);
                    }
                }
            }
            return choices;
        }

        private static List<(int sourceIndex, CelestialBodyDiscoverablePosition d)> BuildRows(
            ScienceRegionData data, string filter, string regionFilter, string sort)
        {
            var rows = new List<(int, CelestialBodyDiscoverablePosition)>();
            if (data.discoverables == null) return rows;
            var filterLower = filter?.ToLowerInvariant() ?? string.Empty;
            for (var i = 0; i < data.discoverables.Count; i++)
            {
                var d = data.discoverables[i];
                if (d == null) continue;
                var id = string.IsNullOrEmpty(d.ScienceRegionId) ? "(unset)" : d.ScienceRegionId;
                if (regionFilter != RegionFilterAll && id != regionFilter) continue;
                if (!string.IsNullOrEmpty(filterLower) && id.ToLowerInvariant().IndexOf(filterLower, StringComparison.Ordinal) < 0) continue;
                rows.Add((i, d));
            }
            // Stable sort against the comparator so order is deterministic.
            switch (sort)
            {
                case SortCoords:
                    rows.Sort((a, b) => ComputeLatitude(a.Item2).CompareTo(ComputeLatitude(b.Item2)));
                    break;
                case SortRadius:
                    rows.Sort((a, b) => b.Item2.Radius.CompareTo(a.Item2.Radius));
                    break;
                default:
                    rows.Sort((a, b) => string.Compare(a.Item2.ScienceRegionId ?? string.Empty, b.Item2.ScienceRegionId ?? string.Empty, StringComparison.OrdinalIgnoreCase));
                    break;
            }
            return rows;
        }

        private VisualElement BuildRow(ScienceRegionData data, int sourceIndex, CelestialBodyDiscoverablePosition d)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.paddingTop = 2f;
            row.style.paddingBottom = 2f;
            row.style.paddingLeft = 4f;
            row.style.paddingRight = 4f;
            row.style.marginBottom = 1f;
            row.style.backgroundColor = new Color(0.16f, 0.18f, 0.22f, 0.45f);
            row.style.borderTopLeftRadius = 2f;
            row.style.borderTopRightRadius = 2f;
            row.style.borderBottomLeftRadius = 2f;
            row.style.borderBottomRightRadius = 2f;

            var idLabel = new Label(string.IsNullOrEmpty(d.ScienceRegionId) ? "(unset)" : d.ScienceRegionId);
            idLabel.style.flexGrow = 1f;
            idLabel.style.minWidth = 80f;
            idLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            row.Add(idLabel);

            var coordsLabel = new Label(FormatCoords(d));
            coordsLabel.style.width = 180f;
            coordsLabel.style.color = new Color(0.7f, 0.72f, 0.78f);
            row.Add(coordsLabel);

            var radiusLabel = new Label($"r {d.Radius:0}m");
            radiusLabel.style.width = 60f;
            radiusLabel.style.color = new Color(0.7f, 0.72f, 0.78f);
            row.Add(radiusLabel);

            var captured = sourceIndex;
            var goBtn = new Button(() => FrameAtDiscoverable(d)) { text = "Go" };
            goBtn.tooltip = "Frame the SceneView camera looking down at this discoverable. Altitude scales with the discoverable's radius so the disc fills the view. For ground-level inspection, open the asset inspector and use Look from surface.";
            goBtn.style.width = 40f;
            goBtn.style.marginRight = 4f;
            row.Add(goBtn);

            var delBtn = new Button(() => DeleteAt(data, captured)) { text = "X" };
            delBtn.tooltip = "Delete this discoverable.";
            delBtn.style.width = 22f;
            row.Add(delBtn);

            return row;
        }

        private void OnPlaceNewClicked()
        {
            var data = ResolveData(out _, out _);
            if (data == null)
            {
                EditorUtility.DisplayDialog(
                    "Place Discoverable",
                    "Start a planet preview session first and ensure a Science Region asset exists for the active body.",
                    "OK");
                return;
            }
            PlaceDiscoverableTool.Begin(data);
        }

        private void DeleteAt(ScienceRegionData data, int index)
        {
            if (data?.discoverables == null || index < 0 || index >= data.discoverables.Count) return;
            var label = data.discoverables[index].ScienceRegionId ?? "(unset)";
            if (!EditorUtility.DisplayDialog(
                    "Delete discoverable",
                    $"Delete discoverable '{label}' at index {index}?",
                    "Delete", "Cancel"))
            {
                return;
            }
            Undo.RecordObject(data, "Delete discoverable");
            data.discoverables.RemoveAt(index);
            EditorUtility.SetDirty(data);
            Refresh(force: true);
        }

        private static void FrameAtDiscoverable(CelestialBodyDiscoverablePosition d)
        {
            // Surface camera scaled to the discoverable's radius. Doesn't preserve zoom; artists
            // want a close-up of the landmark, not whatever altitude they were last orbiting at.
            Vector3d p = d.Position;
            var r = Math.Sqrt(p.x * p.x + p.y * p.y + p.z * p.z);
            if (r < 1e-3) return;
            var lat = Math.Asin(p.y / r) * 180.0 / Math.PI;
            var lon = Math.Atan2(p.z, p.x) * 180.0 / Math.PI;
            var altitude = Math.Max(100.0, d.Radius * 3.0);
            SceneViewFraming.FrameAtLatLonAndAltitude(PlanetAuthoringSession.Active?.Pqs, lat, lon, altitude);
        }

        private static ScienceRegionData ResolveData(out string bodyName, out string statusHint)
        {
            var session = PlanetAuthoringSession.Active;
            if (session == null || !session.IsAlive)
            {
                bodyName = null;
                statusHint = "No active preview. Enable a planet preview to manage its discoverables.";
                return null;
            }
            var body = session.Pqs != null ? session.Pqs.GetComponentInParent<CoreCelestialBodyData>() : null;
            bodyName = body?.Data?.bodyName ?? session.Body?.name ?? "(unknown)";
            if (string.IsNullOrEmpty(bodyName))
            {
                statusHint = "Active session has no body name. Set CoreCelestialBodyData.bodyName before placing discoverables.";
                return null;
            }
            var data = ScienceRegionAssetLocator.FindForBody(bodyName);
            if (data == null)
            {
                statusHint = $"No ScienceRegionData asset matches '{bodyName}'. Create one via Assets > KSP2 Unity Tools > Planet Authoring > Science Region Data.";
                return null;
            }
            statusHint = null;
            return data;
        }

        private static double ComputeLatitude(CelestialBodyDiscoverablePosition d)
        {
            Vector3d p = d.Position;
            var r = Math.Sqrt(p.x * p.x + p.y * p.y + p.z * p.z);
            return r < 1e-3 ? 0.0 : Math.Asin(p.y / r) * 180.0 / Math.PI;
        }

        private static string FormatCoords(CelestialBodyDiscoverablePosition d)
        {
            Vector3d p = d.Position;
            var r = Math.Sqrt(p.x * p.x + p.y * p.y + p.z * p.z);
            if (r < 1e-3) return "(at body center)";
            // Convention matches LatLon.SphericalVector + SwapYAndZ used by the runtime: y is the
            // pole axis, lon = atan2(z, x).
            var lat = Math.Asin(p.y / r) * 180.0 / Math.PI;
            var lon = Math.Atan2(p.z, p.x) * 180.0 / Math.PI;
            return $"({lat:0.00}°, {lon:0.00}°)";
        }

        private static string ComputeFingerprint(List<(int sourceIndex, CelestialBodyDiscoverablePosition d)> rows)
        {
            if (rows == null || rows.Count == 0) return "empty";
            var sb = new System.Text.StringBuilder(rows.Count * 32);
            foreach (var (idx, d) in rows)
            {
                sb.Append(idx).Append('|');
                sb.Append(d.ScienceRegionId ?? "(unset)").Append('|');
                sb.Append(d.Position.x.ToString("R")).Append(',');
                sb.Append(d.Position.y.ToString("R")).Append(',');
                sb.Append(d.Position.z.ToString("R")).Append('|');
                sb.Append(d.Radius.ToString("R")).Append(';');
            }
            return sb.ToString();
        }

        private static bool ListsEqual(List<string> a, List<string> b)
        {
            if (a == null || b == null) return a == b;
            if (a.Count != b.Count) return false;
            for (var i = 0; i < a.Count; i++)
            {
                if (a[i] != b[i]) return false;
            }
            return true;
        }
    }
}
