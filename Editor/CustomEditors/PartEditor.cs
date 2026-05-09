using System.IO;
using System.Reflection;
using System;
using System.Collections.Generic;
using System.Globalization;
using KSP;
using KSP.IO;
using KSP.OAB;
using KSP.Sim.Definitions;
using Ksp2UnityTools.Editor.API;
using Ksp2UnityTools.Editor.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.CustomEditors
{
    [CustomEditor(typeof(CorePartData))]
    public class PartEditor : UnityEditor.Editor
    {
        private static bool _initialized = false;
        private static readonly Color ComColor = new(Color.yellow.r, Color.yellow.g, Color.yellow.b, 0.5f);

        // private static string _jsonPath = "%NAME%.json";

        private static bool _centerOfMassGizmos = true;
        private static bool _centerOfLiftGizmos = true;
        private static bool _attachNodeGizmos = true;


        public static bool DragCubeGizmos = true;

        // Just initialize all the conversion stuff
        private static void Initialize()
        {
            IOProvider.Init();
            _initialized = true;
        }

        private static PersistentDictionary _prefabAddressOverrides;

        private static PersistentDictionary PrefabAddressOverrides => _prefabAddressOverrides ??=
            KSP2UnityToolsManager.GetDictionary("PrefabAddressOverrides");

        private static PersistentDictionary _iconAddressOverrides;

        private static PersistentDictionary IconAddressOverrides =>
            _iconAddressOverrides ??= KSP2UnityToolsManager.GetDictionary("IconAddressOverrides");


        private GameObject TargetObject => TargetData.gameObject;
        private CorePartData TargetData => target as CorePartData;
        private PartCore TargetCore => TargetData.Core;

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            GUILayout.Label("Attach Node Settings");
            if (GUILayout.Button("Auto Generate AttachNodes"))
            {
                TargetCore.data.attachNodes.Clear();
                // Attach node naming scheme
                foreach (AttachmentNode attachmentNode in TargetObject.GetComponentsInChildren<AttachmentNode>())
                {
                    GameObject obj = attachmentNode.gameObject;
                    Vector3 pos = TargetObject.transform.InverseTransformPoint(obj.transform.position);
                    Vector3 dir = Quaternion.Euler(
                        TargetObject.transform.InverseTransformDirection(obj.transform.rotation.eulerAngles)
                    ) * Vector3.forward;
                    var newDefinition = new AttachNodeDefinition
                    {
                        nodeID = obj.name,
                        NodeSymmetryGroupID = attachmentNode.nodeSymmetryGroupID,
                        nodeType = attachmentNode.nodeType,
                        attachMethod = attachmentNode.attachMethod,
                        IsMultiJoint = attachmentNode.isMultiJoint,
                        MultiJointMaxJoint = attachmentNode.multiJointMaxJoint,
                        MultiJointRadiusOffset = attachmentNode.multiJointRadiusOffset,
                        position = pos,
                        orientation = dir,
                        size = attachmentNode.size,
                        sizeKey = PartSizeRegistry.GetAttachNodeSizeKey(attachmentNode.sizeKey, attachmentNode.size),
                        visualSize = attachmentNode.visualSize,
                        angularStrengthMultiplier = attachmentNode.angularStrengthMultiplier,
                        contactArea = attachmentNode.contactArea,
                        overrideDragArea = attachmentNode.overrideDragArea,
                        isCompoundJoint = attachmentNode.isCompoundJoint
                    };
                    TargetCore.data.attachNodes.Add(newDefinition);
                }

                EditorUtility.SetDirty(target);
            }

            GUILayout.Label("Gizmo Settings", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            _centerOfMassGizmos = EditorGUILayout.Toggle("CoM gizmos", _centerOfMassGizmos);
            _centerOfLiftGizmos = EditorGUILayout.Toggle("CoL gizmos", _centerOfLiftGizmos);
            _attachNodeGizmos = EditorGUILayout.Toggle("Attach Node Gizmos", _attachNodeGizmos);
            DragCubeGizmos = EditorGUILayout.Toggle("Drag Cube Gizmos", DragCubeGizmos);
            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(target);
            }

            // GUILayout.Label("Address Overrides (Only Works With Patch Manager)", EditorStyles.boldLabel);
            // var prefabAddress = "%NAME%.prefab";
            // var iconAddress = "%NAME%.png";
            // if (PrefabAddressOverrides.TryGetValue(TargetObject.name, out var newPrefabAddress))
            //     prefabAddress = newPrefabAddress;
            // if (IconAddressOverrides.TryGetValue(TargetObject.name, out var newIconAddress))
            //     iconAddress = newIconAddress;
            // PrefabAddressOverrides[TargetObject.name] =
            //     prefabAddress = EditorGUILayout.TextField("Prefab Address", prefabAddress);
            // IconAddressOverrides[TargetObject.name] =
            //     iconAddress = EditorGUILayout.TextField("Icon Address", iconAddress);

            GUILayout.Label("Part Saving", EditorStyles.boldLabel);
            // var patchPath = "plugin_template/patches/%NAME%.patch";
            string prefabPath = PathUtils.GetPrefabOrAssetPath(TargetData, TargetObject);
            if (string.IsNullOrEmpty(prefabPath))
            {
                return;
            }

            string jsonPath = Path.GetDirectoryName(prefabPath) + $"/{TargetData.name}.json";
            if (!GUILayout.Button("Save Part JSON"))
            {
                return;
            }

            if (!_initialized)
            {
                Initialize();
            }

            if (TargetCore == null)
            {
                return;
            }

            // Clear out the serialized part modules and reserialize them
            TargetCore.data.serializedPartModules.Clear();
            foreach (Component child in TargetObject.GetComponents<Component>())
            {
                if (child is not PartBehaviourModule partBehaviourModule)
                {
                    continue;
                }

                MethodInfo addMethod =
                    child.GetType().GetMethod("AddDataModules", BindingFlags.Instance | BindingFlags.NonPublic) ??
                    child.GetType().GetMethod("AddDataModules", BindingFlags.Instance | BindingFlags.Public);
                addMethod?.Invoke(child, new object[] { });
                foreach (ModuleData data in partBehaviourModule.DataModules.Values)
                {
                    MethodInfo rebuildMethod =
                        data.GetType()
                            .GetMethod(
                                "RebuildDataContext",
                                BindingFlags.Instance | BindingFlags.NonPublic
                            ) ?? data.GetType()
                            .GetMethod("RebuildDataContext", BindingFlags.Instance | BindingFlags.Public);
                    rebuildMethod?.Invoke(data, new object[] { });
                }

                TargetCore.data.serializedPartModules.Add(new SerializedPartModule(partBehaviourModule, false));
            }

            string json = IOProvider.ToJson(TargetCore);
            JObject jObject = JObject.Parse(json);
            json = jObject.ToString(Formatting.Indented);
            string path = jsonPath.Replace("%NAME%", TargetCore.data.partName);
            string directoryName = new FileInfo(path).DirectoryName;
            Directory.CreateDirectory(directoryName);
            File.WriteAllText($"{path}", json);
            AssetDatabase.ImportAsset(path);
            bool madeAddressable = false;
            if (KSP2UnityTools.FindParentMod(target) is { } mod)
            {
                madeAddressable = true;
                AddressablesTools.MakeAddressable(
                    mod.partsGroup,
                    path,
                    $"{TargetCore.data.partName}.json",
                    "parts_data"
                );
            }

            AssetDatabase.Refresh();
            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog(
                "Part Exported",
                !madeAddressable
                    ? $"Json is at: {path}, you need to manually make it addressable"
                    : $"Json is at: {path}",
                "ok"
            );
        }

        [DrawGizmo(GizmoType.Active | GizmoType.Selected)]
        public static void DrawGizmoForPartCoreData(CorePartData data, GizmoType gizmoType)
        {
            Matrix4x4 localToWorldMatrix = data.transform.localToWorldMatrix;
            if (_centerOfMassGizmos)
            {
                Vector3 centerOfMassPosition = data.Data.coMassOffset;
                centerOfMassPosition = localToWorldMatrix.MultiplyPoint(centerOfMassPosition);
                Gizmos.DrawIcon(
                    centerOfMassPosition,
                    SDKConfiguration.BasePath + "/Assets/Gizmos/com_icon.png",
                    false
                );
            }

            if (_centerOfLiftGizmos)
            {
                Vector3 centerOfLiftPosition = data.Data.coLiftOffset;
                centerOfLiftPosition = localToWorldMatrix.MultiplyPoint(centerOfLiftPosition);
                Gizmos.DrawIcon(
                    centerOfLiftPosition,
                    SDKConfiguration.BasePath + "/Assets/Gizmos/col_icon.png",
                    false
                );
            }

            if (!_attachNodeGizmos)
            {
                return;
            }

            Gizmos.color = new Color(Color.green.r, Color.green.g, Color.green.b, 0.5f);
            foreach (AttachNodeDefinition attachNode in data.Data.attachNodes)
            {
                Vector3d pos = attachNode.position;
                pos = localToWorldMatrix.MultiplyPoint(pos);
                Vector3d dir = attachNode.orientation;
                dir = localToWorldMatrix.MultiplyVector(dir);
                Gizmos.DrawRay(pos, dir * 0.25f);
                Gizmos.DrawSphere(pos, 0.05f);
            }
        }

        [DrawGizmo(GizmoType.Active | GizmoType.Selected)]
        public static void DrawGizmoForAttachmentNode(AttachmentNode node, GizmoType gizmoType)
        {
            if (!_attachNodeGizmos)
            {
                return;
            }

            Gizmos.color = new Color(Color.green.r, Color.green.g, Color.green.b, 0.5f);
            Vector3 pos = node.transform.position;
            Gizmos.DrawRay(pos, node.transform.rotation * Vector3.forward * 0.25f);
            Gizmos.DrawSphere(pos, 0.05f);
        }
    }

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

    [CustomPropertyDrawer(typeof(AttachNodeDefinition))]
    public class AttachNodeDefinitionDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return PartSizeInspectorFields.GetFoldoutPropertyHeight(
                property,
                PartSizeInspectorFields.AttachNodeSizeFieldName,
                PartSizeInspectorFields.AttachNodeSizeKeyFieldName
            );
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            PartSizeInspectorFields.DrawFoldoutProperty(
                position,
                property,
                label,
                PartSizeInspectorFields.AttachNodeSizeFieldName,
                PartSizeInspectorFields.AttachNodeSizeKeyFieldName,
                "Node Size",
                PartSizeInspectorFields.LegacyPartSizeMode.AttachNode
            );
        }
    }

    [CustomEditor(typeof(AttachmentNode))]
    public class AttachmentNodeEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            SerializedProperty sizeProperty = serializedObject.FindProperty(PartSizeInspectorFields.AttachNodeSizeFieldName);
            SerializedProperty sizeKeyProperty = serializedObject.FindProperty(PartSizeInspectorFields.AttachNodeSizeKeyFieldName);
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
                        GetLegacyProperty(property, hiddenCompatibilityFieldName),
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
            return IsCustomKey(sizeKeyProperty?.stringValue) || CustomModePropertyPaths.Contains(GetPathKey(sizeKeyProperty))
                ? EditorGUIUtility.singleLineHeight * 2f + VerticalSpacing
                : EditorGUIUtility.singleLineHeight;
        }

        private static SerializedProperty GetSizeKeyProperty(SerializedProperty property, string sizeFieldName)
        {
            return sizeFieldName == AttachNodeSizeFieldName
                ? property.FindPropertyRelative(AttachNodeSizeKeyFieldName)
                : property.FindPropertyRelative(PartSizeFieldName);
        }

        private static SerializedProperty GetLegacyProperty(SerializedProperty property, string hiddenCompatibilityFieldName)
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
