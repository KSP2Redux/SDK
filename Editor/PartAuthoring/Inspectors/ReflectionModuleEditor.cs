using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using KSP;
using KSP.Game;
using KSP.Sim;
using KSP.Sim.Definitions;
using Ksp2UnityTools.Editor.PartAuthoring.Inspectors.Fields;
using Ksp2UnityTools.Editor.PartAuthoring.Inspectors.Sections;
using Ksp2UnityTools.Editor.PartAuthoring.Inspectors.Widgets;
using Ksp2UnityTools.Editor.PartAuthoring.SceneTools;
using Redux.Modules.Attributes;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PartAuthoring.Inspectors
{
    /// <summary>
    /// Renders a <see cref="PartBehaviourModule" />'s <c>Data_*</c> blocks as author-facing fields,
    /// filtered by <see cref="KSPDefinitionAttribute" /> and dispatched per the attribute vocabulary
    /// declared in <c>Redux.Modules.Attributes</c>.
    /// </summary>
    /// <remarks>
    /// This is the generic editor that handles every module without a custom editor. It walks the
    /// module's serialized fields, identifies sub-fields whose type derives from <see cref="ModuleData" />,
    /// and renders each Data block by walking the visible properties inside it.
    ///
    /// Skipped fields: anything tagged <see cref="KSPStateAttribute" /> (runtime state), tagged
    /// <see cref="HideInInspector" />, or missing <see cref="KSPDefinitionAttribute" /> (the include
    /// trigger).
    ///
    /// Honored attributes this phase: <c>[Range]</c> (rendered as Slider by Unity's PropertyField),
    /// <c>[Unit]</c> (suffix label after the field), <c>[Tooltip]</c> (inherited from Unity).
    /// <c>[TransformPath]</c>, <c>[ResourceName]</c>, <c>[FoldoutSection]</c>, and <c>[Hidden]</c>
    /// land in follow-up wedges.
    /// </remarks>
    internal static class ReflectionModuleEditor
    {
        private const BindingFlags FIELD_FLAGS =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        private const string DATA_TYPE_PREFIX = "Data_";

        private const string USS_PATH = "/Assets/Windows/PartAuthoring/Inspectors/DataEditors/DataEditors.uss";

        /// <summary>
        /// Builds the editor VisualElement for the given module. Caller is expected to host the
        /// result inside the module's card body.
        /// </summary>
        /// <param name="module">The module Component being edited.</param>
        /// <param name="corePartDataSo">A SerializedObject of the part's <see cref="CorePartData" />,
        /// used to surface per-module PAM overrides that live on <see cref="PartData" />.</param>
        public static VisualElement Build(PartBehaviourModule module, SerializedObject corePartDataSo)
        {
            var root = new VisualElement();
            root.AddToClassList("reflection-module-editor");

            var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(SDKConfiguration.BasePath + USS_PATH);
            if (sheet != null)
            {
                root.styleSheets.Add(sheet);
            }

            if (module == null)
            {
                return root;
            }

            var so = new SerializedObject(module);
            var dataFields = EnumerateDataFields(module.GetType()).ToList();
            var partRoot = module.gameObject.transform;

            foreach (var dataField in dataFields)
            {
                var dataProp = so.FindProperty(dataField.Name);
                if (dataProp == null)
                {
                    continue;
                }
                var block = BuildDataBlockDispatch(dataField.FieldType, dataProp, partRoot, module);
                if (block.childCount > 0)
                {
                    root.Add(block);
                }
            }

            var moduleLevelSection = BuildModuleLevelSection(so, module.GetType(), partRoot);
            if (moduleLevelSection != null)
            {
                root.Add(moduleLevelSection);
            }

            var pamSection = BuildPamOverridesSection(corePartDataSo, module.PartComponentModuleType);
            if (pamSection != null)
            {
                root.Add(pamSection);
            }

            root.Bind(so);
            pamSection?.Bind(corePartDataSo);
            return root;
        }

        private static IEnumerable<FieldInfo> EnumerateDataFields(Type moduleType)
        {
            var current = moduleType;
            var seen = new HashSet<string>();
            while (current != null && current != typeof(object))
            {
                foreach (var field in current.GetFields(FIELD_FLAGS))
                {
                    if (!typeof(ModuleData).IsAssignableFrom(field.FieldType))
                    {
                        continue;
                    }
                    if (!field.IsPublic && !field.IsDefined(typeof(SerializeField), inherit: true))
                    {
                        continue;
                    }
                    if (seen.Add(field.Name))
                    {
                        yield return field;
                    }
                }
                current = current.BaseType;
            }
        }

        private static VisualElement BuildDataBlockDispatch(Type dataType, SerializedProperty dataProp, Transform partRoot, PartBehaviourModule module)
        {
            if (DataEditorRegistry.TryCreate(dataType, out var customEditor))
            {
                var block = new VisualElement();
                block.AddToClassList("reflection-module-editor__data-block");

                var header = new Label(GetDataBlockDisplayName(dataType));
                header.AddToClassList("reflection-module-editor__data-header");
                block.Add(header);

                var content = customEditor.Build(dataProp, module);
                if (content != null)
                {
                    block.Add(content);
                }
                return block;
            }
            return BuildDataBlock(dataType, dataProp, partRoot, showHeader: true);
        }

        /// <summary>
        /// Renders a single field row for the given SerializedProperty using the generic dispatch
        /// (attribute-aware <see cref="PropertyField" />, SteppedRange slider, Unit suffix wrap, etc).
        /// Exposed so custom <see cref="IDataEditor" /> implementations can reuse the generic field
        /// rendering for simple fields and only custom-render the parts that need it.
        /// </summary>
        /// <param name="labelOverride">If non-null, used as the row's label instead of the auto-generated
        /// <see cref="SerializedProperty.displayName" />. Useful when a custom editor strips redundant
        /// shared prefixes from labels within a group.</param>
        public static VisualElement BuildFieldRowForCustomEditor(SerializedProperty prop, FieldInfo field, Transform partRoot, string labelOverride = null)
        {
            return BuildFieldRow(prop, field, partRoot, labelOverride);
        }

        private static VisualElement BuildDataBlock(Type dataType, SerializedProperty dataProp, Transform partRoot, bool showHeader)
        {
            var block = new VisualElement();
            block.AddToClassList("reflection-module-editor__data-block");

            var rows = new List<VisualElement>();

            var iterator = dataProp.Copy();
            var end = iterator.GetEndProperty();

            var first = true;
            while (iterator.NextVisible(first))
            {
                first = false;
                if (SerializedProperty.EqualContents(iterator, end))
                {
                    break;
                }

                var field = FindField(dataType, iterator.name);
                if (!ShouldRender(field))
                {
                    continue;
                }

                var row = BuildFieldRow(iterator.Copy(), field, partRoot);
                if (row != null)
                {
                    rows.Add(row);
                }
            }

            if (rows.Count == 0)
            {
                return block;
            }

            if (showHeader)
            {
                var header = new Label(GetDataBlockDisplayName(dataType));
                header.AddToClassList("reflection-module-editor__data-header");
                block.Add(header);
            }

            foreach (var row in rows)
            {
                block.Add(row);
            }

            return block;
        }

        private static VisualElement BuildModuleLevelSection(SerializedObject so, Type moduleType, Transform partRoot)
        {
            var rows = new List<VisualElement>();
            foreach (var field in EnumerateModuleLevelFields(moduleType))
            {
                var prop = so.FindProperty(field.Name);
                if (prop == null)
                {
                    continue;
                }
                var row = BuildFieldRow(prop, field, partRoot);
                if (row != null)
                {
                    rows.Add(row);
                }
            }

            if (rows.Count == 0)
            {
                return null;
            }

            var section = new VisualElement();
            section.AddToClassList("reflection-module-editor__data-block");

            var header = new Label("Module");
            header.AddToClassList("reflection-module-editor__data-header");
            section.Add(header);

            foreach (var row in rows)
            {
                section.Add(row);
            }
            return section;
        }

        private static IEnumerable<FieldInfo> EnumerateModuleLevelFields(Type moduleType)
        {
            var current = moduleType;
            var seen = new HashSet<string>();
            while (current != null && current != typeof(object) && current != typeof(PartBehaviourModule))
            {
                foreach (var field in current.GetFields(FIELD_FLAGS))
                {
                    if (typeof(ModuleData).IsAssignableFrom(field.FieldType))
                    {
                        continue;
                    }
                    if (!field.IsPublic && !field.IsDefined(typeof(SerializeField), inherit: true))
                    {
                        continue;
                    }
                    if (field.IsDefined(typeof(KSPStateAttribute), inherit: true))
                    {
                        continue;
                    }
                    if (field.IsDefined(typeof(HideInInspector), inherit: true))
                    {
                        continue;
                    }
                    if (seen.Add(field.Name))
                    {
                        yield return field;
                    }
                }
                current = current.BaseType;
            }
        }


        private static string GetDataBlockDisplayName(Type dataType)
        {
            var n = dataType.Name;
            return n.StartsWith(DATA_TYPE_PREFIX) ? n.Substring(DATA_TYPE_PREFIX.Length) : n;
        }

        private static VisualElement BuildPamOverridesSection(SerializedObject coreDataSo, Type partComponentModuleType)
        {
            if (coreDataSo == null || partComponentModuleType == null)
            {
                return null;
            }
            var moduleName = partComponentModuleType.Name;

            var section = new VisualElement();
            section.AddToClassList("reflection-module-editor__data-block");

            var header = new Label("PAM Overrides");
            header.AddToClassList("reflection-module-editor__data-header");
            section.Add(header);

            var content = new VisualElement();
            section.Add(content);

            void Rebuild()
            {
                coreDataSo.Update();
                content.Clear();
                content.Add(BuildPamOverrideBlock(
                    coreDataSo,
                    listPath: "core.data.PAMModuleSortOverride",
                    moduleName: moduleName,
                    toggleLabel: "Sort Override",
                    fieldNames: new[] { "sortIndex" },
                    fieldLabels: new[] { "Sort Index" },
                    onRebuild: Rebuild));
                content.Add(BuildPamOverrideBlock(
                    coreDataSo,
                    listPath: "core.data.PAMModuleVisualsOverride",
                    moduleName: moduleName,
                    toggleLabel: "Visuals Override",
                    fieldNames: new[] { "ModuleDisplayName", "ShowHeader", "ShowFooter" },
                    fieldLabels: new[] { "Module Display Name", "Show Header", "Show Footer" },
                    onRebuild: Rebuild));
                content.Bind(coreDataSo);
            }

            Rebuild();
            return section;
        }

        private static VisualElement BuildPamOverrideBlock(
            SerializedObject coreDataSo,
            string listPath,
            string moduleName,
            string toggleLabel,
            string[] fieldNames,
            string[] fieldLabels,
            Action onRebuild)
        {
            var block = new VisualElement();
            block.AddToClassList("reflection-module-editor__pam-block");

            var listProp = coreDataSo.FindProperty(listPath);
            var existingIndex = listProp != null ? FindPamEntryIndex(listProp, moduleName) : -1;
            var hasEntry = existingIndex >= 0;

            var toggle = new Toggle(toggleLabel) { value = hasEntry };
            toggle.AddToClassList("unity-base-field__aligned");
            block.Add(toggle);

            var fieldsContainer = new VisualElement();
            fieldsContainer.AddToClassList("reflection-module-editor__pam-fields");
            fieldsContainer.SetEnabled(hasEntry);
            block.Add(fieldsContainer);

            if (hasEntry && listProp != null)
            {
                var entry = listProp.GetArrayElementAtIndex(existingIndex);
                for (var i = 0; i < fieldNames.Length; i++)
                {
                    var fieldProp = entry.FindPropertyRelative(fieldNames[i]);
                    if (fieldProp == null)
                    {
                        continue;
                    }
                    var pf = new PropertyField(fieldProp, fieldLabels[i]);
                    pf.AddToClassList("unity-base-field__aligned");
                    fieldsContainer.Add(pf);
                }
            }

            toggle.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue)
                {
                    AddPamEntry(coreDataSo, listPath, moduleName);
                }
                else
                {
                    RemovePamEntry(coreDataSo, listPath, moduleName);
                }
                EditorApplication.delayCall += () => onRebuild();
            });

            return block;
        }

        private static int FindPamEntryIndex(SerializedProperty listProp, string moduleName)
        {
            for (var i = 0; i < listProp.arraySize; i++)
            {
                var entry = listProp.GetArrayElementAtIndex(i);
                var nameProp = entry.FindPropertyRelative("PartComponentModuleName");
                if (nameProp != null && nameProp.stringValue == moduleName)
                {
                    return i;
                }
            }
            return -1;
        }

        private static void AddPamEntry(SerializedObject so, string listPath, string moduleName)
        {
            so.Update();
            var listProp = so.FindProperty(listPath);
            if (listProp == null)
            {
                return;
            }
            var newIndex = listProp.arraySize;
            listProp.arraySize++;
            so.ApplyModifiedProperties();
            so.Update();
            listProp = so.FindProperty(listPath);
            var entry = listProp.GetArrayElementAtIndex(newIndex);
            var nameProp = entry.FindPropertyRelative("PartComponentModuleName");
            if (nameProp != null)
            {
                nameProp.stringValue = moduleName;
            }
            so.ApplyModifiedProperties();
        }

        private static void RemovePamEntry(SerializedObject so, string listPath, string moduleName)
        {
            so.Update();
            var listProp = so.FindProperty(listPath);
            if (listProp == null)
            {
                return;
            }
            for (var i = listProp.arraySize - 1; i >= 0; i--)
            {
                var entry = listProp.GetArrayElementAtIndex(i);
                var nameProp = entry.FindPropertyRelative("PartComponentModuleName");
                if (nameProp != null && nameProp.stringValue == moduleName)
                {
                    listProp.DeleteArrayElementAtIndex(i);
                }
            }
            so.ApplyModifiedProperties();
        }

        private static bool ShouldRender(FieldInfo field)
        {
            if (field == null)
            {
                return false;
            }
            if (field.IsDefined(typeof(KSPStateAttribute), inherit: true))
            {
                return false;
            }
            if (field.IsDefined(typeof(HideInInspector), inherit: true))
            {
                return false;
            }
            if (!field.IsDefined(typeof(KSPDefinitionAttribute), inherit: true))
            {
                return false;
            }
            return true;
        }

        private static VisualElement BuildFieldRow(SerializedProperty prop, FieldInfo field, Transform partRoot, string labelOverride = null)
        {
            return AttributeAwareFieldRow.Build(prop, field, partRoot, labelOverride);
        }

        private static FieldInfo FindField(Type type, string name)
        {
            while (type != null && type != typeof(object))
            {
                var field = type.GetField(name, FIELD_FLAGS);
                if (field != null)
                {
                    return field;
                }
                type = type.BaseType;
            }
            return null;
        }
    }
}
