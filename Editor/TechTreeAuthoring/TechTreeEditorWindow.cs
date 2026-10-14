using System;
using System.Collections.Generic;
using System.Linq;
using Ksp2UnityTools.Editor.Localization.Export;
using Ksp2UnityTools.Editor.Modding;
using Ksp2UnityTools.Editor.TechTreeAuthoring.Validation;
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

        // The tree a patch changes, drawn read only beside the patch's own nodes, or empty for a tree
        private List<TechTreeNode> _reference = new();

        // The tiers and start node of the tree a patch changes, or null for a tree or a reference without any
        private ReferenceTreeData _referenceTreeData;

        private Button _readinessChip;

        private Button _featuresChip;

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
        /// Focuses the editor window showing a tree, or opens a new one for it as a tab beside the Scene view.
        /// </summary>
        /// <param name="tree">The tree to edit.</param>
        /// <returns>The window.</returns>
        public static TechTreeEditorWindow OpenFor(TechTreeAsset tree)
        {
            TechTreeEditorWindow existing = Resources.FindObjectsOfTypeAll<TechTreeEditorWindow>()
                .FirstOrDefault(window => window._tree == tree);
            if (existing != null)
            {
                existing.Focus();
                return existing;
            }

            var created = CreateWindow<TechTreeEditorWindow>(tree.name, typeof(SceneView));
            created._tree = tree;

            // The window may have built its UI before it knew its tree
            if (created._canvas != null)
            {
                created.Bind();
            }

            return created;
        }

        /// <summary>
        /// Selects a node and centres the view on it.
        /// </summary>
        /// <param name="nodeId">The node's ID.</param>
        public void FocusNode(string nodeId)
        {
            if (_canvas == null)
                return;

            _canvas.Select(nodeId);
            _canvas.CentreOn(nodeId);
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
            rootVisualElement.AddToClassList("tech-tree-window");
            Ksp2UnityToolsStyles.Apply(rootVisualElement, "/Assets/Windows/DataEditors.uss", USS_PATH);

            _sidePanel = rootVisualElement.Q<ScrollView>("side-panel");
            _zoomLabel = rootVisualElement.Q<Label>("zoom-label");
            _canvas = new TechTreeCanvas();
            _canvas.OnSelectionChanged += _ => RefreshSidePanel();
            _canvas.OnZoomChanged += zoom => _zoomLabel.text = $"{Mathf.RoundToInt(zoom * 100f)}%";
            _canvas.OnTreeEdited += RefreshSidePanel;
            _canvas.OnRefused += message => ShowNotification(new GUIContent(message));
            _canvas.OnRebuilt += Revalidate;

            var cursorLabel = rootVisualElement.Q<Label>("cursor-label");
            _canvas.OnCursorMoved += position =>
                cursorLabel.text = $"x {Mathf.RoundToInt(position.x)}  y {Mathf.RoundToInt(position.y)}";

            // First, so the controls in the corners draw over it
            rootVisualElement.Q("canvas-host").Insert(0, _canvas);

            _panel = new TechTreeNodePanel();
            _panel.OnNodeRequested += nodeId =>
            {
                _canvas.Select(nodeId);
                _canvas.CentreOn(nodeId);
            };
            _panel.OnTreeEdited += () => _canvas.Rebuild();
            _panel.OnRefused += message => ShowNotification(new GUIContent(message));
            _panel.OnReferenceChanged += LoadReference;

            // Selecting the new ID before the rebuild keeps the renamed node selected
            _panel.OnNodeRenamed += (_, newId) =>
            {
                _canvas.Select(newId);
                _canvas.Rebuild();
            };
            _sidePanel.Add(_panel);

            _readinessChip = rootVisualElement.Q<Button>("readiness-chip-valid");
            _readinessChip.tooltip = "Open the Tech Tree Validation Report.";
            _readinessChip.clicked += () => TechTreeValidationReportWindow.Open(_tree, _canvas.SnapsToGrid);

            var find = new AutocompleteField(string.Empty, null, FindSuggestions, FindNode);
            find.Q<TextField>().textEdition.placeholder = "Find node";
            rootVisualElement.Q("find-host").Add(find);

            _featuresChip = rootVisualElement.Q<Button>("features-chip");
            _featuresChip.clicked += ShowTreeSettings;

            BindPill("pill-snap", () => _canvas.SnapsToGrid, value =>
            {
                _canvas.SnapsToGrid = value;
                Revalidate();
            });
            BindPill("pill-grid", () => _canvas.ShowsGrid, value => _canvas.ShowsGrid = value);
            BindPill("pill-pages", () => _canvas.ShowsPages, value => _canvas.ShowsPages = value);
            BindPill("pill-band", () => _canvas.ShowsBand, value => _canvas.ShowsBand = value);
            BindModeTabs();
            BindButton("chip-zoom-in", () => _canvas.ZoomBy(1.2f));
            BindButton("chip-zoom-out", () => _canvas.ZoomBy(1f / 1.2f));
            BindButton("chip-frame-all", () => _canvas.FrameAll());
            BindButton("chip-bake-json", () => TechTreeBaker.Bake(_tree));
            BindButton("chip-export-localizations", () => LocExportFlow.RunForAsset(_tree));

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
            if (_tree != null)
            {
                rootVisualElement.Q<Button>("chip-bake-json").text = TechTreeBaker.GetBakeLabel(_tree);
            }

            LoadReference();
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
        private void RefreshSidePanel() =>
            _panel.Show(_tree, _serializedTree, _canvas.SelectedId, _reference, _referenceTreeData);

        // A patch is drawn over the tree it changes, a tree on its own
        private void LoadReference()
        {
            _reference = _tree is BaseReferencedTechTreeAsset referenced
                ? TechTreeReference.Load(referenced)
                : new List<TechTreeNode>();
            _referenceTreeData = _tree is TechTreePatchAsset patch ? TechTreeReference.LoadTreeData(patch) : null;
            _canvas.SetTree(_tree, _reference);
            if (_serializedTree != null)
            {
                RefreshSidePanel();
            }
        }

        // The chip and the node badges follow every redraw
        private void Revalidate()
        {
            List<TechTreeIssue> issues = TechTreeValidationReport.Run(
                new TechTreeValidationContext(_tree, _reference, _canvas.SnapsToGrid, _referenceTreeData)
            );
            TechTreeValidationReportWindow.ShowOnChip(_readinessChip, issues);
            _canvas.SetBadges(TechTreeValidationReportWindow.WorstByNode(issues));
            RefreshFeaturesChip();
        }

        // An edit typed into the panel must not rebuild the field being typed in
        private bool IsEditingInPanel() =>
            rootVisualElement.focusController?.focusedElement is VisualElement focused && _sidePanel.Contains(focused);

        // Finds by exact ID first, then by name, among the tree's nodes and the reference's drawn beside them
        private void FindNode(string query)
        {
            if (_tree == null || string.IsNullOrWhiteSpace(query))
                return;

            TechTreeNode node = TechTreeEditing.FindAny(_tree, _reference, query) ?? SearchableNodes().FirstOrDefault(candidate =>
                string.Equals(candidate.EnglishName, query, StringComparison.OrdinalIgnoreCase));
            if (node == null)
                return;

            _canvas.Select(node.Data.ID);
            _canvas.CentreOn(node.Data.ID);
        }

        private IEnumerable<TechTreeNode> SearchableNodes() => _tree.Nodes.Concat(_reference);

        private IEnumerable<string> FindSuggestions() =>
            _tree == null
                ? Enumerable.Empty<string>()
                : SearchableNodes()
                    .Where(node => !string.IsNullOrEmpty(node.Data?.ID))
                    .SelectMany(node => string.IsNullOrEmpty(node.EnglishName)
                        ? new[] { node.Data.ID }
                        : new[] { node.EnglishName, node.Data.ID })
                    .Distinct();

        // The features a mod's tree needs no label for are its mod's own, so a tree with none names the mod
        private void RefreshFeaturesChip()
        {
            if (_tree == null)
            {
                _featuresChip.text = "Features";
                return;
            }

            // A patch keeps its features itself, since its Lua has no labels to carry them
            string ownerId = TechTreeBake.GetOwnerId(_tree);
            (List<string> features, List<string> noFeatures) = _tree is TechTreePatchAsset patch
                ? (patch.Features.Where(id => !string.IsNullOrWhiteSpace(id)).ToList(),
                    patch.ExcludedFeatures.Where(id => !string.IsNullOrWhiteSpace(id)).ToList())
                : AssetFeatureLabels.ReadAll(
                    _tree.BakedFiles.Select(AssetFeatureLabels.EntryOf).Where(entry => entry != null).Select(entry => entry.labels),
                    ownerId
                );
            IEnumerable<string> shown = features.Concat(noFeatures.Select(feature => "not " + feature));
            _featuresChip.text = $"Features  {(features.Count + noFeatures.Count == 0 ? ownerId : string.Join(", ", shown))}";
        }

        // The tree's settings, where its features are edited, show while nothing is selected
        private void ShowTreeSettings()
        {
            _canvas.Select(null);
            RefreshSidePanel();
        }

        // Authoring and Preview unlocks are one choice, shown as the SDK's tab bar
        private void BindModeTabs()
        {
            var authoring = rootVisualElement.Q<Button>("tab-authoring");
            var preview = rootVisualElement.Q<Button>("tab-preview");
            void Show(bool previews)
            {
                _canvas.PreviewsUnlocks = previews;
                authoring.EnableInClassList("sdk-tab--active", !previews);
                preview.EnableInClassList("sdk-tab--active", previews);
            }

            authoring.clicked += () => Show(false);
            preview.clicked += () => Show(true);
            Show(false);
        }

        // A pill shows its setting as is-active and flips it on click, as the part editor's gizmo pills do
        private void BindPill(string elementName, Func<bool> getter, Action<bool> setter)
        {
            var pill = rootVisualElement.Q<Button>(elementName);
            if (pill == null)
                return;

            pill.EnableInClassList("is-active", getter());
            pill.clicked += () =>
            {
                bool newValue = !getter();
                setter(newValue);
                pill.EnableInClassList("is-active", newValue);
            };
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
