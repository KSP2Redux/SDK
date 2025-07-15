using KSP.Messages;
using KSP.Messages.PropertyWatchers;
using ksp2community.ksp2unitytools.editor.API;
using ksp2community.ksp2unitytools.editor.Editor.Extensions;
using ksp2community.ksp2unitytools.editor.Missions.ConditionTree;
using UnityEditor;
using UnityEditor.Graphs;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace ksp2community.ksp2unitytools.editor.Missions.Editors
{
    [CustomPropertyDrawer(typeof(BaseMissionCondition), true)]
    public class MissionConditionEditor : PropertyDrawer
    {
        private class MissionConditionDrawer : VisualElement
        {
            private Foldout _foldout;
            private SerializedProperty _property;
            private VisualElement _propertyConditionTab;
            private VisualElement _eventConditionTab;
            private VisualElement _conditionSetTab;

            public MissionConditionDrawer(SerializedProperty property)
            {
                _property = property;
                _foldout = new Foldout
                {
                    text = ((MissionConditionType)property.FindPropertyRelative("conditionType").enumValueIndex)
                        .ToString().PascalToInspectorCase(),
                };
                var conditionTypeDropDown = new PropertyField(property.FindPropertyRelative("conditionType"));
                conditionTypeDropDown.RegisterValueChangeCallback(ConditionTypeChanged);
                _foldout.Add(conditionTypeDropDown);
                _propertyConditionTab = new VisualElement();
                _propertyConditionTab.Add(TypeSelection.CreatePropertyForTypesInheritedFromT<PropertyWatcher>(
                    "Property Type",
                    _ => { }, property.FindPropertyRelative("propertyType").stringValue, "propertyType"));
                _propertyConditionTab.Add(new PropertyField(property.FindPropertyRelative("requireCurrentValue")));
                _propertyConditionTab.Add(new PropertyField(property.FindPropertyRelative("watchedStringValue")));
                _propertyConditionTab.Add(new PropertyField(property.FindPropertyRelative("watchedFloatValue")));
                _propertyConditionTab.Add(new PropertyField(property.FindPropertyRelative("watchedBooleanValue")));
                _propertyConditionTab.Add(new PropertyField(property.FindPropertyRelative("propertyOperator")));
                _propertyConditionTab.Add(new PropertyField(property.FindPropertyRelative("useStringInput")));
                _propertyConditionTab.Add(new PropertyField(property.FindPropertyRelative("stringInput")));
                _eventConditionTab = new VisualElement();
                _eventConditionTab.Add(TypeSelection.CreatePropertyForTypesInheritedFromT<MessageCenterMessage>(
                    "Event Type",
                    _ => { }, property.FindPropertyRelative("eventType").stringValue, "eventType"));
                _conditionSetTab = new VisualElement();
                if (property.FindPropertyRelative("conditionMode") != null)
                {
                    _conditionSetTab.Add(new PropertyField(property.FindPropertyRelative("conditionMode")));
                    _conditionSetTab.Add(new PropertyField(property.FindPropertyRelative("children")));
                }

                _foldout.Add(_propertyConditionTab);
                _foldout.Add(_eventConditionTab);
                _foldout.Add(_conditionSetTab);
                Add(_foldout);
                UpdateFoldouts();
            }


            private void ConditionTypeChanged(SerializedPropertyChangeEvent evt)
            {
                UpdateFoldouts();
            }

            private void UpdateFoldouts()
            {
                var property = (MissionConditionType)_property.FindPropertyRelative("conditionType").enumValueIndex;
                _foldout.text = property.ToString().PascalToInspectorCase();
                switch (property)
                {
                    case MissionConditionType.None:
                        _conditionSetTab.style.display = DisplayStyle.None;
                        _eventConditionTab.style.display = DisplayStyle.None;
                        _propertyConditionTab.style.display = DisplayStyle.None;
                        break;
                    case MissionConditionType.ConditionSet:
                        _conditionSetTab.style.display = DisplayStyle.Flex;
                        _eventConditionTab.style.display = DisplayStyle.None;
                        _propertyConditionTab.style.display = DisplayStyle.None;
                        break;
                    case MissionConditionType.EventCondition:
                        _eventConditionTab.style.display = DisplayStyle.Flex;
                        _propertyConditionTab.style.display = DisplayStyle.None;
                        _conditionSetTab.style.display = DisplayStyle.None;
                        break;
                    case MissionConditionType.PropertyCondition:
                        _conditionSetTab.style.display = DisplayStyle.None;
                        _eventConditionTab.style.display = DisplayStyle.None;
                        _propertyConditionTab.style.display = DisplayStyle.Flex;
                        break;
                }
            }
        }

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            return new MissionConditionDrawer(property);
        }
    }
}