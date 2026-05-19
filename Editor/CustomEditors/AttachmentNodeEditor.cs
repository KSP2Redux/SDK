using UnityEditor;

namespace Ksp2UnityTools.Editor.CustomEditors
{
    [CustomEditor(typeof(AttachmentNode))]
    public class AttachmentNodeEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            SerializedProperty sizeProperty =
                serializedObject.FindProperty(PartSizeInspectorFields.AttachNodeSizeFieldName);
            SerializedProperty sizeKeyProperty =
                serializedObject.FindProperty(PartSizeInspectorFields.AttachNodeSizeKeyFieldName);
            SerializedProperty iterator = serializedObject.GetIterator();
            bool enterChildren = true;

            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;

                if (iterator.propertyPath == "m_Script")
                {
                    using (new EditorGUI.DisabledScope(true))
                    {
                        EditorGUILayout.PropertyField(iterator, true);
                    }

                    continue;
                }

                if (iterator.name == PartSizeInspectorFields.AttachNodeSizeFieldName)
                {
                    PartSizeInspectorFields.DrawSizeKeyLayout(
                        "Node Size",
                        sizeKeyProperty,
                        sizeProperty,
                        PartSizeInspectorFields.LegacyPartSizeMode.AttachNode
                    );
                    continue;
                }

                if (iterator.name == PartSizeInspectorFields.AttachNodeSizeKeyFieldName)
                {
                    continue;
                }

                EditorGUILayout.PropertyField(iterator, true);
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
