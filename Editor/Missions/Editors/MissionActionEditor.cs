using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using KSP.Game.Missions;
using KSP.Messages;
using Ksp2UnityTools.Editor.Extensions;
using Ksp2UnityTools.Editor.API;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Enumerable = UniLinq.Enumerable;
using StringExtensions = Ksp2UnityTools.Editor.Extensions.StringExtensions;

namespace Ksp2UnityTools.Editor.Missions.Editors
{
    [CustomPropertyDrawer(typeof(MissionAction))]
    public class MissionActionEditor : PropertyDrawer
    {
        private static List<Type> _validMissionActionTypes = new();

        static MissionActionEditor()
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (Type type in assembly.GetTypes()
                    .Where(x => typeof(IMissionAction).IsAssignableFrom(x) && !x.IsAbstract))
                {
                    _validMissionActionTypes.Add(type);
                }
            }
        }

        private class MissionActionDrawer : VisualElement
        {
            private Foldout _foldout;
            private PropertyField _oabDrawer;
            private PropertyField _dialogDrawer;
            private VisualElement _generalContainer;
            private SerializedProperty _property;
            private SerializedProperty _keys;
            private SerializedProperty _values;
            private string Aqn => _property.FindPropertyRelative("actionAqn").stringValue;
            private Type ActionType => Type.GetType(Aqn);

            public MissionActionDrawer(SerializedProperty property)
            {
                _property = property;
                _keys = property.FindPropertyRelative("keys");
                _values = property.FindPropertyRelative("values");
                _generalContainer = new VisualElement();
                _oabDrawer = new PropertyField(
                    property.FindPropertyRelative("workspaceSelectionData"),
                    "Selected Workspaces"
                );
                _dialogDrawer = new PropertyField(property.FindPropertyRelative("dialogEntries"), "Dialogs");
                _foldout = new Foldout();
                if (ActionType == null)
                {
                    ResetForType(_validMissionActionTypes[0]);
                }

                _foldout.text = ActionType?.Name ?? "Unknown Action";
                var dropdown =
                    new DropdownField(
                        _validMissionActionTypes.Select(x => x.Name).ToList(),
                        ActionType?.Name ?? _validMissionActionTypes[0].Name,
                        StringExtensions.PascalToInspectorCase,
                        StringExtensions.PascalToInspectorCase
                    );
                dropdown.RegisterValueChangedCallback(ClearOldData);
                _foldout.Add(dropdown);
                _foldout.Add(_oabDrawer);
                _foldout.Add(_dialogDrawer);
                _foldout.Add(_generalContainer);
                RebuildGui(ActionType);
                Add(_foldout);
            }

            public void ClearOldData(ChangeEvent<string> changeEvent)
            {
                if (changeEvent.previousValue == changeEvent.newValue)
                {
                    return;
                }

                Type type = _validMissionActionTypes.First(x => x.Name == changeEvent.newValue);
                ResetForType(type);
            }

            private void ResetForType(Type type)
            {
                _property.FindPropertyRelative("actionAqn").stringValue = type.AssemblyQualifiedName;
                _property.FindPropertyRelative("keys");
                for (int i = _keys.arraySize - 1; i >= 0; i--)
                {
                    _keys.DeleteArrayElementAtIndex(i);
                    _values.DeleteArrayElementAtIndex(i);
                }

                foreach (FieldInfo field in type.GetFields()
                    .Where(x => x.GetCustomAttributes(typeof(JsonPropertyAttribute), true).Any()))
                {
                    if (field.FieldType == typeof(string))
                    {
                        SetString(field.Name, "");
                    }
                    else if (field.FieldType == typeof(int) || field.FieldType == typeof(long) ||
                        field.FieldType == typeof(double) || field.FieldType == typeof(float))
                    {
                        Set(field.Name, 0);
                    }
                    else if (field.FieldType == typeof(Type))
                    {
                        Set(field.Name, "");
                    }
                    else if (field.FieldType == typeof(bool))
                    {
                        Set(field.Name, false);
                    }
                    else if (field.FieldType == typeof(Vector3))
                    {
                        SetVector3(field.Name, new Vector3());
                    }
                    else if (field.FieldType.IsEnum)
                    {
                        Set(field.Name, Enum.GetValues(field.FieldType).GetValue(0));
                    }
                }

                _property.serializedObject.ApplyModifiedProperties();
                RebuildGui(type);
            }

            public void RebuildGui(Type newType)
            {
                _generalContainer.Clear();
                _oabDrawer.style.display = DisplayStyle.None;
                _dialogDrawer.style.display = DisplayStyle.None;
                _foldout.text = newType?.Name.PascalToInspectorCase() ?? "Unknown Action";
                if (newType == null)
                {
                    return;
                }

                foreach (FieldInfo field in newType.GetFields())
                {
                    if (field.FieldType == typeof(DialogEntries))
                    {
                        _dialogDrawer.style.display = DisplayStyle.Flex;
                    }
                    else if (field.FieldType == typeof(List<WorkspaceSelectionData>))
                    {
                        _oabDrawer.style.display = DisplayStyle.Flex;
                    }
                    else if (field.FieldType == typeof(string))
                    {
                        var textField = new TextField(field.Name.PascalToInspectorCase())
                        {
                            value = GetString(field.Name)
                        };
                        textField.RegisterValueChangedCallback(evt => SetString(field.Name, evt.newValue));
                        textField.AddToClassList("unity-base-field__aligned");
                        _generalContainer.Add(textField);
                    }
                    else if (field.FieldType == typeof(int))
                    {
                        var intField = new IntegerField(field.Name.PascalToInspectorCase())
                        {
                            value = (int)GetInt(field.Name)
                        };
                        intField.RegisterValueChangedCallback(evt => Set(field.Name, evt.newValue));
                        intField.AddToClassList("unity-base-field__aligned");
                        _generalContainer.Add(intField);
                    }
                    else if (field.FieldType == typeof(long))
                    {
                        var longField = new LongField(field.Name.PascalToInspectorCase())
                        {
                            value = GetInt(field.Name)
                        };
                        longField.RegisterValueChangedCallback(evt => Set(field.Name, evt.newValue));
                        longField.AddToClassList("unity-base-field__aligned");
                        _generalContainer.Add(longField);
                    }
                    else if (field.FieldType == typeof(double))
                    {
                        var doubleField = new DoubleField(field.Name.PascalToInspectorCase())
                        {
                            value = GetFloat(field.Name)
                        };
                        doubleField.RegisterValueChangedCallback(evt => Set(field.Name, evt.newValue));
                        doubleField.AddToClassList("unity-base-field__aligned");
                        _generalContainer.Add(doubleField);
                    }
                    else if (field.FieldType == typeof(float))
                    {
                        var floatField = new FloatField(field.Name.PascalToInspectorCase())
                        {
                            value = (float)GetFloat(field.Name)
                        };
                        floatField.RegisterValueChangedCallback(evt => Set(field.Name, evt.newValue));
                        floatField.AddToClassList("unity-base-field__aligned");
                        _generalContainer.Add(floatField);
                    }
                    else if (field.FieldType == typeof(bool))
                    {
                        var boolField = new Toggle(field.Name.PascalToInspectorCase())
                        {
                            value = GetBool(field.Name)
                        };
                        boolField.RegisterValueChangedCallback(evt => Set(field.Name, evt.newValue));
                        boolField.AddToClassList("unity-base-field__aligned");
                        _generalContainer.Add(boolField);
                    }
                    else if (field.FieldType == typeof(Vector3))
                    {
                        var vectorField = new Vector3Field(field.Name.PascalToInspectorCase())
                        {
                            value = GetVector3(field.Name)
                        };
                        vectorField.RegisterValueChangedCallback(evt => SetVector3(field.Name, evt.newValue));
                        vectorField.AddToClassList("unity-base-field__aligned");
                        _generalContainer.Add(vectorField);
                    }
                    else if (field.FieldType.IsEnum)
                    {
                        var dropdownField = new DropdownField(field.Name.PascalToInspectorCase());
                        foreach (object value in Enum.GetValues(field.FieldType))
                        {
                            dropdownField.choices.Add(value.ToString());
                        }

                        dropdownField.value = GetEnum(field.Name, field.FieldType).ToString();
                        dropdownField.RegisterValueChangedCallback(evt => Set(field.Name, evt.newValue));
                        dropdownField.AddToClassList("unity-base-field__aligned");
                        _generalContainer.Add(dropdownField);
                    }
                    else if (field.FieldType == typeof(Type))
                    {
                        VisualElement messageField =
                            TypeSelection.CreatePropertyForTypesInheritedFromT<MessageCenterMessage>(
                                field.Name.PascalToInspectorCase(),
                                x => SetType(field.Name, x),
                                GetType(field.Name)?.AssemblyQualifiedName ?? ""
                            );
                        _generalContainer.Add(messageField);
                    }
                }
            }

            public int KeyIndex(string key)
            {
                for (int i = 0; i < _keys.arraySize; i++)
                {
                    if (_keys.GetArrayElementAtIndex(i).stringValue == key)
                    {
                        return i;
                    }
                }

                return -1;
            }

            public void SetString(string key, string value)
            {
                int index = KeyIndex(key);
                if (index == -1)
                {
                    _keys.InsertArrayElementAtIndex(_keys.arraySize);
                    _keys.GetArrayElementAtIndex(_keys.arraySize - 1).stringValue = key;
                    _values.InsertArrayElementAtIndex(_values.arraySize);
                    _values.GetArrayElementAtIndex(_values.arraySize - 1).stringValue = value;
                }
                else
                {
                    _values.GetArrayElementAtIndex(index).stringValue = value;
                }

                _property.serializedObject.ApplyModifiedProperties();
            }

            public void Set<T>(string key, T value)
            {
                SetString(key, value.ToString());
            }

            public void SetVector3(string key, Vector3 value)
            {
                SetString(key, $"{value.x}, {value.y}, {value.z}");
            }

            public void SetType(string key, Type value)
            {
                SetString(key, value.AssemblyQualifiedName);
            }

            public string GetString(string key)
            {
                int index = KeyIndex(key);
                return index == -1 ? null : _values.GetArrayElementAtIndex(index).stringValue;
            }

            public object GetEnum(string key, Type enumType)
            {
                string value = GetString(key);
                if (value == null)
                {
                    return Enum.GetValues(enumType).GetValue(0);
                }

                return Enum.TryParse(enumType, value, out object result)
                    ? result
                    : Enum.GetValues(enumType).GetValue(0);
            }

            public long GetInt(string key)
            {
                string value = GetString(key);
                if (value == null)
                {
                    return 0;
                }

                return long.TryParse(value, out long result) ? result : 0;
            }

            public double GetFloat(string key)
            {
                string value = GetString(key);
                if (value == null)
                {
                    return 0;
                }

                return double.TryParse(value, out double result) ? result : 0;
            }

            public bool GetBool(string key)
            {
                string value = GetString(key);
                if (value == null)
                {
                    return false;
                }

                return bool.TryParse(value, out bool result) && result;
            }

            public Type GetType(string key)
            {
                string value = GetString(key);
                if (value == null)
                {
                    return null;
                }

                return Type.GetType(value);
            }

            public Vector3 GetVector3(string key)
            {
                string value = GetString(key);
                if (value == null)
                {
                    return new Vector3();
                }

                float[] v = Enumerable.ToArray(
                    value.Split(',').Select(x => float.TryParse(x.Trim(), out float y) ? y : 0)
                );
                return new Vector3(v[0], v[1], v[2]);
            }
        }

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            return new MissionActionDrawer(property);
        }
    }
}