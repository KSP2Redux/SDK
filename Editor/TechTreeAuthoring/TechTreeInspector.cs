using Ksp2UnityTools.Editor.API;
using Ksp2UnityTools.Editor.Localization.Export;
using Ksp2UnityTools.Editor.Modding;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.TechTreeAuthoring
{
    /// <summary>
    /// The inspector of a <see cref="TechTreeAsset" /> asset: bake and export actions, then its features, tiers and
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
            Ksp2UnityToolsStyles.Apply(root, "/Assets/Windows/DataEditors.uss");

            var actions = new VisualElement();
            actions.AddToClassList("sdk-button-row");
            actions.Add(new Button(() => TechTreeEditorWindow.OpenFor(tree)) { text = "Open Editor" });
            actions.Add(new Button(() => TechTreeBaker.Bake(tree)) { text = "Bake JSON" });
            actions.Add(new Button(() => LocExportFlow.RunForAsset(tree)) { text = "Export Localizations" });
            root.Add(actions);

            // The baked files are what the game loads, so their labels carry the tree's features
            var features = new VisualElement();
            features.AddToClassList("data-editor-section");
            var header = new Label("Features");
            header.AddToClassList("data-editor-section-header");
            features.Add(header);
            features.Add(AssetFeaturesSection.Build(() => tree.BakedFiles, KSP2UnityTools.FindParentMod(tree)));
            root.Add(features);

            root.Add(new PropertyField(serializedObject.FindProperty(nameof(TechTreeAsset.Tiers))));
            root.Add(new PropertyField(serializedObject.FindProperty(nameof(TechTreeAsset.Nodes))));
            root.Bind(serializedObject);
            return root;
        }
    }
}
