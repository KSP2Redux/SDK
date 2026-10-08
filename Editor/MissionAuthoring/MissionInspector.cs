using Ksp2UnityTools.Editor.MissionAuthoring.Windows;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.MissionAuthoring
{
    /// <summary>
    /// CustomEditor for <see cref="Mission" />.
    /// </summary>
    /// <remarks>
    /// Exposes Bake to JSON and Open in Editor above Unity's default property tree. The tree
    /// below is intentionally placeholder.
    /// </remarks>
    [CustomEditor(typeof(Mission))]
    public class MissionInspector : UnityEditor.Editor
    {
        /// <inheritdoc />
        public override VisualElement CreateInspectorGUI()
        {
            var mission = (Mission)target;
            var root = new VisualElement();
            Ksp2UnityToolsStyles.Apply(root, "/Assets/Windows/DataEditors.uss");

            var row = new VisualElement();
            row.AddToClassList("data-editor-inline-row");
            row.Add(new Button(() => MissionAuthoringActions.BakeToJson(mission)) { text = "Bake to JSON" });
            row.Add(new Button(() => MissionEditorWindow.OpenFor(mission)) { text = "Open in Editor" });
            root.Add(row);

            var iter = serializedObject.GetIterator();
            bool enterChildren = true;
            while (iter.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (iter.propertyPath == "m_Script") continue;
                var pf = new PropertyField(iter.Copy());
                pf.Bind(serializedObject);
                root.Add(pf);
            }
            return root;
        }
    }
}
