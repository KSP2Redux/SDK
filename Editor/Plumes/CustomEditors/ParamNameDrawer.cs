using UnityEditor;
using UnityEngine;
using Redux.VFX.Plume.Components;
using Redux.VFX.Plume.ShaderEditor;
using Redux.VFX.Plumes.Editor.Utility;

namespace Redux.VFX.Plumes.Editor.CustomEditors
{
    [CustomPropertyDrawer(typeof(ParamName))]
    public class ParamNameDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);

            var indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            var nameRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);

            var valueProp = property.FindPropertyRelative("Value");
            if (property.serializedObject.targetObject is not PlumeThrottleData { Renderer: { } renderer })
            {
                EditorGUI.PropertyField(nameRect, valueProp, GUIContent.none);
            }
            else
            {
                string[] shaderProps = ShaderUtility.GetShaderPropertyNames(renderer.sharedMaterial.shader);

                if (shaderProps != null)
                {
                    if (EditorGUI.DropdownButton(
                            nameRect,
                            new GUIContent(valueProp.stringValue),
                            FocusType.Keyboard
                        ))
                    {
                        var menu = new GenericMenu();
                        foreach (string propName in shaderProps)
                        {
                            bool isSelected = valueProp.stringValue == propName;
                            menu.AddItem(new GUIContent(propName), isSelected, () =>
                            {
                                valueProp.stringValue = propName;
                                valueProp.serializedObject.ApplyModifiedProperties();
                                property.serializedObject.ApplyModifiedProperties();
                            });
                        }

                        menu.ShowAsContext();
                    }
                }
            }

            EditorGUI.indentLevel = indent;

            EditorGUI.EndProperty();
        }
    }
}