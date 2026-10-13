using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Redux.Features;
using SpaceWarp2.API.Features;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.Modding
{
    /// <summary>
    /// Lists one mod's addressable assets with the features each requires and excludes, and sets them on the
    /// selected assets in bulk.
    /// </summary>
    public class AssetFeaturesWindow : EditorWindow
    {
        private const string UXML_PATH = "/Assets/Windows/AssetFeaturesWindow.uxml";
        private const string ALL_KINDS = "All";
        private const string NO_KIND = "none";

        // Null stands for Redux's own content, which has no mod asset
        private readonly List<Mod> _owners = new();
        private Mod _owner;
        private bool _hasOwner;
        private string _kind = ALL_KINDS;
        private string _search = string.Empty;
        private string _pendingFeature = string.Empty;
        private List<AddressableAssetEntry> _rows = new();
        private MultiColumnListView _list;
        private PopupField<string> _ownerField;
        private PopupField<string> _kindField;
        private Label _count;

        /// <summary>
        /// Opens the window on the first mod in the project.
        /// </summary>
        [MenuItem("Modding/Asset Features...", false, 120)]
        public static void OpenFromMenu() => Open(null);

        /// <summary>
        /// Opens the window on a mod's assets.
        /// </summary>
        /// <param name="owner">The mod, or null for the first in the project.</param>
        public static void Open(Mod owner)
        {
            var window = GetWindow<AssetFeaturesWindow>();
            window.titleContent = new GUIContent("Asset Features");
            if (owner != null)
            {
                window._owner = owner;
                window._hasOwner = true;
                window._ownerField?.SetValueWithoutNotify(NameOf(owner));
                window.BuildFeatureField();
            }

            window.Refresh();
        }

        private string OwnerId => _owner != null ? _owner.id : FeatureCatalog.REDUX_MOD_ID;

        private void CreateGUI()
        {
            AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(SDKConfiguration.BasePath + UXML_PATH).CloneTree(rootVisualElement);
            Ksp2UnityToolsStyles.Apply(rootVisualElement);

            _owners.Clear();
#if REDUX
            _owners.Add(null);
#endif
            _owners.AddRange(FeatureCatalog.ProjectMods().OrderBy(mod => mod.id, StringComparer.OrdinalIgnoreCase));
            if (!_hasOwner)
            {
                _owner = _owners.FirstOrDefault();
            }

            List<string> ownerNames = _owners.Select(NameOf).ToList();
            _ownerField = new PopupField<string>("Mod", ownerNames, Math.Max(0, _owners.IndexOf(_owner)));
            _ownerField.RegisterValueChangedCallback(evt =>
            {
                _owner = _owners[ownerNames.IndexOf(evt.newValue)];
                BuildFeatureField();
                Refresh();
            });
            rootVisualElement.Q("mod-slot").Add(_ownerField);

            _kindField = new PopupField<string>("Kind", new List<string> { ALL_KINDS }, 0);
            _kindField.RegisterValueChangedCallback(evt =>
            {
                _kind = evt.newValue;
                Refresh();
            });
            rootVisualElement.Q("kind-slot").Add(_kindField);

            rootVisualElement.Q<ToolbarSearchField>("search").RegisterValueChangedCallback(evt =>
            {
                _search = evt.newValue ?? string.Empty;
                Refresh();
            });

            _list = rootVisualElement.Q<MultiColumnListView>("assets");
            BindColumn("asset", entry => Path.GetFileName(entry.AssetPath));
            BindColumn("kind", KindOf);
            BindColumn("features", entry => string.Join("  ", Read(entry).features.Prepend(OwnerId)));
            BindColumn("excluded", entry => string.Join("  ", Read(entry).noFeatures));
            _count = rootVisualElement.Q<Label>("selection-count");
            _list.selectedIndicesChanged += _ => UpdateCount();

            BuildFeatureField();
            rootVisualElement.Q<Button>("require").clicked += () => EditSelected(require: true);
            rootVisualElement.Q<Button>("exclude").clicked += () => EditSelected(require: false);
            rootVisualElement.Q<Button>("remove").clicked += () => EditSelected(require: null);
            Refresh();
        }

        // Suggestions follow the mod whose assets are shown
        private void BuildFeatureField()
        {
            VisualElement slot = rootVisualElement.Q("feature-slot");
            if (slot == null)
                return;

            slot.Clear();
            slot.Add(new FeatureField(_pendingFeature, null, _owner, value => _pendingFeature = value));
        }

        private static string NameOf(Mod mod) => mod != null ? mod.id : FeatureCatalog.REDUX_MOD_ID;

        private void BindColumn(string name, Func<AddressableAssetEntry, string> textOf)
        {
            Column column = _list.columns[name];
            column.makeCell = () => new Label();
            column.bindCell = (cell, index) => ((Label)cell).text = textOf(_rows[index]);
        }

        private (List<string> features, List<string> noFeatures) Read(AddressableAssetEntry entry) =>
            AssetFeatureLabels.Read(entry.labels, OwnerId);

        // The content label the game loads an asset under names its kind
        private static string KindOf(AddressableAssetEntry entry) =>
            entry.labels.FirstOrDefault(FeatureAssetIndex.StockFeatureByLabel.ContainsKey)
            ?? entry.labels.FirstOrDefault(label =>
                !label.StartsWith(FeatureSelector.FEATURE_TAG_PREFIX, StringComparison.Ordinal)
                && !label.StartsWith(FeatureSelector.NO_FEATURE_TAG_PREFIX, StringComparison.Ordinal))
            ?? NO_KIND;

        // Redux's own content is every group no mod asset claims
        private IEnumerable<AddressableAssetEntry> EntriesOfOwner()
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
                return Enumerable.Empty<AddressableAssetEntry>();

            IEnumerable<AddressableAssetGroup> groups;
            if (_owner != null)
            {
                groups = _owner.AllGroups.Where(group => group != null).Distinct();
            }
            else
            {
                var modGroups = new HashSet<AddressableAssetGroup>(
                    FeatureCatalog.ProjectMods().SelectMany(mod => mod.AllGroups).Where(group => group != null)
                );
                groups = settings.groups.Where(group => group != null && !modGroups.Contains(group));
            }

            return groups.SelectMany(group => group.entries);
        }

        private void Refresh()
        {
            if (_list == null)
                return;

            List<AddressableAssetEntry> entries = EntriesOfOwner().ToList();
            List<string> kinds = entries.Select(KindOf).Distinct().OrderBy(kind => kind, StringComparer.Ordinal).Prepend(ALL_KINDS).ToList();
            _kindField.choices = kinds;
            if (!kinds.Contains(_kind))
            {
                _kind = ALL_KINDS;
                _kindField.SetValueWithoutNotify(ALL_KINDS);
            }

            _rows = entries
                .Where(entry => _kind == ALL_KINDS || KindOf(entry) == _kind)
                .Where(entry => _search.Length == 0 || entry.AssetPath.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderBy(entry => Path.GetFileName(entry.AssetPath), StringComparer.OrdinalIgnoreCase)
                .ToList();
            _list.itemsSource = _rows;
            _list.RefreshItems();
            UpdateCount();
        }

        private void UpdateCount() => _count.text = $"{_list.selectedIndices.Count()} selected";

        // Requires the pending feature, excludes it, or with null takes it off both lists, on every selected asset
        private void EditSelected(bool? require)
        {
            if (string.IsNullOrWhiteSpace(_pendingFeature))
                return;

            string target = FeatureRegistry.Qualify(OwnerId, _pendingFeature.Trim());
            foreach (AddressableAssetEntry entry in _list.selectedIndices.Select(index => _rows[index]).ToList())
            {
                (List<string> features, List<string> noFeatures) = Read(entry);
                features.RemoveAll(id => IsTarget(id, target));
                noFeatures.RemoveAll(id => IsTarget(id, target));
                if (require == true)
                {
                    features.Add(target);
                }
                else if (require == false)
                {
                    noFeatures.Add(target);
                }

                AssetFeatureLabels.Apply(new[] { entry.AssetPath }, OwnerId, features, noFeatures);
            }

            Refresh();
        }

        private bool IsTarget(string id, string target) =>
            string.Equals(FeatureRegistry.Qualify(OwnerId, id), target, StringComparison.OrdinalIgnoreCase);
    }
}
