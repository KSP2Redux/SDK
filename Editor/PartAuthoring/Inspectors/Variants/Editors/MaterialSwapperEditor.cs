using System.Collections.Generic;
using KSP;
using Ksp2UnityTools.Editor.PartAuthoring.Inspectors.Sections;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VSwift.Modules.Transformers;

namespace Ksp2UnityTools.Editor.PartAuthoring.Inspectors.Variants.Editors
{
    /// <summary>
    /// Custom editor for <see cref="MaterialSwapper" />. Renders the <c>Swaps</c> dictionary as a grid table with two columns: source material name (dropdown of names from the prefab's renderers) and replacement addressables key (text field).
    /// </summary>
    /// <remarks>
    /// Material names come from the part's <c>model/</c> subtree at Build time. The <c>" (Instance)"</c> suffix that Unity appends to runtime material instances is stripped to match the runtime swap convention. The addressables key cell is a plain TextField in V1; a proper addressables picker is a separate widget contribution.
    /// </remarks>
    [TransformerEditor(typeof(MaterialSwapper))]
    public sealed class MaterialSwapperEditor : ITransformerEditor
    {
        private const string MATERIAL_INSTANCE_SUFFIX = " (Instance)";
        private const string MATERIAL_CLONE_SUFFIX = " (Clone)";

        public VisualElement Build(ITransformer transformer, SerializedProperty transformerProp, TransformerEditorContext context)
        {
            var outer = new VisualElement();

            SerializedProperty swapsProp = transformerProp?.FindPropertyRelative("Swaps");
            if (swapsProp == null)
            {
                outer.Add(new HelpBox("Swaps not found on this transformer.", HelpBoxMessageType.Error));
                return outer;
            }
            SerializedProperty entriesProp = swapsProp.FindPropertyRelative("_entries");
            if (entriesProp == null)
            {
                outer.Add(new HelpBox("Swaps backing _entries not found. Was SerializedDictionary's backing changed?", HelpBoxMessageType.Error));
                return outer;
            }

            List<string> materialNames = GetMaterialNames(context?.Part);

            var table = new SerializedArrayTable(
                entriesProp,
                title: null,
                addButtonText: "+ Add",
                columns: new[]
                {
                    new SerializedTableColumn
                    {
                        HeaderLabel = "From",
                        PropertyName = "Key",
                        Kind = SerializedTableColumnKind.Custom,
                        Flex = 1f,
                        CustomBuilder = prop => BuildMaterialDropdown(prop, materialNames),
                    },
                    new SerializedTableColumn
                    {
                        HeaderLabel = "To",
                        PropertyName = "Value",
                        Kind = SerializedTableColumnKind.Text,
                        Flex = 1f,
                    },
                });
            outer.Add(table.Build());
            return outer;
        }

        private static VisualElement BuildMaterialDropdown(SerializedProperty prop, List<string> choices)
        {
            var dropdown = new DropdownField();
            if (choices == null || choices.Count == 0)
            {
                dropdown.choices = new List<string> { "(no materials on part)" };
                dropdown.SetValueWithoutNotify("(no materials on part)");
                dropdown.SetEnabled(false);
                return dropdown;
            }
            dropdown.choices = choices;
            string stored = prop.stringValue ?? string.Empty;
            dropdown.SetValueWithoutNotify(stored);
            dropdown.RegisterValueChangedCallback(evt =>
            {
                prop.serializedObject.Update();
                prop.stringValue = evt.newValue ?? string.Empty;
                prop.serializedObject.ApplyModifiedProperties();
            });
            return dropdown;
        }

        private static List<string> GetMaterialNames(CorePartData part)
        {
            var seen = new HashSet<string>();
            var result = new List<string>();
            if (part == null || part.transform == null)
            {
                return result;
            }
            // Mirror MaterialSwapper.RecursivelySwitch's runtime walk: scan every Renderer under
            // the part root (not just model/), and strip both " (Clone)" and " (Instance)" suffixes
            // so authored names match what the runtime matches against.
            var renderers = part.transform.GetComponentsInChildren<Renderer>(includeInactive: true);
            foreach (Renderer r in renderers)
            {
                if (r == null)
                {
                    continue;
                }
                var mats = r.sharedMaterials;
                foreach (Material mat in mats)
                {
                    if (mat == null || string.IsNullOrEmpty(mat.name))
                    {
                        continue;
                    }
                    string clean = StripRuntimeSuffixes(mat.name);
                    if (seen.Add(clean))
                    {
                        result.Add(clean);
                    }
                }
            }
            return result;
        }

        private static string StripRuntimeSuffixes(string name)
        {
            if (name.EndsWith(MATERIAL_INSTANCE_SUFFIX))
            {
                name = name.Substring(0, name.Length - MATERIAL_INSTANCE_SUFFIX.Length);
            }
            if (name.EndsWith(MATERIAL_CLONE_SUFFIX))
            {
                name = name.Substring(0, name.Length - MATERIAL_CLONE_SUFFIX.Length);
            }
            return name;
        }
    }
}
