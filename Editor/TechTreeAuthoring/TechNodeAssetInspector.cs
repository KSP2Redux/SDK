using System.Collections.Generic;
using Ksp2UnityTools.Editor.API;
using Ksp2UnityTools.Editor.Localization.Export;
using Ksp2UnityTools.Editor.Modding;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.TechTreeAuthoring
{
    /// <summary>
    /// The inspector of a <see cref="TechNodeAsset" />: bake and export actions, its features and reference tree, the
    /// reference tree drawn with the node in it, and the node's options.
    /// </summary>
    /// <remarks>
    /// The canvas moves and links the node as the tech tree editor does, and draws the reference tree read only. The
    /// node can require reference nodes, and nothing in the reference tree changes.
    /// </remarks>
    [CustomEditor(typeof(TechNodeAsset))]
    public class TechNodeAssetInspector : UnityEditor.Editor
    {
        private const string WINDOW_USS_PATH = "/Assets/Windows/TechTreeAuthoring/TechTreeEditorWindow.uss";

        private const float CANVAS_HEIGHT = 420f;

        private TechNodeAsset _asset;

        private TechTreeCanvas _canvas;

        private TechTreeNodePanel _panel;

        private List<TechTreeNode> _reference = new();

        /// <inheritdoc />
        public override VisualElement CreateInspectorGUI()
        {
            _asset = (TechNodeAsset)target;
            EnsureNode();

            var root = new VisualElement();
            Ksp2UnityToolsStyles.Apply(root, "/Assets/Windows/DataEditors.uss", WINDOW_USS_PATH);

            var actions = new VisualElement();
            actions.AddToClassList("sdk-button-row");
            actions.Add(new Button(() => TechTreeBaker.Bake(_asset)) { text = "Bake JSON" });
            actions.Add(new Button(() => LocExportFlow.RunForAsset(_asset)) { text = "Export Localizations" });
            root.Add(actions);

            root.Add(BuildFeatures());
            root.Add(BuildReference());

            _canvas = new TechTreeCanvas { CanAddAndRemoveNodes = false, FrameTarget = _asset.Node.Data.ID };
            _canvas.style.height = CANVAS_HEIGHT;
            _canvas.style.flexGrow = 0f;
            _canvas.OnRefused += Notify;
            _canvas.OnTreeEdited += RefreshPanel;
            root.Add(_canvas);

            _panel = new TechTreeNodePanel();
            _panel.OnNodeRequested += nodeId =>
            {
                _canvas.Select(nodeId);
                _canvas.CentreOn(nodeId);
            };
            _panel.OnTreeEdited += () => _canvas.Rebuild();
            _panel.OnRefused += Notify;
            _panel.OnNodeRenamed += (_, newId) =>
            {
                _canvas.FrameTarget = newId;
                _canvas.Select(newId);
                _canvas.Rebuild();
            };
            root.Add(_panel);

            LoadReference();

            // An edit typed into the panel must not rebuild the field being typed in
            root.TrackSerializedObjectValue(serializedObject, _ =>
            {
                _canvas.Rebuild();
                if (!(root.focusController?.focusedElement is VisualElement focused && _panel.Contains(focused)))
                {
                    RefreshPanel();
                }
            });
            Undo.undoRedoPerformed += OnUndoRedo;
            root.RegisterCallback<DetachFromPanelEvent>(_ => Undo.undoRedoPerformed -= OnUndoRedo);
            return root;
        }

        private VisualElement BuildFeatures()
        {
            // The baked file is what the game loads, so its labels carry the node's features
            var features = new VisualElement();
            features.AddToClassList("data-editor-section");
            var header = new Label("Features");
            header.AddToClassList("data-editor-section-header");
            features.Add(header);
            features.Add(AssetFeaturesSection.Build(() => _asset.BakedFiles, KSP2UnityTools.FindParentMod(_asset)));
            return features;
        }

        private VisualElement BuildReference()
        {
            var section = new VisualElement();
            section.AddToClassList("data-editor-section");
            var header = new Label("Reference Tree");
            header.AddToClassList("data-editor-section-header");
            section.Add(header);

            SerializedProperty sourceProperty = serializedObject.FindProperty(nameof(TechNodeAsset.ReferenceSource));
            var source = new PropertyField(sourceProperty, "Source");
            section.Add(source);

            var reference = new ObjectField("Tree or folder")
            {
                objectType = typeof(Object),
                allowSceneObjects = false,
                bindingPath = nameof(TechNodeAsset.Reference),
            };
            section.Add(reference);

            void ShowReferenceField() =>
                reference.style.display = _asset.ReferenceSource == TechNodeReferenceSource.Asset
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;

            ShowReferenceField();
            section.TrackPropertyValue(sourceProperty, _ =>
            {
                ShowReferenceField();
                LoadReference();
            });
            section.TrackPropertyValue(serializedObject.FindProperty(nameof(TechNodeAsset.Reference)), _ => LoadReference());
            section.Bind(serializedObject);
            return section;
        }

        private void LoadReference()
        {
            _reference = TechTreeReference.Load(_asset);
            _canvas.SetTree(_asset, _reference);
            _canvas.Select(_asset.Node.Data.ID);
            RefreshPanel();
        }

        private void RefreshPanel() => _panel.Show(_asset, serializedObject, _asset.Node?.Data.ID, _reference);

        private void OnUndoRedo()
        {
            if (_asset == null || _canvas == null)
                return;

            serializedObject.Update();
            _canvas.Rebuild();
            RefreshPanel();
        }

        // A node asset always holds its one node, even one made by hand rather than from the menu
        private void EnsureNode()
        {
            if (_asset.Node != null)
                return;

            Undo.RecordObject(_asset, "Add Tech Node");
            TechTreeEditing.AddNode(_asset, Vector2.zero);
            EditorUtility.SetDirty(_asset);
        }

        private static void Notify(string message) => EditorWindow.focusedWindow?.ShowNotification(new GUIContent(message));
    }
}
