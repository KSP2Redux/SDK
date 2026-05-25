using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using KSP;
using KSP.Sim;
using KSP.Sim.Definitions;
using Ksp2UnityTools.Editor.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using VSwift.Modules.Transformers;

namespace Ksp2UnityTools.Editor.PartAuthoring.Inspectors.Variants.Editors
{
    /// <summary>
    /// Custom editor for <see cref="ModuleDefinitionTransformer" />. Chains three constrained dropdowns - behaviour module, data class, and field name - then a JSON value text input for the replacement payload.
    /// </summary>
    /// <remarks>
    /// Key options reflect the chosen DataType's authorable fields (same KSPDefinition + non-HideInInspector + non-KSPState filter the engine data editor uses). Field names are kept as their raw C# declarations - this matches both the JSON serialization name and the runtime's <c>Type.GetField</c> lookup, so the value the dropdown writes is the value the runtime resolves against.
    /// </remarks>
    [TransformerEditor(typeof(ModuleDefinitionTransformer))]
    public sealed class ModuleDefinitionTransformerEditor : ITransformerEditor
    {
        private const BindingFlags FIELD_FLAGS =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        /// <inheritdoc />
        public VisualElement Build(ITransformer transformer, SerializedProperty transformerProp, TransformerEditorContext context)
        {
            var outer = new VisualElement();

            SerializedProperty behaviourProp = transformerProp?.FindPropertyRelative("BehaviourType");
            SerializedProperty dataProp = transformerProp?.FindPropertyRelative("DataType");
            SerializedProperty keyProp = transformerProp?.FindPropertyRelative("Key");
            SerializedProperty valueProp = transformerProp?.FindPropertyRelative("_valueSerialized");

            if (behaviourProp == null || dataProp == null || keyProp == null || valueProp == null)
            {
                outer.Add(new HelpBox("ModuleDefinitionTransformer fields not found.", HelpBoxMessageType.Error));
                return outer;
            }

            List<string> behaviourChoices = GetPartBehaviourTypeNames(context?.Part);
            List<string> dataChoices = GetDataTypeNamesForBehaviour(behaviourProp.stringValue);

            DropdownField behaviourDropdown = BuildDropdown("Behaviour Type", behaviourChoices, behaviourProp,
                "Module on the part whose data is replaced when the variant activates.");
            outer.Add(behaviourDropdown);

            DropdownField dataDropdown = BuildDropdown("Data Type", dataChoices, dataProp,
                "Module data class containing the field to replace.");
            outer.Add(dataDropdown);

            DropdownField keyDropdown = BuildDropdown("Key", GetKeyChoices(dataProp.stringValue), keyProp,
                "Field name on the data class whose value is replaced.");
            outer.Add(keyDropdown);

            behaviourDropdown.RegisterValueChangedCallback(_ =>
            {
                List<string> newDataChoices = GetDataTypeNamesForBehaviour(behaviourProp.stringValue);
                string currentData = dataProp.stringValue ?? string.Empty;
                if (!string.IsNullOrEmpty(currentData) && !newDataChoices.Contains(currentData))
                {
                    newDataChoices.Add(currentData);
                }
                dataDropdown.choices = newDataChoices;
                dataDropdown.SetValueWithoutNotify(currentData);
            });

            dataDropdown.RegisterValueChangedCallback(_ =>
            {
                List<string> newChoices = GetKeyChoices(dataProp.stringValue);
                keyDropdown.choices = newChoices;
                string currentKey = keyProp.stringValue ?? string.Empty;
                if (!string.IsNullOrEmpty(currentKey) && !newChoices.Contains(currentKey))
                {
                    newChoices.Add(currentKey);
                    keyDropdown.choices = newChoices;
                }
                keyDropdown.SetValueWithoutNotify(currentKey);
            });

            var valueField = new TextField("Value (JSON)")
            {
                isDelayed = true,
                tooltip = "JSON literal deserialized into the field when the variant is active. Number (42), string (\"text\"), bool (true), array, or object.",
            };
            valueField.AddToClassList("unity-base-field");
            valueField.AddToClassList("unity-base-field__aligned");
            valueField.BindProperty(valueProp);
            outer.Add(valueField);

            return outer;
        }

        private static List<string> GetTypeNames<TBase>()
        {
            var list = ReduxTypeCache.GetTypesDerivedFrom<TBase>()
                .Select(t => t.Name)
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToList();
            return list;
        }

        private static List<string> GetDataTypeNamesForBehaviour(string behaviourTypeName)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(behaviourTypeName))
            {
                return result;
            }
            Type behaviour = ReduxTypeCache.GetTypesDerivedFrom<PartBehaviourModule>()
                .FirstOrDefault(t => t.Name == behaviourTypeName);
            if (behaviour == null)
            {
                return result;
            }
            var seen = new HashSet<string>();
            const BindingFlags FLAGS =
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            foreach (FieldInfo field in behaviour.GetFields(FLAGS))
            {
                if (field == null || field.FieldType == null)
                {
                    continue;
                }
                if (!typeof(ModuleData).IsAssignableFrom(field.FieldType))
                {
                    continue;
                }
                string name = field.FieldType.Name;
                if (seen.Add(name))
                {
                    result.Add(name);
                }
            }
            result.Sort(StringComparer.Ordinal);
            return result;
        }

        private static List<string> GetPartBehaviourTypeNames(CorePartData part)
        {
            var result = new List<string>();
            if (part == null)
            {
                return result;
            }
            var seen = new HashSet<string>();
            foreach (PartBehaviourModule module in part.GetComponents<PartBehaviourModule>())
            {
                if (module == null)
                {
                    continue;
                }
                string name = module.GetType().Name;
                if (seen.Add(name))
                {
                    result.Add(name);
                }
            }
            result.Sort(StringComparer.Ordinal);
            return result;
        }

        private static List<string> GetKeyChoices(string dataTypeName)
        {
            if (string.IsNullOrEmpty(dataTypeName))
            {
                return new List<string>();
            }
            Type resolved = ReduxTypeCache.GetTypesDerivedFrom<ModuleData>()
                .FirstOrDefault(t => t.Name == dataTypeName);
            if (resolved == null)
            {
                return new List<string>();
            }
            var names = new List<string>();
            foreach (FieldInfo f in resolved.GetFields(FIELD_FLAGS))
            {
                if (!ShouldRender(f))
                {
                    continue;
                }
                names.Add(f.Name);
            }
            names.Sort(StringComparer.Ordinal);
            return names;
        }

        private static bool ShouldRender(FieldInfo field)
        {
            if (field == null) return false;
            if (field.IsDefined(typeof(KSPStateAttribute), inherit: true)) return false;
            if (field.IsDefined(typeof(UnityEngine.HideInInspector), inherit: true)) return false;
            if (!field.IsDefined(typeof(KSPDefinitionAttribute), inherit: true)) return false;
            return true;
        }

        private static DropdownField BuildDropdown(string label, List<string> choices, SerializedProperty prop, string tooltip)
        {
            var dropdown = new DropdownField(label);
            dropdown.tooltip = tooltip;
            dropdown.AddToClassList("unity-base-field");
            dropdown.AddToClassList("unity-base-field__aligned");

            string stored = prop.stringValue ?? string.Empty;
            if (!string.IsNullOrEmpty(stored) && !choices.Contains(stored))
            {
                choices.Add(stored);
            }
            dropdown.choices = choices;
            dropdown.SetValueWithoutNotify(stored);

            dropdown.RegisterValueChangedCallback(evt =>
            {
                prop.serializedObject.Update();
                prop.stringValue = evt.newValue ?? string.Empty;
                prop.serializedObject.ApplyModifiedProperties();
            });
            return dropdown;
        }
    }
}
