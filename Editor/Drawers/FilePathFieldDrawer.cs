using System.IO;
using KSP.Inspector;
using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.Drawers
{
    [CustomPropertyDrawer(typeof(FilePathFieldAttribute))]
    public class FilePathFieldDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var attr = (FilePathFieldAttribute)attribute;
            bool browseClicked = false;

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

            browseClicked = GUI.Button(btnRect, "Browse");

            EditorGUI.EndProperty();

            if (browseClicked)
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
                        string directoryName = Path.GetDirectoryName(path);
                        if (!string.IsNullOrEmpty(directoryName))
                        {
                            startDir = directoryName;
                        }
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
        }
    }
}