using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using KSP.Modules;
using KSP.Sim;
using KSP.Sim.Definitions;
using Ksp2UnityTools.Editor.PartAuthoring.Inspectors.Sections;
using Ksp2UnityTools.Editor.PartAuthoring.Inspectors.Widgets;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PartAuthoring.Inspectors.DataEditors
{
    /// <summary>
    /// Custom editor for <see cref="Data_Engine" />. Replaces the generic field rendering of the
    /// <c>engineModes[]</c> array with collapsible mode cards, groups each mode's fields by their
    /// <see cref="HeaderAttribute" /> markers into sub-foldouts, and renders
    /// <c>ThrustTransformNamesMultipliers</c> as a path/multiplier table.
    /// </summary>
    [DataEditor(typeof(Data_Engine))]
    public sealed class EngineDataEditor : IDataEditor
    {
        private const BindingFlags FIELD_FLAGS =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        private const string USS_PATH = "/Assets/Windows/PartAuthoring/Inspectors/DataEditors/DataEditors.uss";

        private static List<HeaderGroup> _engineModeGroups;

        private PartBehaviourModule _module;
        private Transform _partRoot;

        /// <inheritdoc />
        public VisualElement Build(SerializedProperty dataProp, PartBehaviourModule module)
        {
            _module = module;
            _partRoot = module == null ? null : module.gameObject.transform;

            var root = new VisualElement();
            root.style.flexDirection = FlexDirection.Column;

            var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(SDKConfiguration.BasePath + USS_PATH);
            if (sheet != null)
            {
                root.styleSheets.Add(sheet);
            }

            var modesProp = dataProp.FindPropertyRelative("engineModes");
            root.Add(CardListSection.Build(modesProp, new CardListSection.Config
            {
                Title = "Engine Modes",
                AddButtonText = "+ Add Mode",
                IdentityFieldName = "engineID",
                ChipFieldName = "maxThrust",
                ChipFormatter = p => $"{p.floatValue.ToString("F0", CultureInfo.InvariantCulture)} kN",
                BuildBody = (entry, body) => BuildModeBody(body, entry),
                OnAddSeed = (entry, newIndex) =>
                {
                    var idProp = entry.FindPropertyRelative("engineID");
                    if (idProp != null)
                    {
                        idProp.stringValue = $"Mode{newIndex}";
                    }
                    var nameProp = entry.FindPropertyRelative("EngineDisplayName");
                    if (nameProp != null)
                    {
                        nameProp.stringValue = $"Mode{newIndex}";
                    }
                },
            }));
            root.Add(BuildTopLevelSection(dataProp));

            return root;
        }

        // -------------------- Mode body --------------------

        private void BuildModeBody(VisualElement body, SerializedProperty modeProp)
        {
            var groups = GetEngineModeGroups();
            foreach (var group in groups)
            {
                if (string.IsNullOrEmpty(group.Header))
                {
                    foreach (var field in group.Fields)
                    {
                        AddModeFieldRow(body, modeProp, field, labelPrefix: null);
                    }
                }
                else
                {
                    var foldout = new Foldout { text = group.Header, value = false };
                    foldout.AddToClassList("data-editor-subsection-foldout");
                    var prefix = ComputeCommonLabelPrefix(group.Fields);
                    foreach (var field in group.Fields)
                    {
                        AddModeFieldRow(foldout, modeProp, field, prefix);
                    }
                    body.Add(foldout);
                }
            }
        }

        private void AddModeFieldRow(VisualElement parent, SerializedProperty modeProp, FieldInfo field, string labelPrefix)
        {
            if (field.Name == "ThrustTransformNamesMultipliers")
            {
                var arrayProp = modeProp.FindPropertyRelative(field.Name);
                if (arrayProp != null)
                {
                    parent.Add(BuildThrustTransformsTable(arrayProp));
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

            var row = ReflectionModuleEditor.BuildFieldRowForCustomEditor(prop, field, _partRoot, labelOverride);
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

        // -------------------- ThrustTransformNamesMultipliers table --------------------

        private VisualElement BuildThrustTransformsTable(SerializedProperty arrayProp)
        {
            var outer = new VisualElement();
            outer.AddToClassList("data-editor-inline-block");
            outer.Add(InlineListBlock.Build(
                arrayProp,
                titleFormat: "Thrust Transform Multipliers ({0})",
                addButtonText: "+ Add",
                emptyHint: "(none - falls back to thrustVectorTransformName above)",
                rowBuilder: BuildThrustTransformRow));
            return outer;
        }

        private VisualElement BuildThrustTransformRow(SerializedProperty entry, int index, Action onDelete)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 2f;

            var nameProp = entry.FindPropertyRelative("ThrustTransformName");
            VisualElement nameField;
            if (nameProp != null)
            {
                nameField = new TransformGroupField(nameProp, string.Empty, _partRoot);
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

        // -------------------- Top-level (non-mode) Data_Engine fields --------------------

        private VisualElement BuildTopLevelSection(SerializedProperty dataProp)
        {
            var section = new VisualElement();
            section.AddToClassList("data-editor-section");

            var header = new Label("Emissive & Animation");
            header.AddToClassList("data-editor-section-header");
            section.Add(header);

            AddTopLevelField(section, dataProp, "UseEmissive");

            var emissiveNamesProp = dataProp.FindPropertyRelative("EmissiveMaterialNames");
            if (emissiveNamesProp != null)
            {
                section.Add(BuildEmissiveMaterialNamesBlock(emissiveNamesProp));
            }

            AddTopLevelField(section, dataProp, "EmissiveTemperatureCurve");
            AddTopLevelField(section, dataProp, "EmissiveLerpRateUp");
            AddTopLevelField(section, dataProp, "EmissiveLerpRateDown");
            AddTopLevelField(section, dataProp, "DeployedModeAnimationStateShortName");

            return section;
        }

        private VisualElement BuildEmissiveMaterialNamesBlock(SerializedProperty arrayProp)
        {
            return InlineListBlock.Build(
                arrayProp,
                titleFormat: "Emissive Material Names ({0})",
                addButtonText: "+ Add",
                emptyHint: "(none)",
                rowBuilder: BuildEmissiveMaterialNameRow);
        }

        private VisualElement BuildEmissiveMaterialNameRow(SerializedProperty entry, int index, Action onDelete)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 2f;

            var textField = new TextField { value = entry.stringValue, isDelayed = true };
            textField.style.flexGrow = 1f;
            textField.style.marginRight = 4f;
            textField.RegisterValueChangedCallback(evt =>
            {
                entry.serializedObject.Update();
                entry.stringValue = evt.newValue ?? string.Empty;
                entry.serializedObject.ApplyModifiedProperties();
            });
            row.Add(textField);

            var removeBtn = new Button(onDelete) { text = "X" };
            removeBtn.AddToClassList("data-editor-card-remove-btn");
            row.Add(removeBtn);

            return row;
        }

        private static void AddTopLevelField(VisualElement parent, SerializedProperty dataProp, string fieldName)
        {
            var prop = dataProp.FindPropertyRelative(fieldName);
            if (prop == null)
            {
                return;
            }
            var field = new PropertyField(prop);
            field.AddToClassList("unity-base-field__aligned");
            parent.Add(field);
        }

        // -------------------- EngineMode field grouping --------------------

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
