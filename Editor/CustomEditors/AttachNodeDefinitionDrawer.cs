using KSP.Sim.Definitions;
using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.CustomEditors
{
    [CustomPropertyDrawer(typeof(AttachNodeDefinition))]
    public class AttachNodeDefinitionDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return PartSizeInspectorFields.GetFoldoutPropertyHeight(
                property,
                PartSizeInspectorFields.AttachNodeSizeFieldName,
                PartSizeInspectorFields.AttachNodeSizeKeyFieldName
            );
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            PartSizeInspectorFields.DrawFoldoutProperty(
                position,
                property,
                label,
                PartSizeInspectorFields.AttachNodeSizeFieldName,
                PartSizeInspectorFields.AttachNodeSizeKeyFieldName,
                "Node Size",
                PartSizeInspectorFields.LegacyPartSizeMode.AttachNode
            );
        }
    }
}
