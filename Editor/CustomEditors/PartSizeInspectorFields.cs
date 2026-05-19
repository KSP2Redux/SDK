using System;
using System.Collections.Generic;
using System.Globalization;
using KSP.OAB;
using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.CustomEditors
{
    internal static class PartSizeInspectorFields
    {
        public const string PartSizeFieldName = "sizeKey";
        public const string LegacyPartSizeCategoryFieldName = "sizeCategory";
        public const string AttachNodeSizeFieldName = "size";
        public const string AttachNodeSizeKeyFieldName = "sizeKey";

        private const string AutoCategoryName = "Auto";
        private const float VerticalSpacing = 2f;

        private static readonly HashSet<string> CustomModePropertyPaths = new();
        private static GUIContent[] _presetOptions;
        private static string[] _presetKeys;

        public enum LegacyPartSizeMode
        {
            PartCategory,
            AttachNode
        }

        public static float GetFoldoutPropertyHeight(
            SerializedProperty property,
            string sizeFieldName,
            string hiddenCompatibilityFieldName
        )
        {
            float height = EditorGUIUtility.singleLineHeight;
            if (!property.isExpanded)
            {
                return height;
            }

            SerializedProperty iterator = property.Copy();
            SerializedProperty endProperty = property.GetEndProperty();
            bool enterChildren = true;

            while (iterator.NextVisible(enterChildren) && !SerializedProperty.EqualContents(iterator, endProperty))
            {
                enterChildren = false;
                if (iterator.depth != property.depth + 1)
                {
                    continue;
                }

                if (iterator.name == hiddenCompatibilityFieldName)
                {
                    continue;
                }

                height += VerticalSpacing;
                height += iterator.name == sizeFieldName
                    ? GetSizeKeyFieldHeight(GetSizeKeyProperty(property, sizeFieldName))
                    : EditorGUI.GetPropertyHeight(iterator, true);
            }

            return height;
        }

        public static void DrawFoldoutProperty(
            Rect position,
            SerializedProperty property,
            GUIContent label,
            string sizeFieldName,
            string hiddenCompatibilityFieldName,
            string sizeLabel,
            LegacyPartSizeMode legacyMode
        )
        {
            EditorGUI.BeginProperty(position, label, property);
            Rect foldoutRect = new(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, label, true);

            if (!property.isExpanded)
            {
                EditorGUI.EndProperty();
                return;
            }

            int originalIndent = EditorGUI.indentLevel;
            EditorGUI.indentLevel++;

            float y = foldoutRect.yMax;
            SerializedProperty iterator = property.Copy();
            SerializedProperty endProperty = property.GetEndProperty();
            bool enterChildren = true;

            while (iterator.NextVisible(enterChildren) && !SerializedProperty.EqualContents(iterator, endProperty))
            {
                enterChildren = false;
                if (iterator.depth != property.depth + 1)
                {
                    continue;
                }

                if (iterator.name == hiddenCompatibilityFieldName)
                {
                    continue;
                }

                y += VerticalSpacing;
                float height = iterator.name == sizeFieldName
                    ? GetSizeKeyFieldHeight(GetSizeKeyProperty(property, sizeFieldName))
                    : EditorGUI.GetPropertyHeight(iterator, true);
                Rect childRect = new(position.x, y, position.width, height);

                if (iterator.name == sizeFieldName)
                {
                    DrawSizeKeyField(
                        childRect,
                        sizeLabel,
                        GetSizeKeyProperty(property, sizeFieldName),
                        legacyMode == LegacyPartSizeMode.AttachNode
                            ? iterator
                            : GetLegacyProperty(property, hiddenCompatibilityFieldName),
                        legacyMode
                    );
                }
                else
                {
                    EditorGUI.PropertyField(childRect, iterator, true);
                }

                y += height;
            }

            EditorGUI.indentLevel = originalIndent;
            EditorGUI.EndProperty();
        }

        public static void DrawSizeKeyLayout(
            string label,
            SerializedProperty sizeKeyProperty,
            SerializedProperty legacyProperty,
            LegacyPartSizeMode legacyMode
        )
        {
            Rect rect = EditorGUILayout.GetControlRect(true, GetSizeKeyFieldHeight(sizeKeyProperty));
            DrawSizeKeyField(rect, label, sizeKeyProperty, legacyProperty, legacyMode);
        }

        private static void DrawSizeKeyField(
            Rect position,
            string label,
            SerializedProperty sizeKeyProperty,
            SerializedProperty legacyProperty,
            LegacyPartSizeMode legacyMode
        )
        {
            EnsurePresetCache();

            string pathKey = GetPathKey(sizeKeyProperty);
            string currentKey = sizeKeyProperty?.stringValue ?? string.Empty;
            int customIndex = _presetKeys.Length;
            bool customMode = CustomModePropertyPaths.Contains(pathKey) || IsCustomKey(currentKey);
            int selectedIndex = customMode ? customIndex : GetPresetIndex(currentKey);

            if (selectedIndex < 0)
            {
                selectedIndex = GetPresetIndex(GetLegacyKey(legacyProperty, legacyMode));
            }

            if (selectedIndex < 0)
            {
                selectedIndex = GetPresetIndex(PartSizeRegistry.DefaultSizeKey);
            }

            Rect popupRect = new(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            EditorGUI.BeginChangeCheck();
            int newIndex = EditorGUI.Popup(popupRect, new GUIContent(label), selectedIndex, _presetOptions);
            if (EditorGUI.EndChangeCheck())
            {
                if (newIndex == customIndex)
                {
                    CustomModePropertyPaths.Add(pathKey);
                    if (!IsCustomKey(currentKey))
                    {
                        sizeKeyProperty.stringValue = string.Empty;
                    }

                    SetLegacyCustom(legacyProperty, legacyMode);
                }
                else
                {
                    CustomModePropertyPaths.Remove(pathKey);
                    sizeKeyProperty.stringValue = _presetKeys[newIndex];
                    SetLegacyPreset(legacyProperty, legacyMode, PartSizeRegistry.Get(_presetKeys[newIndex]));
                }
            }

            if (newIndex != customIndex && !customMode)
            {
                return;
            }

            Rect textRect = new(
                position.x,
                popupRect.yMax + VerticalSpacing,
                position.width,
                EditorGUIUtility.singleLineHeight
            );
            EditorGUI.PropertyField(textRect, sizeKeyProperty, new GUIContent("Custom Key"));
        }

        private static float GetSizeKeyFieldHeight(SerializedProperty sizeKeyProperty)
        {
            return IsCustomKey(sizeKeyProperty?.stringValue) ||
                CustomModePropertyPaths.Contains(GetPathKey(sizeKeyProperty))
                    ? EditorGUIUtility.singleLineHeight * 2f + VerticalSpacing
                    : EditorGUIUtility.singleLineHeight;
        }

        private static SerializedProperty GetSizeKeyProperty(SerializedProperty property, string sizeFieldName)
        {
            return sizeFieldName == AttachNodeSizeFieldName
                ? property.FindPropertyRelative(AttachNodeSizeKeyFieldName)
                : property.FindPropertyRelative(PartSizeFieldName);
        }

        private static SerializedProperty GetLegacyProperty(
            SerializedProperty property,
            string hiddenCompatibilityFieldName
        )
        {
            return property.FindPropertyRelative(hiddenCompatibilityFieldName);
        }

        private static string GetLegacyKey(SerializedProperty legacyProperty, LegacyPartSizeMode legacyMode)
        {
            if (legacyProperty == null)
            {
                return null;
            }

            if (legacyMode == LegacyPartSizeMode.AttachNode)
            {
                return PartSizeRegistry.GetAttachNodeSizeKey(null, legacyProperty.intValue);
            }

            string enumName = legacyProperty.enumNames[legacyProperty.enumValueIndex];
            return Enum.TryParse(enumName, out MetaAssemblySizeFilterType category)
                ? PartSizeRegistry.GetPartSizeKey(null, category)
                : null;
        }

        private static void SetLegacyPreset(
            SerializedProperty legacyProperty,
            LegacyPartSizeMode legacyMode,
            PartSizeDefinition definition
        )
        {
            if (legacyProperty == null)
            {
                return;
            }

            if (legacyMode == LegacyPartSizeMode.AttachNode)
            {
                if (definition.LegacyAttachNodeSizeAliases.Count > 0)
                {
                    legacyProperty.intValue = definition.LegacyAttachNodeSizeAliases[0];
                }

                return;
            }

            SetEnumValue(legacyProperty, GetMetaCategoryName(definition.Key));
        }

        private static void SetLegacyCustom(SerializedProperty legacyProperty, LegacyPartSizeMode legacyMode)
        {
            if (legacyProperty == null || legacyMode == LegacyPartSizeMode.AttachNode)
            {
                return;
            }

            SetEnumValue(legacyProperty, AutoCategoryName);
        }

        private static void SetEnumValue(SerializedProperty property, string enumName)
        {
            for (int i = 0; i < property.enumNames.Length; i++)
            {
                if (property.enumNames[i] == enumName)
                {
                    property.enumValueIndex = i;
                    return;
                }
            }
        }

        private static bool IsCustomKey(string key)
        {
            return !string.IsNullOrWhiteSpace(key) && !PartSizeRegistry.IsValidKey(key);
        }

        private static int GetPresetIndex(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return -1;
            }

            for (int i = 0; i < _presetKeys.Length; i++)
            {
                if (string.Equals(_presetKeys[i], key, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return -1;
        }

        private static string GetPathKey(SerializedProperty property)
        {
            return property == null
                ? string.Empty
                : property.serializedObject.targetObject.GetInstanceID() + ":" + property.propertyPath;
        }

        private static string GetMetaCategoryName(string key)
        {
            return key switch
            {
                PartSizeRegistry.XsMinus => nameof(MetaAssemblySizeFilterType.XSMINUS),
                PartSizeRegistry.Xs => nameof(MetaAssemblySizeFilterType.XS),
                PartSizeRegistry.XsPlus => nameof(MetaAssemblySizeFilterType.XSPLUS),
                PartSizeRegistry.Sm => nameof(MetaAssemblySizeFilterType.S),
                PartSizeRegistry.SmPlus => nameof(MetaAssemblySizeFilterType.SPLUS),
                PartSizeRegistry.Md => nameof(MetaAssemblySizeFilterType.M),
                PartSizeRegistry.MdPlus => nameof(MetaAssemblySizeFilterType.MPLUS),
                PartSizeRegistry.Lg => nameof(MetaAssemblySizeFilterType.L),
                PartSizeRegistry.LgPlus => nameof(MetaAssemblySizeFilterType.LPLUS),
                PartSizeRegistry.Xl => nameof(MetaAssemblySizeFilterType.XL),
                PartSizeRegistry.XlPlus => nameof(MetaAssemblySizeFilterType.XLPLUS),
                PartSizeRegistry.TwoXl => nameof(MetaAssemblySizeFilterType.XXL),
                PartSizeRegistry.ThreeXl => nameof(MetaAssemblySizeFilterType.XXXL),
                PartSizeRegistry.FourXl => nameof(MetaAssemblySizeFilterType.XXXXL),
                PartSizeRegistry.FiveXl => nameof(MetaAssemblySizeFilterType.XXXXXL),
                PartSizeRegistry.SixXl => nameof(MetaAssemblySizeFilterType.XXXXXXL),
                _ => AutoCategoryName
            };
        }

        private static void EnsurePresetCache()
        {
            if (_presetOptions != null)
            {
                return;
            }

            IReadOnlyList<PartSizeDefinition> definitions = PartSizeRegistry.Definitions;
            _presetKeys = new string[definitions.Count];
            _presetOptions = new GUIContent[definitions.Count + 1];

            for (int i = 0; i < definitions.Count; i++)
            {
                PartSizeDefinition definition = definitions[i];
                _presetKeys[i] = definition.Key;
                _presetOptions[i] = new GUIContent(
                    definition.DisplayName + " (" +
                    definition.Diameter.ToString("0.####", CultureInfo.InvariantCulture) + " m)"
                );
            }

            _presetOptions[^1] = new GUIContent("Custom...");
        }
    }
}
