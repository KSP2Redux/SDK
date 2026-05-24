using System.Collections.Generic;
using KSP;
using KSP.Game;
using Ksp2UnityTools.Editor.PartAuthoring.Inspectors.Sections;
using Ksp2UnityTools.Editor.PartAuthoring.Inspectors.Tabs.Variants;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using VSwift.Modules.Behaviours;
using VSwift.Modules.Data;
using VSwift.Modules.Variants;

namespace Ksp2UnityTools.Editor.PartAuthoring.Inspectors.Tabs
{
    /// <summary>
    /// Variants tab content. Authoring surface for V-SwiFT <see cref="Module_PartSwitch" /> data.
    /// </summary>
    /// <remarks>
    /// Two layouts: an empty state when the part has no <see cref="Module_PartSwitch" /> attached (single "Add PartSwitch Module" button), and a populated state when the module is present (variant-set list + remove button). Step 4 renders VariantSet cards via the shared <see cref="CardListSection" /> scaffold used by engine modes, science experiments, and resource-converter formulas.
    /// </remarks>
    internal static class VariantsTab
    {
        private const string USS_PATH = "/Assets/Windows/PartAuthoring/Inspectors/Tabs/VariantsTab.uss";
        private const string DataEditorsUssPath = "/Assets/Windows/PartAuthoring/Inspectors/DataEditors/DataEditors.uss";
        private const string ModulesTabUssPath = "/Assets/Windows/PartAuthoring/Inspectors/Tabs/ModulesTab.uss";

        private const string PartSwitchComponentModuleName = "PartComponentModule_PartSwitch";
        private const string PartSwitchPamDisplayName = "VSwift/PartSwitch";

        public static VisualElement Build(CorePartData target)
        {
            var root = new VisualElement();
            root.AddToClassList("variants-tab");

            Ksp2UnityToolsStyles.Apply(root, USS_PATH);
            Ksp2UnityToolsStyles.Apply(root, DataEditorsUssPath);
            Ksp2UnityToolsStyles.Apply(root, ModulesTabUssPath);

            void Rebuild()
            {
                root.Clear();
                Module_PartSwitch module = target.gameObject.GetComponent<Module_PartSwitch>();
                if (module == null)
                {
                    BuildEmptyState(root, target, Rebuild);
                }
                else
                {
                    BuildPopulatedState(root, target, module, Rebuild);
                }
            }

            Rebuild();
            return root;
        }

        private static void BuildEmptyState(VisualElement root, CorePartData target, System.Action rebuild)
        {
            var help = new HelpBox(
                "This part has no variants. Variants are owned by the PartSwitch module. Add the module to begin authoring variant sets.",
                HelpBoxMessageType.Info);
            help.AddToClassList("variants-tab-empty-help");
            root.Add(help);

            var addBtn = new Button(() => AddPartSwitchModule(target, rebuild))
            {
                text = "Add PartSwitch Module",
                tooltip = "Attaches Module_PartSwitch to this part and prepares an empty Data_PartSwitch carrier.",
            };
            addBtn.AddToClassList("variants-tab-add-module-btn");
            root.Add(addBtn);
        }

        private static void BuildPopulatedState(VisualElement root, CorePartData target, Module_PartSwitch module, System.Action rebuild)
        {
            var so = new SerializedObject(module);
            var variantSetsProp = so.FindProperty("_dataPartSwitch.VariantSets");
            if (variantSetsProp == null)
            {
                root.Add(new HelpBox("Module_PartSwitch is in an unexpected state (no _dataPartSwitch). Try removing and re-adding the module.", HelpBoxMessageType.Warning));
                root.Add(BuildRemoveButton(module, rebuild));
                return;
            }

            var section = CardListSection.Build(variantSetsProp, new CardListSection.Config
            {
                Title = "Variant Sets",
                AddButtonText = "+ Add",
                IdentityFieldName = "VariantSetId",
                BuildBody = (entry, body) => BuildVariantSetBody(entry, body),
                OnAddSeed = (entry, index) =>
                {
                    var idProp = entry.FindPropertyRelative("VariantSetId");
                    if (idProp != null)
                    {
                        idProp.stringValue = $"set_{index + 1}";
                    }
                    var locKeyProp = entry.FindPropertyRelative("VariantSetLocalizationKey");
                    if (locKeyProp != null)
                    {
                        locKeyProp.stringValue = string.Empty;
                    }
                    var popoutProp = entry.FindPropertyRelative("IsPopout");
                    if (popoutProp != null)
                    {
                        popoutProp.boolValue = false;
                    }
                },
            });
            root.Add(section);
            root.Bind(so);

            root.Add(BuildRemoveButton(module, rebuild));
        }

        private static void BuildVariantSetBody(SerializedProperty entry, VisualElement body)
        {
            var locKeyProp = entry.FindPropertyRelative("VariantSetLocalizationKey");
            if (locKeyProp != null)
            {
                body.Add(new PropertyField(locKeyProp, "Localization Key"));
            }

            var popoutProp = entry.FindPropertyRelative("IsPopout");
            if (popoutProp != null)
            {
                body.Add(new PropertyField(popoutProp, "Popout"));
            }

            body.Add(BuildDefaultVariantField(entry));

            var variantsProp = entry.FindPropertyRelative("Variants");
            if (variantsProp != null)
            {
                var variantsSection = CardListSection.Build(variantsProp, new CardListSection.Config
                {
                    Title = "Variants",
                    AddButtonText = "+ Add",
                    IdentityFieldName = "VariantId",
                    BuildBody = (variantEntry, variantBody) => BuildVariantBody(variantEntry, variantBody),
                    OnAddSeed = (variantEntry, index) =>
                    {
                        var idProp = variantEntry.FindPropertyRelative("VariantId");
                        if (idProp != null)
                        {
                            idProp.stringValue = $"variant_{index + 1}";
                        }
                        var locProp = variantEntry.FindPropertyRelative("VariantLocalizationKey");
                        if (locProp != null)
                        {
                            locProp.stringValue = string.Empty;
                        }
                    },
                });
                body.Add(variantsSection);
            }
        }

        private static VisualElement BuildDefaultVariantField(SerializedProperty variantSetEntry)
        {
            int setIndex = ExtractIndexFromPath(variantSetEntry.propertyPath);
            var so = variantSetEntry.serializedObject;
            var defaultsProp = so.FindProperty("_dataPartSwitch.DefaultActiveVariants");
            var variantsProp = variantSetEntry.FindPropertyRelative("Variants");

            var dropdown = new DropdownField("Default")
            {
                tooltip = "The variant active when the part is first placed.",
            };
            dropdown.AddToClassList("unity-base-field");
            dropdown.AddToClassList("unity-base-field__aligned");

            void Refresh()
            {
                var choices = new List<string>();
                if (variantsProp != null)
                {
                    for (int i = 0; i < variantsProp.arraySize; i++)
                    {
                        var idProp = variantsProp.GetArrayElementAtIndex(i).FindPropertyRelative("VariantId");
                        string id = idProp?.stringValue ?? string.Empty;
                        if (!string.IsNullOrEmpty(id))
                        {
                            choices.Add(id);
                        }
                    }
                }

                if (choices.Count == 0)
                {
                    dropdown.choices = new List<string> { "(no variants)" };
                    dropdown.SetValueWithoutNotify("(no variants)");
                    dropdown.SetEnabled(false);
                    return;
                }

                dropdown.SetEnabled(true);
                dropdown.choices = choices;

                string current = string.Empty;
                if (defaultsProp != null && setIndex >= 0 && setIndex < defaultsProp.arraySize)
                {
                    current = defaultsProp.GetArrayElementAtIndex(setIndex).stringValue ?? string.Empty;
                }
                if (string.IsNullOrEmpty(current) || !choices.Contains(current))
                {
                    current = choices[0];
                }
                dropdown.SetValueWithoutNotify(current);
            }

            Refresh();

            dropdown.RegisterValueChangedCallback(evt =>
            {
                if (defaultsProp == null || setIndex < 0)
                {
                    return;
                }
                while (defaultsProp.arraySize <= setIndex)
                {
                    defaultsProp.arraySize++;
                    defaultsProp.GetArrayElementAtIndex(defaultsProp.arraySize - 1).stringValue = string.Empty;
                }
                defaultsProp.GetArrayElementAtIndex(setIndex).stringValue = evt.newValue ?? string.Empty;
                defaultsProp.serializedObject.ApplyModifiedProperties();
            });

            if (variantsProp != null)
            {
                dropdown.TrackPropertyValue(variantsProp, _ => Refresh());
            }
            return dropdown;
        }

        private static void BuildVariantBody(SerializedProperty entry, VisualElement body)
        {
            var locKeyProp = entry.FindPropertyRelative("VariantLocalizationKey");
            if (locKeyProp != null)
            {
                body.Add(new PropertyField(locKeyProp, "Localization Key"));
            }

            var techsProp = entry.FindPropertyRelative("VariantTechs");
            if (techsProp != null)
            {
                body.Add(InlineStringListBlock.Build(techsProp, "Required Techs"));
            }

            body.Add(BuildVariantTransformerSection(entry));
        }

        private static VisualElement BuildVariantTransformerSection(SerializedProperty variantEntry)
        {
            var so = variantEntry.serializedObject;
            var module = so.targetObject as Module_PartSwitch;
            if (module == null)
            {
                return new HelpBox("Variant is not on a Module_PartSwitch target.", HelpBoxMessageType.Error);
            }
            CorePartData part = module.gameObject.GetComponent<CorePartData>();

            int setIndex = ExtractIndexFromPath(GetParentArrayPath(variantEntry.propertyPath, "Variants"));
            int variantIndex = ExtractIndexFromPath(variantEntry.propertyPath);
            if (setIndex < 0 || variantIndex < 0)
            {
                return new HelpBox("Could not resolve variant index.", HelpBoxMessageType.Error);
            }

            VariantSet set = module.DataPartSwitch?.VariantSets != null && setIndex < module.DataPartSwitch.VariantSets.Count
                ? module.DataPartSwitch.VariantSets[setIndex]
                : null;
            Variant variant = set?.Variants != null && variantIndex < set.Variants.Count
                ? set.Variants[variantIndex]
                : null;
            if (variant == null)
            {
                return new HelpBox("Could not resolve variant.", HelpBoxMessageType.Error);
            }

            var holder = new VisualElement();

            void MarkDirty()
            {
                Undo.RecordObject(module, "Edit transformer");
                EditorUtility.SetDirty(module);
            }

            SerializedProperty transformersArrayProp = variantEntry?.FindPropertyRelative("Transformers");
            holder.Add(TransformerListBlock.Build(module, part, transformersArrayProp, MarkDirty));
            return holder;
        }

        private static int ExtractIndexFromPath(string propertyPath)
        {
            if (string.IsNullOrEmpty(propertyPath))
            {
                return -1;
            }
            int close = propertyPath.LastIndexOf(']');
            if (close <= 0)
            {
                return -1;
            }
            int open = propertyPath.LastIndexOf('[', close - 1);
            if (open < 0)
            {
                return -1;
            }
            string slice = propertyPath.Substring(open + 1, close - open - 1);
            return int.TryParse(slice, out int idx) ? idx : -1;
        }

        private static string GetParentArrayPath(string propertyPath, string arrayName)
        {
            if (string.IsNullOrEmpty(propertyPath))
            {
                return string.Empty;
            }
            int marker = propertyPath.IndexOf("." + arrayName + ".Array.data[", System.StringComparison.Ordinal);
            if (marker < 0)
            {
                return string.Empty;
            }
            return propertyPath.Substring(0, marker);
        }

        private static Button BuildRemoveButton(Module_PartSwitch module, System.Action rebuild)
        {
            var removeBtn = new Button(() => RemovePartSwitchModule(module, rebuild))
            {
                text = "Remove PartSwitch Module",
                tooltip = "Detaches Module_PartSwitch from this part. Variants are discarded.",
            };
            removeBtn.AddToClassList("variants-tab-remove-module-btn");
            return removeBtn;
        }

        private static void AddPartSwitchModule(CorePartData target, System.Action rebuild)
        {
            Module_PartSwitch module = Undo.AddComponent<Module_PartSwitch>(target.gameObject);
            if (module == null)
            {
                return;
            }
            module.InitForDataModules();
            EditorUtility.SetDirty(module);
            EnsurePartSwitchPamOverride(target);
            rebuild();
        }

        private static void RemovePartSwitchModule(Module_PartSwitch module, System.Action rebuild)
        {
            int variantSetCount = module.DataPartSwitch?.VariantSets?.Count ?? 0;
            if (variantSetCount > 0)
            {
                string suffix = variantSetCount == 1 ? "set" : "sets";
                bool confirm = EditorUtility.DisplayDialog(
                    "Remove PartSwitch?",
                    $"Removing this module will discard {variantSetCount} variant {suffix}. Proceed?",
                    "Remove",
                    "Cancel");
                if (!confirm)
                {
                    return;
                }
            }
            CorePartData target = module.gameObject.GetComponent<CorePartData>();
            Undo.DestroyObjectImmediate(module);
            if (target != null)
            {
                RemovePartSwitchPamOverride(target);
            }
            rebuild();
        }

        private static void EnsurePartSwitchPamOverride(CorePartData target)
        {
            var data = target?.Core?.data;
            if (data?.PAMModuleVisualsOverride == null)
            {
                return;
            }
            foreach (var existing in data.PAMModuleVisualsOverride)
            {
                if (existing != null
                    && string.Equals(existing.PartComponentModuleName, PartSwitchComponentModuleName, System.StringComparison.Ordinal))
                {
                    return;
                }
            }
            Undo.RecordObject(target, "Add PartSwitch PAM override");
            data.PAMModuleVisualsOverride.Add(new PartsManagerCore.SerializedPartModuleDisplayVisuals
            {
                PartComponentModuleName = PartSwitchComponentModuleName,
                ModuleDisplayName = PartSwitchPamDisplayName,
                ShowHeader = true,
                ShowFooter = false,
            });
            EditorUtility.SetDirty(target);
        }

        private static void RemovePartSwitchPamOverride(CorePartData target)
        {
            var data = target?.Core?.data;
            if (data?.PAMModuleVisualsOverride == null)
            {
                return;
            }
            int removed = data.PAMModuleVisualsOverride.RemoveAll(o =>
                o != null
                && string.Equals(o.PartComponentModuleName, PartSwitchComponentModuleName, System.StringComparison.Ordinal));
            if (removed > 0)
            {
                Undo.RecordObject(target, "Remove PartSwitch PAM override");
                EditorUtility.SetDirty(target);
            }
        }
    }
}
