using System;
using System.Collections.Generic;
using ksp2community.ksp2unitytools.editor.Editor.Extensions;
using UniLinq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ksp2community.ksp2unitytools.editor.API
{
    public class TypeSelection : EditorWindow
    {
        private static Dictionary<Type, Type[]> _types = new Dictionary<Type, Type[]>();

        
        public static VisualElement CreatePropertyForTypesInheritedFromT<T>(string label, Action<Type> onTypeSelected, string initialValue, string bindPath="")
        {
            if (!_types.TryGetValue(typeof(T), out var types))
            {
                List<Type> typesInheritedFrom = new List<Type>();
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    foreach (var type in assembly.GetTypes()
                                 .Where(x => typeof(T).IsAssignableFrom(x) && !x.IsInterface && !x.IsAbstract))
                    {
                        typesInheritedFrom.Add(type);
                    }
                }
                types = typesInheritedFrom.ToArray();
            }
            
            var element = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row
                }
            };
            var textField = new TextField(label)
            {
                label = label,
                style =
                {
                    flexGrow = 0,
                    flexShrink = 0,
                    textOverflow = TextOverflow.Clip,
                    width = new StyleLength(new Length(90, LengthUnit.Percent))
                }
            };
            textField.AddToClassList("unity-base-field__aligned");
            textField.value = initialValue;
            if (!string.IsNullOrEmpty(bindPath)) textField.bindingPath = bindPath;
            var button = new Button
            {
                text = "Select",
                style =
                {
                    flexGrow = 0,
                    flexShrink = 0,
                    width = new Length(10, LengthUnit.Percent)
                }
            };
            
            element.Add(textField);
            element.Add(button);
            button.clicked += () =>
            {
                ShowFor(types, textField, onTypeSelected);
            };
            return element;
        }

        private static void ShowFor(Type[] types, TextField textField, Action<Type> onTypeSelected)
        {
            var instance = GetWindow<TypeSelection>();
            instance.RefreshWindowFor(types, textField, onTypeSelected);
        }

        private TextField _textField;
        private ScrollView _scrollView;
        private Button _cancel;


        private void CreateGUI()
        {
            titleContent = new GUIContent("Select Type");
            _textField = new TextField();
            rootVisualElement.Add(_textField);
            _scrollView = new ScrollView
            {
                style =
                {
                    backgroundColor = new StyleColor(new Color(32 / 255f, 32 / 255f, 32 / 255f)),
                    borderBottomColor = new StyleColor(new Color(64 / 255f, 64 / 255f, 64 / 255f)),
                    borderBottomWidth = 3,
                    borderBottomLeftRadius = 3,
                    borderBottomRightRadius = 3,
                    borderLeftColor = new StyleColor(new Color(64 / 255f, 64 / 255f, 64 / 255f)),
                    borderLeftWidth = 3,
                    borderRightColor = new StyleColor(new Color(64 / 255f, 64 / 255f, 64 / 255f)),
                    borderRightWidth = 3,
                    borderTopColor = new StyleColor(new Color(64 / 255f, 64 / 255f, 64 / 255f)),
                    borderTopWidth = 3,
                    borderTopLeftRadius = 3,
                    borderTopRightRadius = 3
                }
            };
            rootVisualElement.Add(_scrollView);
            _textField.RegisterValueChangedCallback(evt =>
            {
                foreach (var child in _scrollView.Children().OfType<Button>())
                {
                    child.style.display = child.text.StartsWith(evt.newValue, StringComparison.InvariantCultureIgnoreCase) ? DisplayStyle.Flex : DisplayStyle.None;
                }
            });
            _cancel = new Button
            {
                text = "Cancel Type Selection",
            };
            _cancel.clicked += Close;
            rootVisualElement.Add(_cancel);
        }

        private void RefreshWindowFor(Type[] types, TextField textField, Action<Type> onTypeSelected)
        {
            _scrollView.Clear();
            foreach (var type in types)
            {
                var button = new Button
                {
                    text = type.Name.PascalToInspectorCase()
                };
                button.clicked += () =>
                {
                    textField.value = type.AssemblyQualifiedName;
                    onTypeSelected(type);
                    Close();
                };
                _scrollView.Add(button);
            }
        }
    }
}