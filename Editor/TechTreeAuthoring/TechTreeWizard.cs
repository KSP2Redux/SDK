using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ksp2UnityTools.Editor.API;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.TechTreeAuthoring
{
    /// <summary>
    /// Wizard for starting a tech tree: a patch that changes one that exists, or a tree of its own, empty or duplicated
    /// from one that exists.
    /// </summary>
    /// <remarks>
    /// One page, as the Create Celestial Body wizard is. The source is the game's nodes, a tech tree asset or folder
    /// of tech node JSON, or the feature set a campaign plays with as Patch Manager cached it, whose nodes are the tree
    /// after every patch ran. A tree can also start empty.
    /// A patch of a cached set keeps the set's nodes in a JSON folder beside it, so its reference stays put when the
    /// cache changes. The new asset is made in the folder selected in the Project window.
    /// </remarks>
    public class TechTreeWizard : EditorWindow
    {
        private const string UXML_PATH = "/Assets/Windows/TechTreeAuthoring/TechTreeWizard.uxml";

        private const string USS_PATH = "/Assets/Windows/TechTreeAuthoring/TechTreeWizard.uss";

        private const string STATUS_ERROR_CLASS = "tech-tree-wizard__status--error";

        private const string EMPTY = "Empty";

        private const string GAME_NODES = "Game nodes";

        private const string ASSET = "Tech tree or folder";

        private const string CACHE = "Patch Manager cache";

        private bool _makesPatch = true;

        private List<TechTreeCacheSet> _sets = new();

        private Button _patchTab;

        private Button _treeTab;

        private DropdownField _sourceField;

        private ObjectField _assetField;

        private DropdownField _setField;

        private Label _setFeatures;

        private TextField _nameField;

        private Label _statusLabel;

        private Button _createButton;

        /// <summary>
        /// Opens the wizard.
        /// </summary>
        [MenuItem("Assets/Redux SDK/Tech Tree/Tech Tree Wizard", priority = KSP2UnityTools.MenuPriority)]
        public static void ShowWindow()
        {
            var window = CreateInstance<TechTreeWizard>();
            window.titleContent = new GUIContent("Tech Tree Wizard");
            window.minSize = new Vector2(460f, 360f);
            window.ShowUtility();
        }

        /// <summary>
        /// Builds a duplicate of a tree from its nodes, sharing nothing with them.
        /// </summary>
        /// <param name="nodes">The nodes to copy.</param>
        /// <param name="treeData">The tier names and start node of the tree they come from, which the duplicate takes,
        /// or null when it has none.</param>
        /// <returns>The duplicate, not yet saved as an asset.</returns>
        public static TechTreeAsset BuildDuplicate(IEnumerable<TechTreeNode> nodes, ReferenceTreeData treeData)
        {
            var tree = CreateInstance<TechTreeAsset>();
            tree.Nodes = nodes.Select(TechTreeEditing.CopyNode).ToList();
            if (treeData != null)
            {
                tree.Tiers = ReferenceTreeData.CopyTiers(treeData.Tiers);
                tree.StartNodeId = treeData.StartNodeId;
            }

            return tree;
        }

        private void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(SDKConfiguration.BasePath + UXML_PATH);
            if (visualTree == null)
            {
                root.Add(new Label("Failed to load TechTreeWizard.uxml"));
                return;
            }

            visualTree.CloneTree(root);
            Ksp2UnityToolsStyles.Apply(root, USS_PATH);

            _patchTab = root.Q<Button>("tab-patch");
            _treeTab = root.Q<Button>("tab-tree");
            _sourceField = root.Q<DropdownField>("source-field");
            _assetField = root.Q<ObjectField>("asset-field");
            _setField = root.Q<DropdownField>("set-field");
            _setFeatures = root.Q<Label>("set-features");
            _nameField = root.Q<TextField>("name-field");
            _statusLabel = root.Q<Label>("status-label");
            _createButton = root.Q<Button>("create-button");

            _patchTab.clicked += () => SetMakesPatch(true);
            _treeTab.clicked += () => SetMakesPatch(false);

            _sourceField.RegisterValueChangedCallback(_ => Refresh());

            _assetField.objectType = typeof(UnityEngine.Object);
            _assetField.allowSceneObjects = false;
            _assetField.RegisterValueChangedCallback(_ => Refresh());

            // A set whose patches leave the tree as the game ships it is no different from starting from game nodes
            List<TechTreeNode> gameNodes = TechTreeReference.LoadGameNodes(Array.Empty<string>());
            _sets = TechTreeCacheImport.FindCampaignSets(TechTreeCacheImport.GetCacheRoots(), TechTreeCacheImport.GetSavesRoot())
                .Where(set => !TechTreeCacheImport.HasNodes(set, gameNodes))
                .ToList();
            _setField.choices = _sets.Select(set => $"Campaign: {set.Campaign}").ToList();
            if (_sets.Count > 0)
            {
                _setField.SetValueWithoutNotify(_setField.choices[0]);
            }

            _setField.RegisterValueChangedCallback(_ => Refresh());
            _nameField.RegisterValueChangedCallback(_ => Refresh());
            root.Q<Button>("cancel-button").clicked += Close;
            _createButton.clicked += OnCreate;

            SetMakesPatch(true);
        }

        private void OnSelectionChange() => Refresh();

        private void SetMakesPatch(bool makesPatch)
        {
            _makesPatch = makesPatch;
            _patchTab.EnableInClassList("sdk-tab--active", makesPatch);
            _treeTab.EnableInClassList("sdk-tab--active", !makesPatch);

            // A patch needs a tree to change, so only a tree can start empty
            List<string> choices = makesPatch
                ? new List<string> { GAME_NODES, ASSET, CACHE }
                : new List<string> { EMPTY, GAME_NODES, ASSET, CACHE };
            _sourceField.choices = choices;
            if (!choices.Contains(_sourceField.value))
            {
                _sourceField.SetValueWithoutNotify(choices[0]);
            }

            Refresh();
        }

        private void Refresh()
        {
            if (_statusLabel == null)
                return;

            string source = _sourceField.value;
            _assetField.style.display = source == ASSET ? DisplayStyle.Flex : DisplayStyle.None;
            _setField.style.display = source == CACHE ? DisplayStyle.Flex : DisplayStyle.None;
            TechTreeCacheSet? set = SelectedSet();
            _setFeatures.style.display = source == CACHE ? DisplayStyle.Flex : DisplayStyle.None;
            _setFeatures.text = set is { } chosen
                ? $"{chosen.NodeNames.Count} nodes, cached for {(string.IsNullOrEmpty(chosen.Features) ? "an unnamed set" : chosen.Features)}"
                : "No campaign plays a changed tech tree.";

            string problem = FindProblem(source, set);
            _statusLabel.EnableInClassList(STATUS_ERROR_CLASS, problem != null);
            _statusLabel.text = problem ?? DescribeResult(source);
            _createButton.SetEnabled(problem == null);
        }

        private string FindProblem(string source, TechTreeCacheSet? set)
        {
            string name = _nameField.value?.Trim() ?? string.Empty;
            if (name.Length == 0)
                return "Name the new asset.";

            if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                return $"{name} cannot be a file name.";

            if (AssetDatabase.LoadMainAssetAtPath(AssetPath()) != null)
                return $"{AssetPath()} already exists.";

            if (source == ASSET && !IsUsableReference(_assetField.value))
                return "Pick a tech tree asset or a folder of tech node JSON.";

            if (source == CACHE && set == null)
                return "Pick a cached set.";

            return null;
        }

        private string DescribeResult(string source)
        {
            string what = _makesPatch ? "a tech tree patch" : "a tech tree";
            string result = $"Will create {what} at {AssetPath()}";
            return _makesPatch && source == CACHE ? $"{result}, with the set's nodes in {ReferenceFolder()}." : $"{result}.";
        }

        private void OnCreate()
        {
            try
            {
                TechTreeAsset created = _makesPatch ? CreatePatch() : CreateTree();
                if (created == null)
                    return;

                Selection.activeObject = created;
                TechTreeEditorWindow.OpenFor(created);
                Close();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                EditorUtility.DisplayDialog("Tech Tree Wizard", ex.Message, "OK");
            }
        }

        private TechTreeAsset CreatePatch()
        {
            var patch = CreateInstance<TechTreePatchAsset>();
            switch (_sourceField.value)
            {
                case GAME_NODES:
                    patch.ReferenceSource = TechNodeReferenceSource.GameNodes;
                    break;
                case ASSET:
                    patch.ReferenceSource = TechNodeReferenceSource.Asset;
                    patch.Reference = _assetField.value;
                    break;
                default:
                    patch.ReferenceSource = TechNodeReferenceSource.Asset;
                    patch.Reference = WriteReferenceFolder(SelectedSet().Value);
                    break;
            }

            AssetDatabase.CreateAsset(patch, AssetPath());
            AssetDatabase.SaveAssets();
            return patch;
        }

        private TechTreeAsset CreateTree()
        {
            List<TechTreeNode> nodes;
            ReferenceTreeData treeData = null;
            switch (_sourceField.value)
            {
                case EMPTY:
                    nodes = null;
                    break;
                case GAME_NODES:
                    nodes = TechTreeReference.LoadGameNodes(Array.Empty<string>());
                    treeData = TechTreeReference.LoadGameTreeData(Array.Empty<string>());
                    break;
                case ASSET when _assetField.value is TechTreeAsset tree:
                    nodes = tree.Nodes;
                    treeData = ReferenceTreeData.FromTree(tree);
                    break;
                case ASSET:
                    string folder = AssetDatabase.GetAssetPath(_assetField.value);
                    nodes = TechTreeReference.LoadFolder(folder);
                    treeData = TechTreeReference.LoadTreeDataFolder(folder);
                    break;
                default:
                    TechTreeCacheSet set = SelectedSet().Value;
                    nodes = TechTreeReference.FromJson(TechTreeCacheImport.ReadNodes(set).Values);
                    string json = SetTreeDataJson(set);
                    treeData = json != null ? TechTreeReference.TreeDataFromJson(new[] { json }) : null;
                    break;
            }

            if (nodes is { Count: 0 })
            {
                EditorUtility.DisplayDialog("Tech Tree Wizard", "The source has no tech nodes.", "OK");
                return null;
            }

            TechTreeAsset created = nodes == null ? CreateInstance<TechTreeAsset>() : BuildDuplicate(nodes, treeData);
            AssetDatabase.CreateAsset(created, AssetPath());
            AssetDatabase.SaveAssets();
            return created;
        }

        // The set's nodes and tree data as a JSON folder of the project, which the patch keeps as its reference. Without
        // the tree data the patch would take the reference for one that has none, and give the set a second.
        private UnityEngine.Object WriteReferenceFolder(TechTreeCacheSet set)
        {
            string folder = ReferenceFolder();
            Directory.CreateDirectory(folder);
            foreach (KeyValuePair<string, string> node in TechTreeCacheImport.ReadNodes(set))
            {
                File.WriteAllText($"{folder}/{node.Key}.json", node.Value);
            }

            string treeData = SetTreeDataJson(set);
            if (treeData != null)
            {
                File.WriteAllText($"{folder}/{_nameField.value?.Trim()}{TechTreeBake.TREE_DATA_FILE_SUFFIX}", treeData);
            }

            AssetDatabase.ImportAsset(folder, ImportAssetOptions.ImportRecursive);
            return AssetDatabase.LoadMainAssetAtPath(folder);
        }

        // The tree data a set loads: the cache's own when a patch in the set changed it, otherwise the game's
        private static string SetTreeDataJson(TechTreeCacheSet set) =>
            TechTreeCacheImport.ReadTreeData(set)
            ?? TechTreeReference.LoadGameTreeDataJson(Array.Empty<string>()).FirstOrDefault();

        private TechTreeCacheSet? SelectedSet()
        {
            int index = _setField?.index ?? -1;
            return index >= 0 && index < _sets.Count ? _sets[index] : null;
        }

        private string AssetPath() => $"{TargetFolder()}/{_nameField.value?.Trim()}.asset";

        private string ReferenceFolder() => $"{TargetFolder()}/{_nameField.value?.Trim()}_Reference";

        // The folder selected in the Project window, or the folder of the selected asset
        private static string TargetFolder()
        {
            string path = Selection.activeObject != null ? AssetDatabase.GetAssetPath(Selection.activeObject) : null;
            if (string.IsNullOrEmpty(path))
                return "Assets";

            return AssetDatabase.IsValidFolder(path) ? path : Path.GetDirectoryName(path)?.Replace('\\', '/');
        }

        private static bool IsUsableReference(UnityEngine.Object reference) =>
            reference is TechTreeAsset || (reference != null && AssetDatabase.IsValidFolder(AssetDatabase.GetAssetPath(reference)));
    }
}
