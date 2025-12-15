using System.IO;
using KSP.Inspector;
using UnityEditor;
using UnityEngine;

namespace ksp2community.ksp2unitytools.editor.Drawers
{
    [CustomPropertyDrawer(typeof(FilePathFieldAttribute))]
    public class FilePathFieldDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var attr = (FilePathFieldAttribute)attribute;

            EditorGUI.BeginProperty(position, label, property);

            const float btnW = 70f;
            var fieldRect = new Rect(position.x, position.y, position.width - btnW - 2, position.height);
            var btnRect = new Rect(position.x + position.width - btnW, position.y, btnW, position.height);

            EditorGUI.BeginChangeCheck();
            string newVal = EditorGUI.TextField(fieldRect, label, property.stringValue);
            if (EditorGUI.EndChangeCheck())
            {
                property.stringValue = newVal;
            }

            if (GUI.Button(btnRect, "Browse"))
            {
                string startDir = "Assets";
                if (!string.IsNullOrEmpty(property.stringValue))
                {
                    string path = property.stringValue;
                    if (!Path.IsPathRooted(path))
                    {
                        path = Path.Combine(Directory.GetCurrentDirectory(), path);
                    }

                    if (File.Exists(path))
                    {
                        startDir = Path.GetDirectoryName(path);
                    }
                }

                string picked = EditorUtility.OpenFilePanel(attr.Title, startDir, attr.Extension ?? "");
                if (!string.IsNullOrEmpty(picked))
                {
                    // Store relative "Assets/..." if not absolute
                    if (!attr.IsAbsolute &&
                        picked.Replace('\\', '/').StartsWith(Application.dataPath.Replace('\\', '/')))
                    {
                        string rel = "Assets" + picked[Application.dataPath.Length..];
                        property.stringValue = rel.Replace('\\', '/');
                    }
                    else
                    {
                        property.stringValue = picked.Replace('\\', '/');
                    }
                }
            }

            EditorGUI.EndProperty();
        }
    }
}