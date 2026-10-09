using Ksp2UnityTools.Editor.TechTreeAuthoring.Widgets;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.TechTreeAuthoring
{
    /// <summary>
    /// The inspector of a <see cref="TechTreeAsset" /> asset: its layer, tiers and nodes.
    /// </summary>
    [CustomEditor(typeof(TechTreeAsset))]
    public class TechTreeInspector : UnityEditor.Editor
    {
        /// <inheritdoc />
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            Ksp2UnityToolsStyles.Apply(root);
            root.Add(new TechLayerField(serializedObject.FindProperty(nameof(TechTreeAsset.Layer)), "Layer"));
            root.Add(new PropertyField(serializedObject.FindProperty(nameof(TechTreeAsset.Tiers))));
            root.Add(new PropertyField(serializedObject.FindProperty(nameof(TechTreeAsset.Nodes))));
            root.Bind(serializedObject);
            return root;
        }
    }
}
