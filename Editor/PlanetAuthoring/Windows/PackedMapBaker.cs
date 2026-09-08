using System;
using System.Collections.Generic;
using System.IO;
using Ksp2UnityTools.Editor.PlanetAuthoring.Tools;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Windows
{
    /// <summary>
    /// Editor window that packs separate normal, smoothness and ambient-occlusion maps into the single tile texture the small-layer cascade samples.
    /// </summary>
    /// <remarks>
    /// Output RGBA is <c>(smoothness, normalY, AO, normalX)</c>, which is what
    /// <c>_SmallNormalArray</c> and <c>_DecalNormalSAO</c> are read as at runtime. The pack itself
    /// runs on the GPU through <see cref="PackedMapBakeOperation" />, so sources stay compressed and
    /// non-readable and mismatched source sizes resample on the way in.
    ///
    /// Batch mode pairs a folder's files by shared name stem. See
    /// <see cref="PackedMapBatchPairing" /> for the matching rules.
    /// </remarks>
    public class PackedMapBaker : EditorWindow
    {
        private const string UxmlPath = "/Assets/Windows/PlanetAuthoring/Windows/PackedMapBaker.uxml";
        private const string PrefsPrefix = "Ksp2UnityTools.PackedMapBaker.";
        private const string Title = "Packed Map Baker";

        /// <summary>
        /// Opens or focuses the Packed Map Baker editor window.
        /// </summary>
        [MenuItem(PlanetAuthoringWindows.MenuRoot + "Packed Map Baker")]
        public static void ShowWindow()
        {
            var window = GetWindow<PackedMapBaker>();
            window.titleContent = new GUIContent(Title);
        }

        private DropdownField _mode;
        private Foldout _sourcesSection;
        private Foldout _batchSection;

        private ObjectField _normal;
        private DropdownField _normalEncoding;
        private Toggle _flipGreen;

        private ObjectField _smoothness;
        private DropdownField _smoothnessChannel;
        private Toggle _smoothnessInvert;
        private FloatField _smoothnessConstant;

        private ObjectField _ao;
        private DropdownField _aoChannel;
        private FloatField _aoConstant;

        private DropdownField _resolution;

        private TextField _inputFolder;
        private TextField _outputFolder;
        private TextField _normalSuffixes;
        private TextField _smoothnessSuffixes;
        private TextField _roughnessSuffixes;
        private TextField _aoSuffixes;
        private Button _scan;
        private VisualElement _scanSlot;

        private Button _bake;
        private TextField _path;
        private Image _preview;
        private VisualElement _warningSlot;

        private bool IsBatchMode => _mode != null && _mode.index == 1;

        private void CreateGUI()
        {
            VisualElement root = rootVisualElement;

            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(SDKConfiguration.BasePath + UxmlPath);
            if (tree == null)
            {
                root.Add(new Label("Failed to load PackedMapBaker.uxml"));
                return;
            }

            tree.CloneTree(root);
            Ksp2UnityToolsStyles.Apply(root);

            _mode = root.Q<DropdownField>("mode-field");
            _sourcesSection = root.Q<Foldout>("sources-section");
            _batchSection = root.Q<Foldout>("batch-section");

            _normal = root.Q<ObjectField>("normal-field");
            _normalEncoding = root.Q<DropdownField>("normal-encoding-field");
            _flipGreen = root.Q<Toggle>("flip-green-field");

            _smoothness = root.Q<ObjectField>("smoothness-field");
            _smoothnessChannel = root.Q<DropdownField>("smoothness-channel-field");
            _smoothnessInvert = root.Q<Toggle>("smoothness-invert-field");
            _smoothnessConstant = root.Q<FloatField>("smoothness-constant-field");

            _ao = root.Q<ObjectField>("ao-field");
            _aoChannel = root.Q<DropdownField>("ao-channel-field");
            _aoConstant = root.Q<FloatField>("ao-constant-field");

            _resolution = root.Q<DropdownField>("resolution-field");

            _inputFolder = root.Q<TextField>("input-folder-field");
            _outputFolder = root.Q<TextField>("output-folder-field");
            _normalSuffixes = root.Q<TextField>("normal-suffixes-field");
            _smoothnessSuffixes = root.Q<TextField>("smoothness-suffixes-field");
            _roughnessSuffixes = root.Q<TextField>("roughness-suffixes-field");
            _aoSuffixes = root.Q<TextField>("ao-suffixes-field");
            _scan = root.Q<Button>("scan-button");
            _scanSlot = root.Q<VisualElement>("scan-slot");

            _bake = root.Q<Button>("bake-button");
            _path = root.Q<TextField>("path-field");
            _preview = root.Q<Image>("preview-image");
            _warningSlot = root.Q<VisualElement>("warning-slot");

            _bake.clicked += Bake;
            _scan.clicked += ScanBatchFolder;
            root.Q<Button>("input-folder-button").clicked += ChooseInputFolder;
            root.Q<Button>("output-folder-button").clicked += ChooseOutputFolder;
            _mode.RegisterValueChangedCallback(OnModeChanged);

            LoadPrefs();
            ApplyMode();
        }

        private void OnDestroy()
        {
            if (_bake != null)
            {
                _bake.clicked -= Bake;
            }
            if (_scan != null)
            {
                _scan.clicked -= ScanBatchFolder;
            }
            if (_mode != null)
            {
                _mode.UnregisterValueChangedCallback(OnModeChanged);
            }
        }

        private void OnModeChanged(ChangeEvent<string> evt)
        {
            ApplyMode();
            EditorPrefs.SetInt(PrefsPrefix + "Mode", _mode.index);
        }

        // The three source ObjectFields only make sense in single-tile mode, where the artist picks
        // the files. In batch mode the paths come from the folder scan, but every other setting on
        // the Sources foldout (channels, encoding, resolution) still applies to each tile.
        private void ApplyMode()
        {
            bool batch = IsBatchMode;
            _normal.SetEnabled(!batch);
            _smoothness.SetEnabled(!batch);
            _ao.SetEnabled(!batch);
            _smoothnessInvert.SetEnabled(!batch);
            _batchSection.style.display = batch ? DisplayStyle.Flex : DisplayStyle.None;
            _bake.text = batch ? "Bake Folder" : "Bake";
        }

        private void Bake()
        {
            SetWarning(null);
            if (IsBatchMode)
            {
                BakeBatch();
                return;
            }
            BakeSingle();
        }

        private void BakeSingle()
        {
            var normal = _normal.value as Texture2D;
            if (normal == null)
            {
                EditorUtility.DisplayDialog(Title, "Please assign a normal map.", "OK");
                return;
            }

            string defaultName = StripKnownSuffix(normal.name) + PackedMapSuffixes.OUTPUT_SUFFIX;
            string sourceFolder = Path.GetDirectoryName(AssetDatabase.GetAssetPath(normal)) ?? "Assets";
            string path = EditorUtility.SaveFilePanelInProject(
                "Save Packed Map",
                defaultName,
                "png",
                "Choose where to save the packed map.",
                sourceFolder
            );

            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            try
            {
                EditorUtility.DisplayProgressBar(Title, "Packing...", 0.5f);
                Texture2D written = BakeOne(
                    normal,
                    _smoothness.value as Texture2D,
                    null,
                    _ao.value as Texture2D,
                    path
                );
                ShowResult(path, written);
            }
            catch (Exception ex)
            {
                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog(Title, $"Packing failed.\n\n{ex.GetType().Name}: {ex.Message}", "OK");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private void BakeBatch()
        {
            PackedMapBatchPairing.ScanResult scan = RunScan();
            if (scan == null)
            {
                return;
            }
            if (scan.Tiles.Count == 0)
            {
                EditorUtility.DisplayDialog(Title, "The scan paired no tiles. Check the input folder and the suffix lists.", "OK");
                return;
            }

            string outputFolder = _outputFolder.value;
            var failures = new List<string>();
            string lastPath = null;
            Texture2D lastWritten = null;

            // No StartAssetEditing around this loop. Each tile's PNG has to import before its
            // importer settings can be applied and the asset loaded back for the preview, and
            // batched asset editing defers exactly that.
            try
            {
                for (var i = 0; i < scan.Tiles.Count; i++)
                {
                    PackedMapTileSources tile = scan.Tiles[i];
                    bool cancelled = EditorUtility.DisplayCancelableProgressBar(
                        Title,
                        $"Packing {tile.OutputName} ({i + 1} of {scan.Tiles.Count})",
                        (float)i / scan.Tiles.Count
                    );
                    if (cancelled)
                    {
                        failures.Add($"Cancelled after {i} of {scan.Tiles.Count} tiles.");
                        break;
                    }

                    var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(tile.NormalPath);
                    if (normal == null)
                    {
                        failures.Add($"{tile.Stem}: could not load '{tile.NormalPath}'.");
                        continue;
                    }

                    // Empty output folder writes each tile next to its own normal source, which
                    // keeps a nested source tree's layout instead of flattening it.
                    string folder = string.IsNullOrEmpty(outputFolder)
                        ? Path.GetDirectoryName(tile.NormalPath) ?? "Assets"
                        : outputFolder;
                    string path = $"{folder}/{tile.OutputName}.png".Replace('\\', '/');

                    try
                    {
                        lastWritten = BakeOne(
                            normal,
                            LoadOptional(tile.SmoothnessPath),
                            LoadOptional(tile.RoughnessPath),
                            LoadOptional(tile.AmbientOcclusionPath),
                            path
                        );
                        lastPath = path;
                    }
                    catch (Exception ex)
                    {
                        failures.Add($"{tile.Stem}: {ex.GetType().Name}: {ex.Message}");
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            ShowResult(lastPath, lastWritten);
            var messages = new List<string>(scan.Notes);
            messages.AddRange(failures);
            SetWarning(messages.Count > 0 ? string.Join("\n", messages) : null);
        }

        // A role the scan did not fill has a null path, which LoadAssetAtPath does not accept.
        private static Texture2D LoadOptional(string projectPath) =>
            string.IsNullOrEmpty(projectPath) ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(projectPath);

        // Runs one tile end to end. roughness is only consulted when there is no smoothness source,
        // matching how the scan reports the pair.
        private Texture2D BakeOne(Texture2D normal, Texture2D smoothness, Texture2D roughness, Texture2D ao, string outputPath)
        {
            bool useRoughness = smoothness == null && roughness != null;
            var request = new PackedMapBakeOperation.Request
            {
                Normal = normal,
                NormalEncoding = SelectedNormalEncoding(),
                FlipGreen = _flipGreen.value,
                Smoothness = useRoughness ? roughness : smoothness,
                SmoothnessChannel = (PackedMapChannel)_smoothnessChannel.index,
                InvertSmoothness = useRoughness || _smoothnessInvert.value,
                SmoothnessConstant = _smoothnessConstant.value,
                AmbientOcclusion = ao,
                AmbientOcclusionChannel = (PackedMapChannel)_aoChannel.index,
                AmbientOcclusionConstant = _aoConstant.value,
                Resolution = SelectedResolution(),
            };

            Texture2D baked = PackedMapBakeOperation.Bake(request);
            try
            {
                return PackedMapBakeOperation.WriteAndImport(baked, outputPath);
            }
            finally
            {
                DestroyImmediate(baked);
            }
        }

        private void ScanBatchFolder()
        {
            PackedMapBatchPairing.ScanResult scan = RunScan();
            if (scan == null)
            {
                return;
            }

            _scanSlot.Clear();
            _scanSlot.Add(new Label($"{scan.Tiles.Count} tile(s) paired."));
            foreach (PackedMapTileSources tile in scan.Tiles)
            {
                var parts = new List<string> { "normal" };
                if (!string.IsNullOrEmpty(tile.SmoothnessPath))
                {
                    parts.Add("smoothness");
                }
                if (!string.IsNullOrEmpty(tile.RoughnessPath))
                {
                    parts.Add("roughness");
                }
                if (!string.IsNullOrEmpty(tile.AmbientOcclusionPath))
                {
                    parts.Add("ao");
                }
                _scanSlot.Add(new Label($"{tile.OutputName}  ({string.Join(" + ", parts)})"));
            }

            var messages = new List<string>(scan.Notes);
            if (scan.StemsWithoutNormal.Count > 0)
            {
                messages.Add(
                    $"Skipped {scan.StemsWithoutNormal.Count} stem(s) with no normal map: " +
                    $"{string.Join(", ", scan.StemsWithoutNormal)}."
                );
            }
            SetWarning(messages.Count > 0 ? string.Join("\n", messages) : null);
        }

        // Returns null when the input folder is unusable, having already told the artist why.
        private PackedMapBatchPairing.ScanResult RunScan()
        {
            string folder = _inputFolder.value;
            if (string.IsNullOrEmpty(folder) || !AssetDatabase.IsValidFolder(folder))
            {
                EditorUtility.DisplayDialog(Title, "Choose an input folder inside the project first.", "OK");
                return null;
            }

            SavePrefs();

            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folder });
            var paths = new List<string>(guids.Length);
            foreach (string guid in guids)
            {
                paths.Add(AssetDatabase.GUIDToAssetPath(guid));
            }

            var suffixes = new PackedMapSuffixes
            {
                Normal = PackedMapSuffixes.Parse(_normalSuffixes.value),
                Smoothness = PackedMapSuffixes.Parse(_smoothnessSuffixes.value),
                Roughness = PackedMapSuffixes.Parse(_roughnessSuffixes.value),
                AmbientOcclusion = PackedMapSuffixes.Parse(_aoSuffixes.value),
            };

            return PackedMapBatchPairing.Scan(paths, suffixes);
        }

        private void ChooseInputFolder()
        {
            string picked = PickProjectFolder("Choose Input Folder", _inputFolder.value);
            if (picked == null)
            {
                return;
            }
            _inputFolder.value = picked;
            SavePrefs();
        }

        private void ChooseOutputFolder()
        {
            string picked = PickProjectFolder("Choose Output Folder", _outputFolder.value);
            if (picked == null)
            {
                return;
            }
            _outputFolder.value = picked;
            SavePrefs();
        }

        // Returns a project-relative folder path, or null when the artist cancelled or picked
        // somewhere outside the project.
        private static string PickProjectFolder(string title, string current)
        {
            string start = !string.IsNullOrEmpty(current) && AssetDatabase.IsValidFolder(current) ? current : "Assets";
            string absolute = EditorUtility.OpenFolderPanel(title, start, "");
            if (string.IsNullOrEmpty(absolute))
            {
                return null;
            }

            string projectRoot = Path.GetDirectoryName(Application.dataPath)?.Replace('\\', '/');
            string normalized = absolute.Replace('\\', '/');
            if (string.IsNullOrEmpty(projectRoot) || !normalized.StartsWith(projectRoot + "/", StringComparison.Ordinal))
            {
                EditorUtility.DisplayDialog(
                    Title,
                    "That folder is outside the project. Source maps have to be imported assets so the pack can sample them.",
                    "OK"
                );
                return null;
            }

            return normalized[(projectRoot.Length + 1)..];
        }

        private PackedMapNormalEncoding SelectedNormalEncoding() => _normalEncoding.index switch
        {
            1 => PackedMapNormalEncoding.Rgb,
            2 => PackedMapNormalEncoding.Dxt5nm,
            _ => PackedMapNormalEncoding.Auto,
        };

        // Index 0 is "Match Source", which the bake operation reads as 0 and resolves from the
        // normal map's own width. The rest are 512 doubling per entry.
        private int SelectedResolution() => _resolution.index == 0 ? 0 : 512 << (_resolution.index - 1);

        private void ShowResult(string path, Texture2D written)
        {
            _path.value = path ?? "";
            _preview.image = written;
            if (!string.IsNullOrEmpty(path))
            {
                EditorPrefs.SetString(PrefsPrefix + "LastPath", path);
            }
        }

        private void SetWarning(string message)
        {
            if (_warningSlot == null)
            {
                return;
            }
            _warningSlot.Clear();
            if (!string.IsNullOrEmpty(message))
            {
                _warningSlot.Add(new HelpBox(message, HelpBoxMessageType.Warning));
            }
        }

        // Drops a trailing role suffix from the normal map's name so the default output name is the
        // shared stem rather than "Rock_n_Packed".
        private string StripKnownSuffix(string name)
        {
            var suffixes = new PackedMapSuffixes
            {
                Normal = PackedMapSuffixes.Parse(_normalSuffixes.value),
            };
            foreach (string suffix in suffixes.Normal)
            {
                if (suffix.Length < name.Length && name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                {
                    return name[..^suffix.Length];
                }
            }
            return name;
        }

        private void LoadPrefs()
        {
            var defaults = new PackedMapSuffixes();
            _mode.index = EditorPrefs.GetInt(PrefsPrefix + "Mode", 0);
            _inputFolder.value = EditorPrefs.GetString(PrefsPrefix + "InputFolder", "");
            _outputFolder.value = EditorPrefs.GetString(PrefsPrefix + "OutputFolder", "");
            _normalSuffixes.value = EditorPrefs.GetString(PrefsPrefix + "NormalSuffixes", PackedMapSuffixes.Format(defaults.Normal));
            _smoothnessSuffixes.value = EditorPrefs.GetString(PrefsPrefix + "SmoothnessSuffixes", PackedMapSuffixes.Format(defaults.Smoothness));
            _roughnessSuffixes.value = EditorPrefs.GetString(PrefsPrefix + "RoughnessSuffixes", PackedMapSuffixes.Format(defaults.Roughness));
            _aoSuffixes.value = EditorPrefs.GetString(PrefsPrefix + "AoSuffixes", PackedMapSuffixes.Format(defaults.AmbientOcclusion));
            _path.value = EditorPrefs.GetString(PrefsPrefix + "LastPath", "");
        }

        private void SavePrefs()
        {
            EditorPrefs.SetString(PrefsPrefix + "InputFolder", _inputFolder.value ?? "");
            EditorPrefs.SetString(PrefsPrefix + "OutputFolder", _outputFolder.value ?? "");
            EditorPrefs.SetString(PrefsPrefix + "NormalSuffixes", _normalSuffixes.value ?? "");
            EditorPrefs.SetString(PrefsPrefix + "SmoothnessSuffixes", _smoothnessSuffixes.value ?? "");
            EditorPrefs.SetString(PrefsPrefix + "RoughnessSuffixes", _roughnessSuffixes.value ?? "");
            EditorPrefs.SetString(PrefsPrefix + "AoSuffixes", _aoSuffixes.value ?? "");
        }
    }
}
