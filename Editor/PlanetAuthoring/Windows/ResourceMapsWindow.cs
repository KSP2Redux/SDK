using System.Collections.Generic;
using System.IO;
using Ksp2UnityTools.Editor.PlanetAuthoring.ResourceMaps;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Windows
{
    /// <summary>
    /// Authoring window for a celestial body's resource density maps.
    /// </summary>
    /// <remarks>
    /// A body's biome mask supplies the shape of each deposit and fractal noise supplies its
    /// texture. The two are combined per pixel into the single channel density map the mining and
    /// map-view overlay code samples.
    ///
    /// Generating writes four things, because a PNG alone is invisible to the game: the map, its
    /// importer settings, the definition JSON, and both Addressables entries. See
    /// <see cref="ResourceMapBakeOperation" />.
    /// </remarks>
    public partial class ResourceMapsWindow : EditorWindow
    {
        private const string UXML_PATH = "/Assets/Windows/PlanetAuthoring/Windows/ResourceMapsWindow.uxml";
        private const string PREFS_PREFIX = "Ksp2UnityTools.ResourceMaps.";
        private const string TITLE = "Resource Maps";

        /// <summary>
        /// Opens or focuses the Resource Maps window.
        /// </summary>
        [MenuItem(PlanetAuthoringWindows.MenuRoot + TITLE, priority = PlanetAuthoringWindows.PriorityResourceMaps)]
        public static void ShowWindow()
        {
            var window = GetWindow<ResourceMapsWindow>();
            window.titleContent = new GUIContent(TITLE);
            window.minSize = new Vector2(420f, 480f);
        }

        private ObjectField _authoring;
        private TextField _mask;
        private DropdownField _body;
        private TextField _bodyCustom;

        // Sticky rather than derived from the stored name, so the dropdown does not snap back to a
        // known body the moment what has been typed happens to match one.
        private bool _customBody;
        private VisualElement _maskWarningSlot;
        private VisualElement _windowWarningSlot;
        private VisualElement _resourcesSlot;

        private TextField _mapFolder;
        private TextField _definitionFolder;
        private TextField _authoringFolder;
        private TextField _maskFolder;
        private TextField _group;
        private DropdownField _outputSize;
        private DropdownField _previewSize;

        private ResourceMapAuthoring _asset;
        private SerializedObject _serialized;

        private BiomeMask _fullMask;
        private BiomeMask _previewMask;
        private Vector4 _channelPeaks;

        private List<string> _bodyNames = new();
        private List<string> _resourceNames = new();

        private readonly List<ResourceCardView> _cards = new();

        private void CreateGUI()
        {
            VisualElement root = rootVisualElement;

            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(SDKConfiguration.BasePath + UXML_PATH);
            if (tree == null)
            {
                root.Add(new Label("Failed to load ResourceMapsWindow.uxml"));
                return;
            }

            tree.CloneTree(root);
            Ksp2UnityToolsStyles.Apply(root);

            _bodyNames = ResourceMapCatalog.GetCelestialBodyNames();
            _resourceNames = ResourceMapCatalog.GetResourceNames();

            BuildAuthoringField(root.Q<VisualElement>("authoring-slot"));

            _mask = root.Q<TextField>("mask-field");
            _body = root.Q<DropdownField>("body-field");
            _maskWarningSlot = root.Q<VisualElement>("mask-warning-slot");
            _windowWarningSlot = root.Q<VisualElement>("window-warning-slot");
            _resourcesSlot = root.Q<VisualElement>("resources-slot");

            _mapFolder = root.Q<TextField>("map-folder-field");
            _definitionFolder = root.Q<TextField>("definition-folder-field");
            _authoringFolder = root.Q<TextField>("authoring-folder-field");
            _maskFolder = root.Q<TextField>("mask-folder-field");
            _group = root.Q<TextField>("group-field");
            _outputSize = root.Q<DropdownField>("output-size-field");
            _previewSize = root.Q<DropdownField>("preview-size-field");

            _bodyCustom = root.Q<TextField>("body-custom-field");
            _body.choices = new List<string>(_bodyNames) { ResourceMapCatalog.OTHER_ENTRY };
            _body.RegisterValueChangedCallback(OnBodyChanged);
            _bodyCustom.RegisterValueChangedCallback(evt =>
            {
                if (_asset != null)
                {
                    WriteBodyName(evt.newValue);
                }
            });

            root.Q<Button>("mask-button").clicked += ImportMask;
            root.Q<Button>("new-button").clicked += CreateAuthoringAsset;
            root.Q<Button>("reset-settings-button").clicked += ResetSettings;

            LoadSettingsIntoFields();
            RegisterSettingsCallbacks();

            RestoreLastAsset();

            // Drives the debounced previews. Every card ticks from here so there is one scheduler
            // for the window rather than one per card.
            root.schedule.Execute(TickPreviews).Every(33);
        }

        private void OnDestroy()
        {
            DisposeCards();
        }

        private void BuildAuthoringField(VisualElement slot)
        {
            // Built here rather than declared in the UXML so the field never depends on the
            // assembly-qualified type name, which a UXML ObjectField would have to spell out.
            _authoring = new ObjectField("Authoring Asset")
            {
                objectType = typeof(ResourceMapAuthoring),
                allowSceneObjects = false,
                tooltip = "The body being authored. This is the Load control: pick, drag in, or create one with the button below.",
            };
            _authoring.AddToClassList("sdk-field");
            _authoring.AddToClassList("unity-base-field__aligned");
            _authoring.RegisterValueChangedCallback(evt => BindAsset(evt.newValue as ResourceMapAuthoring));
            slot?.Add(_authoring);
        }

        private void TickPreviews()
        {
            foreach (ResourceCardView card in _cards)
            {
                card.Tick();
            }
        }

        private void DisposeCards()
        {
            foreach (ResourceCardView card in _cards)
            {
                card.Dispose();
            }
            _cards.Clear();
        }

        private void RestoreLastAsset()
        {
            string path = EditorPrefs.GetString(PREFS_PREFIX + "LastAsset", "");
            if (string.IsNullOrEmpty(path))
            {
                BindAsset(null);
                return;
            }

            var asset = AssetDatabase.LoadAssetAtPath<ResourceMapAuthoring>(path);
            _authoring.SetValueWithoutNotify(asset);
            BindAsset(asset);
        }

        private void BindAsset(ResourceMapAuthoring asset)
        {
            DisposeCards();

            _asset = asset;
            _serialized = asset == null ? null : new SerializedObject(asset);
            _fullMask = null;
            _previewMask = null;
            _channelPeaks = Vector4.zero;

            if (asset != null)
            {
                EditorPrefs.SetString(PREFS_PREFIX + "LastAsset", AssetDatabase.GetAssetPath(asset));
            }

            _mask.SetValueWithoutNotify(asset == null ? "" : asset.BiomeMaskFileName);
            _customBody = false;
            RefreshBodyControls();

            LoadMask();
            RebuildResources();
        }

        // Drives both body controls from the stored name. The typed field only appears once Other
        // is the selection, and choosing Other keeps whatever name is already stored rather than
        // wiping a name the project simply does not declare yet.
        private void RefreshBodyControls()
        {
            string stored = _asset == null ? "" : _asset.CelestialBodyName ?? "";

            string match = null;
            foreach (string known in _bodyNames)
            {
                if (string.Equals(known, stored, System.StringComparison.OrdinalIgnoreCase))
                {
                    match = known;
                    break;
                }
            }

            bool custom = _customBody || (match == null && !string.IsNullOrEmpty(stored));
            _customBody = custom;

            _body.SetValueWithoutNotify(custom ? ResourceMapCatalog.OTHER_ENTRY : match);
            _bodyCustom.SetValueWithoutNotify(stored);
            _bodyCustom.style.display = custom ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void OnBodyChanged(ChangeEvent<string> evt)
        {
            if (_asset == null || _serialized == null)
                return;

            if (evt.newValue == ResourceMapCatalog.OTHER_ENTRY)
            {
                _customBody = true;
                RefreshBodyControls();
                _bodyCustom.Focus();
                RefreshWarnings();
                return;
            }

            _customBody = false;
            WriteBodyName(evt.newValue);
        }

        private void WriteBodyName(string value)
        {
            _serialized.Update();
            _serialized.FindProperty(nameof(ResourceMapAuthoring.CelestialBodyName)).stringValue = value ?? "";
            _serialized.ApplyModifiedProperties();
            RefreshBodyControls();
            RefreshWarnings();
        }

        private void CreateAuthoringAsset()
        {
            string folder = _authoringFolder.value;
            if (string.IsNullOrEmpty(folder))
            {
                folder = ResourceMapSettings.DEFAULT_AUTHORING_FOLDER;
            }
            Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();

            string path = EditorUtility.SaveFilePanelInProject(
                "New Resource Map Authoring Asset",
                "NewBody",
                "asset",
                "Choose where to create the authoring asset for this body.",
                folder
            );
            if (string.IsNullOrEmpty(path))
                return;

            var asset = CreateInstance<ResourceMapAuthoring>();
            asset.CelestialBodyName = Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();

            _authoring.value = asset;
        }

        private void ImportMask()
        {
            if (_asset == null)
            {
                EditorUtility.DisplayDialog(TITLE, "Pick or create an authoring asset first.", "OK");
                return;
            }

            string picked = EditorUtility.OpenFilePanel("Choose Biome Mask", ResourceMapSettings.GetAbsoluteMaskFolder(), "png");
            if (string.IsNullOrEmpty(picked))
                return;

            string destinationFolder = ResourceMapSettings.GetAbsoluteMaskFolder();
            string fileName = Path.GetFileName(picked);
            string destination = $"{destinationFolder}/{fileName}";

            try
            {
                Directory.CreateDirectory(destinationFolder);
                // Already-in-place is the common case on a second run, and copying a file onto
                // itself throws.
                if (Path.GetFullPath(picked) != Path.GetFullPath(destination))
                {
                    File.Copy(picked, destination, true);
                }
            }
            catch (IOException ex)
            {
                EditorUtility.DisplayDialog(TITLE, $"Could not copy the mask into '{destinationFolder}'.\n\n{ex.Message}", "OK");
                return;
            }

            _serialized.Update();
            _serialized.FindProperty(nameof(ResourceMapAuthoring.BiomeMaskFileName)).stringValue = fileName;

            // A mask names its body, so the common path is zero clicks on the body dropdown.
            string guessed = ResourceMapCatalog.GuessBodyFromMaskName(fileName, _bodyNames);
            if (!string.IsNullOrEmpty(guessed) && string.IsNullOrEmpty(_asset.CelestialBodyName))
            {
                _serialized.FindProperty(nameof(ResourceMapAuthoring.CelestialBodyName)).stringValue = guessed;
            }
            _serialized.ApplyModifiedProperties();

            _mask.SetValueWithoutNotify(fileName);
            RefreshBodyControls();

            LoadMask();
            RebuildResources();
        }

        private void LoadMask()
        {
            _fullMask = null;
            _previewMask = null;
            _channelPeaks = Vector4.zero;

            if (_asset == null || string.IsNullOrEmpty(_asset.BiomeMaskFileName))
            {
                RefreshWarnings();
                return;
            }

            string path = ResourceMapSettings.GetAbsoluteMaskPath(_asset.BiomeMaskFileName);
            try
            {
                EditorUtility.DisplayProgressBar(TITLE, $"Loading {_asset.BiomeMaskFileName}...", 0.5f);
                _fullMask = BiomeMask.Load(path);
                _previewMask = _fullMask.Downsample(ResourceMapSettings.PreviewSize);
                _channelPeaks = _fullMask.GetChannelPeaks();
            }
            catch (System.Exception ex)
            {
                _fullMask = null;
                _previewMask = null;
                Debug.LogWarning($"[ResourceMaps] {ex.Message}");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            RefreshWarnings();
        }

        private void RefreshWarnings()
        {
            SetWarning(_maskWarningSlot, BuildMaskWarning());
            SetWarning(_windowWarningSlot, BuildWindowWarning());
        }

        private string BuildMaskWarning()
        {
            if (_asset == null)
                return null;
            if (string.IsNullOrEmpty(_asset.BiomeMaskFileName))
                return "No biome mask imported. Density maps need one to know where each biome is.";
            if (_fullMask == null)
            {
                return $"'{_asset.BiomeMaskFileName}' is not in {ResourceMapSettings.MaskFolder}. " +
                    "The masks are ripped stock art and stay out of version control, so import your own copy.";
            }

            var notes = new List<string>();
            if (_fullMask.Width != _fullMask.Height)
            {
                notes.Add($"The mask is {_fullMask.Width} by {_fullMask.Height}. An equirectangular mask is normally twice as wide as it is tall or square.");
            }

            var emptyChannels = new List<string>();
            for (var channel = 0; channel < ResourceMapAuthoring.CHANNEL_COUNT; channel++)
            {
                if (PeakForChannel(channel) <= 0f)
                {
                    emptyChannels.Add(ResourceMapAuthoring.CHANNEL_NAMES[channel]);
                }
            }
            if (emptyChannels.Count > 0)
            {
                notes.Add($"Channels {string.Join(", ", emptyChannels)} carry no coverage, so nothing sampled from them reaches the map.");
            }

            return notes.Count > 0 ? string.Join("\n", notes) : null;
        }

        private string BuildWindowWarning()
        {
            if (_asset == null)
                return null;

            var notes = new List<string>();
            if (string.IsNullOrEmpty(_asset.CelestialBodyName))
            {
                notes.Add("No celestial body chosen. Generated files need it for their names and their definitions.");
            }
            if (ResourceMapBakeOperation.ResolveGroup(_group.value) == null)
            {
                notes.Add($"No Addressables group named '{_group.value}'. Generated files will be written but will not load until they are registered.");
            }

            return notes.Count > 0 ? string.Join("\n", notes) : null;
        }

        private float PeakForChannel(int channel) => channel switch
        {
            1 => _channelPeaks.y,
            2 => _channelPeaks.z,
            3 => _channelPeaks.w,
            _ => _channelPeaks.x,
        };

        private static void SetWarning(VisualElement slot, string message)
        {
            if (slot == null)
                return;
            slot.Clear();
            if (!string.IsNullOrEmpty(message))
            {
                slot.Add(new HelpBox(message, HelpBoxMessageType.Warning));
            }
        }

        private void LoadSettingsIntoFields()
        {
            _mapFolder.SetValueWithoutNotify(ResourceMapSettings.MapFolder);
            _definitionFolder.SetValueWithoutNotify(ResourceMapSettings.DefinitionFolder);
            _authoringFolder.SetValueWithoutNotify(ResourceMapSettings.AuthoringFolder);
            _maskFolder.SetValueWithoutNotify(ResourceMapSettings.MaskFolder);
            _group.SetValueWithoutNotify(ResourceMapSettings.GroupName);
            _outputSize.SetValueWithoutNotify(ResourceMapSettings.OutputSize.ToString());
            _previewSize.SetValueWithoutNotify(ResourceMapSettings.PreviewSize.ToString());
        }

        private void RegisterSettingsCallbacks()
        {
            _mapFolder.RegisterValueChangedCallback(evt => ResourceMapSettings.MapFolder = evt.newValue);
            _definitionFolder.RegisterValueChangedCallback(evt => ResourceMapSettings.DefinitionFolder = evt.newValue);
            _authoringFolder.RegisterValueChangedCallback(evt => ResourceMapSettings.AuthoringFolder = evt.newValue);
            _group.RegisterValueChangedCallback(evt =>
            {
                ResourceMapSettings.GroupName = evt.newValue;
                RefreshWarnings();
            });
            _maskFolder.RegisterValueChangedCallback(evt =>
            {
                ResourceMapSettings.MaskFolder = evt.newValue;
                LoadMask();
                RebuildResources();
            });
            _outputSize.RegisterValueChangedCallback(evt =>
            {
                if (int.TryParse(evt.newValue, out int size))
                {
                    ResourceMapSettings.OutputSize = size;
                }
            });
            _previewSize.RegisterValueChangedCallback(evt =>
            {
                if (!int.TryParse(evt.newValue, out int size))
                    return;
                ResourceMapSettings.PreviewSize = size;
                _previewMask = _fullMask?.Downsample(size);
                RebuildResources();
            });
        }

        private void ResetSettings()
        {
            ResourceMapSettings.MapFolder = ResourceMapSettings.DEFAULT_MAP_FOLDER;
            ResourceMapSettings.DefinitionFolder = ResourceMapSettings.DEFAULT_DEFINITION_FOLDER;
            ResourceMapSettings.AuthoringFolder = ResourceMapSettings.DEFAULT_AUTHORING_FOLDER;
            ResourceMapSettings.MaskFolder = ResourceMapSettings.DEFAULT_MASK_FOLDER;
            ResourceMapSettings.GroupName = ResourceMapSettings.DEFAULT_GROUP_NAME;
            ResourceMapSettings.OutputSize = ResourceMapSettings.DEFAULT_OUTPUT_SIZE;
            ResourceMapSettings.PreviewSize = ResourceMapSettings.DEFAULT_PREVIEW_SIZE;

            LoadSettingsIntoFields();
            LoadMask();
            RebuildResources();
        }
    }
}
