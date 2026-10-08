using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using KSP.Modules;
using KSP.Sim;
using KSP.Sim.Definitions;
using Ksp2UnityTools.Editor.PartAuthoring.Inspectors.Widgets;
using Ksp2UnityTools.Editor.Widgets;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PartAuthoring.Inspectors.DataEditors
{
    /// <summary>
    /// Renders the body of one <see cref="Data_Engine.EngineMode" /> card. Shared by <see cref="EngineDataEditor" /> (Modules tab, editing the part's own engine modes) and <see cref="Ksp2UnityTools.Editor.PartAuthoring.Inspectors.Variants.Editors.EngineModeSwapperEditor" /> (Variants tab, editing modes that replace the engine's modes when a variant activates).
    /// </summary>
    /// <remarks>
    /// Fields are grouped by <see cref="HeaderAttribute" /> markers into sub-foldouts. The <c>ThrustTransformNamesMultipliers</c> field is special-cased to render as an inline name/multiplier table. Everything else flows through <see cref="ReflectionModuleEditor.BuildFieldRowForCustomEditor" /> so [Unit], [TransformPath], etc. dispatch the same way as the per-Data_* editor.
    /// </remarks>
    internal static class EngineModeBodyBuilder
    {
        private const BindingFlags FIELD_FLAGS =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        private static List<HeaderGroup> _engineModeGroups;

        /// <summary>
        /// Appends the rendered body of a single engine-mode entry into <paramref name="body" />.
        /// </summary>
        /// <param name="body">Container to populate.</param>
        /// <param name="modeProp">SerializedProperty of one <c>Data_Engine.EngineMode</c>.</param>
        /// <param name="partRoot">The part root Transform, used by transform-path widgets in field rows.</param>
        public static void Build(VisualElement body, SerializedProperty modeProp, Transform partRoot)
        {
            var groups = GetEngineModeGroups();
            foreach (var group in groups)
            {
                if (string.IsNullOrEmpty(group.Header))
                {
                    foreach (var field in group.Fields)
                    {
                        AddModeFieldRow(body, modeProp, field, partRoot, labelPrefix: null);
                    }
                }
                else
                {
                    var foldout = new Foldout { text = group.Header, value = false };
                    foldout.AddToClassList("data-editor-subsection-foldout");
                    var prefix = ComputeCommonLabelPrefix(group.Fields);
                    foreach (var field in group.Fields)
                    {
                        AddModeFieldRow(foldout, modeProp, field, partRoot, prefix);
                    }
                    body.Add(foldout);
                }
            }
        }

        private static void AddModeFieldRow(VisualElement parent, SerializedProperty modeProp, FieldInfo field, Transform partRoot, string labelPrefix)
        {
            if (field.Name == "ThrustTransformNamesMultipliers")
            {
                var arrayProp = modeProp.FindPropertyRelative(field.Name);
                if (arrayProp != null)
                {
                    parent.Add(BuildThrustTransformsTable(arrayProp, partRoot));
                }
                return;
            }

            var prop = modeProp.FindPropertyRelative(field.Name);
            if (prop == null)
            {
                return;
            }

            string labelOverride = null;
            if (!string.IsNullOrEmpty(labelPrefix) && prop.displayName != null && prop.displayName.StartsWith(labelPrefix))
            {
                var stripped = prop.displayName.Substring(labelPrefix.Length);
                if (!string.IsNullOrWhiteSpace(stripped))
                {
                    labelOverride = stripped;
                }
            }

            var row = ReflectionModuleEditor.BuildFieldRowForCustomEditor(prop, field, partRoot, labelOverride);
            if (row != null)
            {
                parent.Add(row);
            }
        }

        private static string ComputeCommonLabelPrefix(IReadOnlyList<FieldInfo> fields)
        {
            if (fields == null || fields.Count < 2)
            {
                return null;
            }

            var names = new string[fields.Count];
            for (var i = 0; i < fields.Count; i++)
            {
                names[i] = ObjectNames.NicifyVariableName(fields[i].Name);
            }

            var minLen = int.MaxValue;
            foreach (var n in names)
            {
                if (n.Length < minLen) minLen = n.Length;
            }

            var common = 0;
            while (common < minLen)
            {
                var c = names[0][common];
                var allMatch = true;
                for (var i = 1; i < names.Length; i++)
                {
                    if (names[i][common] != c)
                    {
                        allMatch = false;
                        break;
                    }
                }
                if (!allMatch) break;
                common++;
            }
            if (common == 0)
            {
                return null;
            }

            var candidate = names[0].Substring(0, common);
            while (true)
            {
                var lastSpace = candidate.LastIndexOf(' ');
                if (lastSpace < 0)
                {
                    return null;
                }
                var prefix = candidate.Substring(0, lastSpace + 1);
                var allHaveContent = true;
                foreach (var n in names)
                {
                    if (n.Length <= prefix.Length || string.IsNullOrWhiteSpace(n.Substring(prefix.Length)))
                    {
                        allHaveContent = false;
                        break;
                    }
                }
                if (allHaveContent)
                {
                    return prefix;
                }
                candidate = candidate.Substring(0, lastSpace);
            }
        }

        private static VisualElement BuildThrustTransformsTable(SerializedProperty arrayProp, Transform partRoot)
        {
            var outer = new VisualElement();
            outer.AddToClassList("data-editor-inline-block");
            outer.Add(InlineListBlock.Build(
                arrayProp,
                titleFormat: "Thrust Transform Multipliers ({0})",
                addButtonText: "+ Add",
                emptyHint: "(none - falls back to thrustVectorTransformName above)",
                rowBuilder: (entry, index, onDelete) => BuildThrustTransformRow(entry, onDelete, partRoot)));
            return outer;
        }

        private static VisualElement BuildThrustTransformRow(SerializedProperty entry, Action onDelete, Transform partRoot)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 2f;

            var nameProp = entry.FindPropertyRelative("ThrustTransformName");
            VisualElement nameField;
            if (nameProp != null)
            {
                nameField = new TransformGroupField(nameProp, string.Empty, partRoot);
                nameField.style.flexGrow = 1f;
            }
            else
            {
                nameField = new Label("(missing name field)");
            }
            row.Add(nameField);

            var multProp = entry.FindPropertyRelative("ThrustTransformMultiplier");
            var multField = new FloatField { value = multProp?.floatValue ?? 0f, isDelayed = true };
            multField.style.width = 70f;
            multField.style.marginLeft = 4f;
            multField.style.marginRight = 4f;
            if (multProp != null)
            {
                multField.RegisterValueChangedCallback(evt =>
                {
                    multProp.serializedObject.Update();
                    multProp.floatValue = evt.newValue;
                    multProp.serializedObject.ApplyModifiedProperties();
                });
            }
            row.Add(multField);

            var removeBtn = new Button(onDelete) { text = "X" };
            removeBtn.AddToClassList("data-editor-card-remove-btn");
            row.Add(removeBtn);

            return row;
        }

        private struct HeaderGroup
        {
            public string Header;
            public List<FieldInfo> Fields;
        }

        private static List<HeaderGroup> GetEngineModeGroups()
        {
            if (_engineModeGroups != null)
            {
                return _engineModeGroups;
            }

            var allFields = typeof(Data_Engine.EngineMode)
                .GetFields(FIELD_FLAGS)
                .OrderBy(f => f.MetadataToken)
                .ToList();

            var groups = new List<HeaderGroup>();
            var current = new HeaderGroup { Header = null, Fields = new List<FieldInfo>() };

            foreach (var field in allFields)
            {
                if (!ShouldRender(field))
                {
                    continue;
                }
                var hdr = field.GetCustomAttribute<HeaderAttribute>();
                if (hdr != null)
                {
                    if (current.Fields.Count > 0)
                    {
                        groups.Add(current);
                    }
                    current = new HeaderGroup { Header = hdr.header, Fields = new List<FieldInfo>() };
                }
                current.Fields.Add(field);
            }
            if (current.Fields.Count > 0)
            {
                groups.Add(current);
            }

            _engineModeGroups = groups;
            return _engineModeGroups;
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
    }
}
