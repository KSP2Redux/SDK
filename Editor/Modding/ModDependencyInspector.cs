using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace ksp2community.ksp2unitytools.editor.Editor.Modding
{
    [CustomPropertyDrawer(typeof(ModDependency))]
    public class ModDependencyInspector : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            // Create drawer UI using C#.
            var popup = new UnityEngine.UIElements.PopupWindow
            {
                text = "Dependency or Conflict Details"
            };
            popup.Add(new PropertyField(property.FindPropertyRelative("id"), "ID"));
            popup.Add(new PropertyField(property.FindPropertyRelative("min"), "Minimum Version"));
            popup.Add(new PropertyField(property.FindPropertyRelative("max"), "Maximum Version"));
    
            // Return the finished UI.
            return popup;
        }
    }
}