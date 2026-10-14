using System;
using System.Collections.Generic;
using System.Linq;
using Ksp2UnityTools.Editor.Localization.Export;
using Ksp2UnityTools.Editor.Widgets;
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

        private TechTreeNodePanel _panel;

        private Label _zoomLabel;

        [OnOpenAsset]
        private static bool OnOpenTechTreeAsset(EntityId assetId, int line)
        {
            string path = AssetDatabase.GetAssetPath(assetId);
            if (string.IsNullOrEmpty(path))
                return false;

            // A node asset is edited in its inspector
            var tree = AssetDatabase.LoadAssetAtPath<TechTreeAsset>(path);
            if (tree == null || tree is TechNodeAsset)
                return false;

            OpenFor(tree);
            return true;
        }

        [MenuItem("Modding/Tech Tree Authoring/Tech Tree Editor")]
        private static void OpenFromMenu()
        {
            if (Selection.activeObject is TechTreeAsset tree and not TechNodeAsset)
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
            Ksp2UnityToolsStyles.Apply(rootVisualElement, "/Assets/Windows/DataEditors.uss", USS_PATH);

            _sidePanel = rootVisualElement.Q<ScrollView>("side-panel");
            _zoomLabel = rootVisualElement.Q<Label>("zoom-label");
            _canvas = new TechTreeCanvas();
            _canvas.OnSelectionChanged += _ => RefreshSidePanel();
            _canvas.OnZoomChanged += zoom => _zoomLabel.text = $"{Mathf.RoundToInt(zoom * 100f)}%";
            _canvas.OnTreeEdited += RefreshSidePanel;
            _canvas.OnRefused += message => ShowNotification(new GUIContent(message));
            rootVisualElement.Q("canvas-host").Add(_canvas);

            _panel = new TechTreeNodePanel();
            _panel.OnNodeRequested += nodeId =>
            {
                _canvas.Select(nodeId);
                _canvas.CentreOn(nodeId);
            };
            _panel.OnTreeEdited += () => _canvas.Rebuild();
            _panel.OnRefused += message => ShowNotification(new GUIContent(message));

            // Selecting the new ID before the rebuild keeps the renamed node selected
            _panel.OnNodeRenamed += (_, newId) =>
            {
                _canvas.Select(newId);
                _canvas.Rebuild();
            };
            _sidePanel.Add(_panel);

            var find = new AutocompleteField(string.Empty, null, FindSuggestions, FindNode) { tooltip = "Find node" };
            rootVisualElement.Q("find-host").Add(find);

            BindToggle("snap-toggle", value => _canvas.SnapsToGrid = value);
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

            // Any change to the asset, from this window or the inspector, redraws the tree
            rootVisualElement.TrackSerializedObjectValue(_serializedTree, _ =>
            {
                _canvas.Rebuild();
                if (!IsEditingInPanel())
                {
                    RefreshSidePanel();
                }
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

        // The selected node's options, or the tree's settings when nothing is selected
        private void RefreshSidePanel() => _panel.Show(_tree, _serializedTree, _canvas.SelectedId);

        // An edit typed into the panel must not rebuild the field being typed in
        private bool IsEditingInPanel() =>
            rootVisualElement.focusController?.focusedElement is VisualElement focused && _sidePanel.Contains(focused);

        // Finds by exact ID first, then by name
        private void FindNode(string query)
        {
            if (_tree == null || string.IsNullOrWhiteSpace(query))
                return;

            TechTreeNode node = TechTreeEditing.Find(_tree, query) ?? _tree.Nodes.FirstOrDefault(candidate =>
                string.Equals(candidate.EnglishName, query, StringComparison.OrdinalIgnoreCase));
            if (node == null)
                return;

            _canvas.Select(node.Data.ID);
            _canvas.CentreOn(node.Data.ID);
        }

        private IEnumerable<string> FindSuggestions() =>
            _tree == null
                ? Enumerable.Empty<string>()
                : _tree.Nodes
                    .Where(node => !string.IsNullOrEmpty(node.Data?.ID))
                    .SelectMany(node => string.IsNullOrEmpty(node.EnglishName)
                        ? new[] { node.Data.ID }
                        : new[] { node.EnglishName, node.Data.ID })
                    .Distinct();

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
