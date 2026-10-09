using System;
using System.Linq;
using Ksp2UnityTools.Editor.Localization.Export;
using Ksp2UnityTools.Editor.TechTreeAuthoring.Widgets;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.TechTreeAuthoring
{
    /// <summary>
    /// The tech tree editor: one <see cref="TechTreeAsset" /> drawn as the R&amp;D Center draws it, with the selected
    /// node's details beside it.
    /// </summary>
    /// <remarks>
    /// Opens on double-click of a tech tree asset, from the inspector, or from the menu with one selected. The canvas
    /// redraws whenever the asset changes, so edits made in the inspector show at once.
    /// </remarks>
    public class TechTreeEditorWindow : EditorWindow
    {
        private const string UXML_PATH = "/Assets/Windows/TechTreeAuthoring/TechTreeEditorWindow.uxml";

        private const string USS_PATH = "/Assets/Windows/TechTreeAuthoring/TechTreeEditorWindow.uss";

        // Survives domain reload through Unity's serialization
        [SerializeField] private TechTreeAsset _tree;

        private SerializedObject _serializedTree;

        private TechTreeCanvas _canvas;

        private ScrollView _sidePanel;

        private Label _zoomLabel;

        [OnOpenAsset]
        private static bool OnOpenTechTreeAsset(EntityId assetId, int line)
        {
            string path = AssetDatabase.GetAssetPath(assetId);
            if (string.IsNullOrEmpty(path))
                return false;

            var tree = AssetDatabase.LoadAssetAtPath<TechTreeAsset>(path);
            if (tree == null)
                return false;

            OpenFor(tree);
            return true;
        }

        [MenuItem("Modding/Tech Tree Authoring/Tech Tree Editor")]
        private static void OpenFromMenu()
        {
            if (Selection.activeObject is TechTreeAsset tree)
            {
                OpenFor(tree);
                return;
            }

            EditorUtility.DisplayDialog("Tech Tree Editor", "Select a tech tree asset in the Project window first.", "OK");
        }

        /// <summary>
        /// Focuses the editor window showing a tree, or opens a new one for it.
        /// </summary>
        /// <param name="tree">The tree to edit.</param>
        public static void OpenFor(TechTreeAsset tree)
        {
            TechTreeEditorWindow existing = Resources.FindObjectsOfTypeAll<TechTreeEditorWindow>()
                .FirstOrDefault(window => window._tree == tree);
            if (existing != null)
            {
                existing.Focus();
                return;
            }

            var window = CreateInstance<TechTreeEditorWindow>();
            window._tree = tree;
            window.Show();
        }

        private void CreateGUI()
        {
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(SDKConfiguration.BasePath + UXML_PATH);
            if (visualTree == null)
            {
                rootVisualElement.Add(new Label("Failed to load TechTreeEditorWindow.uxml"));
                return;
            }

            visualTree.CloneTree(rootVisualElement);
            rootVisualElement.style.flexGrow = 1f;
            Ksp2UnityToolsStyles.Apply(rootVisualElement, USS_PATH);

            _sidePanel = rootVisualElement.Q<ScrollView>("side-panel");
            _zoomLabel = rootVisualElement.Q<Label>("zoom-label");
            _canvas = new TechTreeCanvas();
            _canvas.OnSelectionChanged += _ => RefreshSidePanel();
            _canvas.OnZoomChanged += zoom => _zoomLabel.text = $"{Mathf.RoundToInt(zoom * 100f)}%";
            rootVisualElement.Q("canvas-host").Add(_canvas);

            BindToggle("grid-toggle", value => _canvas.ShowsGrid = value);
            BindToggle("pages-toggle", value => _canvas.ShowsPages = value);
            BindToggle("band-toggle", value => _canvas.ShowsBand = value);
            BindButton("zoom-in", () => _canvas.ZoomBy(1.2f));
            BindButton("zoom-out", () => _canvas.ZoomBy(1f / 1.2f));
            BindButton("frame-all", () => _canvas.FrameAll());
            BindButton("bake-json", () => TechTreeBaker.Bake(_tree));
            BindButton("export-localizations", () => LocExportFlow.RunForAsset(_tree));

            Undo.undoRedoPerformed += OnUndoRedo;
            Bind();
        }

        private void OnDestroy()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
        }

        private void Bind()
        {
            titleContent = new GUIContent(_tree != null ? _tree.name : "Tech Tree Editor");
            _canvas.SetTree(_tree);
            if (_tree == null)
                return;

            _serializedTree = new SerializedObject(_tree);
            var layerHost = rootVisualElement.Q("layer-host");
            layerHost.Clear();
            var layerField = new TechLayerField(_serializedTree.FindProperty(nameof(TechTreeAsset.Layer)), "Layer");
            layerHost.Add(layerField);
            layerHost.Bind(_serializedTree);

            // Any change to the asset, from this window or the inspector, redraws the tree
            rootVisualElement.TrackSerializedObjectValue(_serializedTree, _ =>
            {
                _canvas.Rebuild();
                RefreshSidePanel();
            });
            RefreshSidePanel();
        }

        private void OnUndoRedo()
        {
            if (_tree == null || _canvas == null)
                return;

            _serializedTree?.Update();
            _canvas.Rebuild();
            RefreshSidePanel();
        }

        // The selected node's details, or the tree's tiers when nothing is selected
        private void RefreshSidePanel()
        {
            _sidePanel.Clear();
            if (_tree == null)
                return;

            TechTreeNode node = _tree.Nodes.FirstOrDefault(x => x.Data != null && x.Data.ID == _canvas.SelectedId);
            if (node == null)
            {
                AddPanelLabel(_tree.name, "tech-tree-panel__title");
                var tiers = new PropertyField(_serializedTree.FindProperty(nameof(TechTreeAsset.Tiers)));
                tiers.Bind(_serializedTree);
                _sidePanel.Add(tiers);
                return;
            }

            AddPanelLabel(string.IsNullOrEmpty(node.EnglishName) ? node.Data.ID : node.EnglishName, "tech-tree-panel__title");
            AddPanelLabel(node.Data.ID, "tech-tree-panel__id");
            AddPanelLabel($"Science: {node.Data.RequiredSciencePoints}", "tech-tree-panel__row");
            AddPanelList("Prerequisites", node.Data.RequiredTechNodeIDs);
            AddPanelList("Parts", node.Data.UnlockedPartsIDs);
            AddPanelList("Missions", node.Data.RequiredMissionIDs);
        }

        private void AddPanelList(string heading, string[] items)
        {
            AddPanelLabel(heading, "tech-tree-panel__heading");
            string[] shown = items ?? Array.Empty<string>();
            if (shown.Length == 0)
            {
                AddPanelLabel("None", "tech-tree-panel__row");
                return;
            }

            foreach (string item in shown)
            {
                AddPanelLabel(item, "tech-tree-panel__row");
            }
        }

        private void AddPanelLabel(string text, string className)
        {
            var label = new Label(text);
            label.AddToClassList(className);
            _sidePanel.Add(label);
        }

        private void BindToggle(string elementName, Action<bool> onChanged)
        {
            var toggle = rootVisualElement.Q<Toggle>(elementName);
            toggle?.RegisterValueChangedCallback(evt => onChanged(evt.newValue));
        }

        private void BindButton(string elementName, Action onClicked)
        {
            var button = rootVisualElement.Q<Button>(elementName);
            if (button != null)
            {
                button.clicked += onClicked;
            }
        }
    }
}
