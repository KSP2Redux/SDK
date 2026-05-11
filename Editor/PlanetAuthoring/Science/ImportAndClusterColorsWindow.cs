using System.Collections.Generic;
using Ksp2UnityTools.Editor.ScriptableObjects;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Science
{
    /// <summary>
    /// Modal-ish editor window that runs k-means color clustering on a Science Region asset's source <c>scienceRegionMap</c> and applies the discovered clusters back as region rows.
    /// </summary>
    /// <remarks>
    /// This is the primary authoring entry point on the Science Region Inspector v2.
    /// Opened via <see cref="Open" /> from the inspector's "Import &amp; cluster colors" button.
    /// The window is bound to one <see cref="ScienceRegionData" /> instance and disposes itself
    /// if the asset becomes invalid (e.g. deleted out from under the window).
    /// </remarks>
    internal sealed class ImportAndClusterColorsWindow : EditorWindow
    {
        private const string UxmlPath = "/Assets/Windows/ImportAndClusterColorsWindow.uxml";
        private const float DefaultMergeTolerance = 0.06f;

        private ScienceRegionData _data;
        private KMeansColorClustering.Cluster[] _clusters;
        private string[] _clusterNames;

        private Label _sourceLabel;
        private Label _pixelStatsLabel;
        private SliderInt _kSlider;
        private Toggle _snapEdgesToggle;
        private Toggle _posterizeToggle;
        private SliderInt _posterizeBitsSlider;
        private Button _clusterButton;
        private Label _resultsStatusLabel;
        private VisualElement _clustersList;
        private DropdownField _applyModeDropdown;
        private FloatField _mergeToleranceField;
        private Button _applyButton;

        private static readonly string[] ApplyModeChoices = { "Replace existing regions", "Merge with existing" };

        /// <summary>
        /// Opens the window bound to <paramref name="data" />.
        /// </summary>
        /// <param name="data">The Science Region asset whose source map will be clustered.</param>
        public static void Open(ScienceRegionData data)
        {
            if (data == null)
            {
                EditorUtility.DisplayDialog("Import & cluster colors", "No Science Region asset selected.", "OK");
                return;
            }
            var window = GetWindow<ImportAndClusterColorsWindow>(utility: true, title: "Import & cluster colors");
            window.minSize = new Vector2(360f, 480f);
            window.Bind(data);
        }

        private void Bind(ScienceRegionData data)
        {
            _data = data;
            RefreshSourceLabels();
        }

        private void CreateGUI()
        {
            var root = rootVisualElement;

            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(SDKConfiguration.BasePath + UxmlPath);
            if (tree == null)
            {
                root.Add(new Label("Failed to load ImportAndClusterColorsWindow.uxml"));
                return;
            }
            tree.CloneTree(root);
            Ksp2UnityToolsStyles.Apply(root);

            _sourceLabel = root.Q<Label>("source-label");
            _pixelStatsLabel = root.Q<Label>("pixel-stats-label");
            _kSlider = root.Q<SliderInt>("cluster-count-slider");
            _snapEdgesToggle = root.Q<Toggle>("snap-edges-toggle");
            _posterizeToggle = root.Q<Toggle>("posterize-toggle");
            _posterizeBitsSlider = root.Q<SliderInt>("posterize-bits-slider");
            _clusterButton = root.Q<Button>("cluster-button");
            _resultsStatusLabel = root.Q<Label>("results-status-label");
            _clustersList = root.Q<VisualElement>("clusters-list");
            _applyModeDropdown = root.Q<DropdownField>("apply-mode-dropdown");
            _mergeToleranceField = root.Q<FloatField>("merge-tolerance-field");
            _applyButton = root.Q<Button>("apply-button");

            _kSlider.SetValueWithoutNotify(7);
            _snapEdgesToggle.SetValueWithoutNotify(true);
            _posterizeToggle.SetValueWithoutNotify(false);
            _posterizeBitsSlider.SetValueWithoutNotify(5);
            _posterizeBitsSlider.style.display = DisplayStyle.None;
            _posterizeToggle.RegisterValueChangedCallback(evt =>
            {
                _posterizeBitsSlider.style.display = evt.newValue ? DisplayStyle.Flex : DisplayStyle.None;
            });

            _applyModeDropdown.choices = new List<string>(ApplyModeChoices);
            _applyModeDropdown.SetValueWithoutNotify(ApplyModeChoices[1]);
            _mergeToleranceField.SetValueWithoutNotify(DefaultMergeTolerance);

            _clusterButton.clicked += OnClusterClicked;
            _applyButton.clicked += OnApplyClicked;

            RefreshSourceLabels();
            ClearClustersList("Run clustering to discover regions.");
        }

        private void RefreshSourceLabels()
        {
            if (_sourceLabel == null) return;
            if (_data == null || _data.scienceRegionMap == null)
            {
                _sourceLabel.text = "Source: (no scienceRegionMap assigned)";
                _pixelStatsLabel.text = string.Empty;
                _clusterButton?.SetEnabled(false);
                return;
            }
            var src = _data.scienceRegionMap;
            _sourceLabel.text = $"Source: {src.name}";
            _pixelStatsLabel.text = $"Pixels: {src.width * src.height:N0}    Size: {src.width} x {src.height}    Format: {src.format}";
            _clusterButton?.SetEnabled(src.isReadable);
            if (!src.isReadable && _resultsStatusLabel != null)
            {
                _resultsStatusLabel.text = "Source texture is not Read/Write enabled. Enable it in the importer to cluster.";
            }
        }

        private void OnClusterClicked()
        {
            if (_data == null || _data.scienceRegionMap == null) return;

            try
            {
                var opts = new KMeansColorClustering.Options
                {
                    K = _kSlider.value,
                    SnapAntiAliased = _snapEdgesToggle.value,
                    PosterizeFirst = _posterizeToggle.value,
                    PosterizeBitsPerChannel = _posterizeBitsSlider.value,
                    RandomSeed = 0x5EED,
                };
                _clusters = KMeansColorClustering.Run(
                    _data.scienceRegionMap,
                    opts,
                    progress: (frac, label) => EditorUtility.DisplayProgressBar("Import & cluster colors", label, frac));
                _clusterNames = new string[_clusters.Length];
                for (var i = 0; i < _clusters.Length; i++)
                {
                    _clusterNames[i] = $"Cluster {i + 1}";
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            PopulateClustersList();
        }

        private void ClearClustersList(string status)
        {
            _clustersList?.Clear();
            if (_resultsStatusLabel != null)
                _resultsStatusLabel.text = status ?? string.Empty;
        }

        private void PopulateClustersList()
        {
            _clustersList.Clear();
            if (_clusters == null || _clusters.Length == 0)
            {
                _resultsStatusLabel.text = "No clusters found.";
                return;
            }
            _resultsStatusLabel.text = $"{_clusters.Length} clusters discovered. Rename below before applying.";

            for (var i = 0; i < _clusters.Length; i++)
            {
                var captured = i;
                var cluster = _clusters[i];

                var row = new VisualElement();
                row.AddToClassList("sdk-button-row");

                var swatch = new VisualElement();
                swatch.AddToClassList("sdk-tile-grid-cell");
                swatch.style.backgroundColor = (Color)cluster.Centroid;
                swatch.tooltip = $"RGB({cluster.Centroid.r}, {cluster.Centroid.g}, {cluster.Centroid.b})";
                row.Add(swatch);

                var nameField = new TextField { value = _clusterNames[captured] };
                nameField.style.flexGrow = 1f;
                nameField.RegisterValueChangedCallback(evt => _clusterNames[captured] = evt.newValue);
                row.Add(nameField);

                var stats = new Label($"{cluster.PixelFraction * 100f:0.0}%   ({cluster.PixelCount:N0} px)");
                stats.AddToClassList("sdk-hint");
                stats.style.minWidth = 110f;
                stats.style.unityTextAlign = TextAnchor.MiddleRight;
                row.Add(stats);

                _clustersList.Add(row);
            }
        }

        private void OnApplyClicked()
        {
            if (_data == null || _clusters == null || _clusters.Length == 0)
            {
                EditorUtility.DisplayDialog("Import & cluster colors", "Run clustering first.", "OK");
                return;
            }

            var replace = _applyModeDropdown.value == ApplyModeChoices[0];
            var tolerance = Mathf.Clamp01(_mergeToleranceField.value);

            Undo.RecordObject(_data, "Apply science region clusters");

            if (replace)
            {
                ApplyReplace();
            }
            else
            {
                ApplyMerge(tolerance);
            }

            EditorUtility.SetDirty(_data);
            AssetDatabase.SaveAssets();
            Close();
        }

        private void ApplyReplace()
        {
            var defs = new ScienceRegionData.ExtendedScienceRegionDefinition[_clusters.Length];
            for (var i = 0; i < _clusters.Length; i++)
            {
                defs[i] = new ScienceRegionData.ExtendedScienceRegionDefinition
                {
                    Id = _clusterNames[i],
                    MapId = i + 1,
                    AtmosphereScalar = 1f,
                    SplashedScalar = 1f,
                    LandedScalar = 1f,
                    RegionColor = (Color)_clusters[i].Centroid,
                };
            }
            _data.information.ScienceRegionDefinitions = defs;
        }

        private void ApplyMerge(float tolerance)
        {
            var existing = new List<ScienceRegionData.ExtendedScienceRegionDefinition>();
            if (_data.information.ScienceRegionDefinitions != null)
            {
                existing.AddRange(_data.information.ScienceRegionDefinitions);
            }

            var nextMapId = 1;
            foreach (var def in existing)
            {
                nextMapId = Mathf.Max(nextMapId, def.MapId + 1);
            }

            // Snapshot the original count. New clusters get appended to `existing` and we don't
            // want subsequent iterations matching against rows we just added (and `used` is sized
            // for the original entries only).
            var originalCount = existing.Count;
            var toleranceSquared = tolerance * tolerance * 3f; // sum-of-squares in normalized RGB
            var used = new bool[originalCount];
            for (var c = 0; c < _clusters.Length; c++)
            {
                Color centroid = _clusters[c].Centroid;
                var matchIdx = -1;
                var bestSq = toleranceSquared;
                for (var e = 0; e < originalCount; e++)
                {
                    if (used[e]) continue;
                    var ec = existing[e].RegionColor;
                    var dr = centroid.r - ec.r;
                    var dg = centroid.g - ec.g;
                    var db = centroid.b - ec.b;
                    var s = dr * dr + dg * dg + db * db;
                    if (s <= bestSq)
                    {
                        bestSq = s;
                        matchIdx = e;
                    }
                }
                if (matchIdx >= 0)
                {
                    used[matchIdx] = true;
                    existing[matchIdx].RegionColor = centroid;
                }
                else
                {
                    existing.Add(new ScienceRegionData.ExtendedScienceRegionDefinition
                    {
                        Id = _clusterNames[c],
                        MapId = nextMapId++,
                        AtmosphereScalar = 1f,
                        SplashedScalar = 1f,
                        LandedScalar = 1f,
                        RegionColor = centroid,
                    });
                }
            }

            _data.information.ScienceRegionDefinitions = existing.ToArray();
        }
    }
}
