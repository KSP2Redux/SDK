using Ksp2UnityTools.Editor.Localization.Export;
using Ksp2UnityTools.Editor.TechTreeAuthoring.Widgets;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.TechTreeAuthoring
{
    /// <summary>
    /// The inspector of a <see cref="TechTreeAsset" /> asset: bake and export actions, then its layer, tiers and
    /// nodes.
    /// </summary>
    [CustomEditor(typeof(TechTreeAsset))]
    public class TechTreeInspector : UnityEditor.Editor
    {
        /// <inheritdoc />
        public override VisualElement CreateInspectorGUI()
        {
            var tree = (TechTreeAsset)target;
            var root = new VisualElement();
            Ksp2UnityToolsStyles.Apply(root);

            var actions = new VisualElement();
            actions.AddToClassList("sdk-button-row");
            actions.Add(new Button(() => TechTreeEditorWindow.OpenFor(tree)) { text = "Open Editor" });
            actions.Add(new Button(() => TechTreeBaker.Bake(tree)) { text = "Bake JSON" });
            actions.Add(new Button(() => LocExportFlow.RunForAsset(tree)) { text = "Export Localizations" });
            root.Add(actions);

            root.Add(new TechLayerField(serializedObject.FindProperty(nameof(TechTreeAsset.Layer)), "Layer"));
            root.Add(new PropertyField(serializedObject.FindProperty(nameof(TechTreeAsset.Tiers))));
            root.Add(new PropertyField(serializedObject.FindProperty(nameof(TechTreeAsset.Nodes))));
            root.Bind(serializedObject);
            return root;
        }
    }
}
