using KSP.Sim.Definitions;
using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.CustomEditors
{
    [CustomPropertyDrawer(typeof(PartData))]
    public class PartDataDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return PartSizeInspectorFields.GetFoldoutPropertyHeight(
                property,
                PartSizeInspectorFields.PartSizeFieldName,
                PartSizeInspectorFields.LegacyPartSizeCategoryFieldName
            );
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            PartSizeInspectorFields.DrawFoldoutProperty(
                position,
                property,
                label,
                PartSizeInspectorFields.PartSizeFieldName,
                PartSizeInspectorFields.LegacyPartSizeCategoryFieldName,
                "Part Size",
                PartSizeInspectorFields.LegacyPartSizeMode.PartCategory
            );
        }
    }
}
