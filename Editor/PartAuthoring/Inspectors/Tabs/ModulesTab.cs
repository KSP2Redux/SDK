using System;
using System.Reflection;
using KSP;
using KSP.Sim.Definitions;
using Ksp2UnityTools.Editor.PartAuthoring.Inspectors;
using Ksp2UnityTools.Editor.PartAuthoring.Inspectors.Picker;
using Redux.Modules.Attributes;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PartAuthoring.Inspectors.Tabs
{
    /// <summary>
    /// Modules tab content that lists every <see cref="PartBehaviourModule" /> on the part's GameObject as a foldable card with a per-card body that renders the module's serialized fields.
    /// </summary>
    /// <remarks>
    /// Each module's body uses <see cref="ReflectionModuleEditor" /> for a baseline field-by-field render. An "Add Module" picker is wired to insert new module Components, and a per-card remove button detaches them.
    /// </remarks>
    internal static class ModulesTab
    {
        private const string USS_PATH = "/Assets/Windows/PartAuthoring/Inspectors/Tabs/ModulesTab.uss";
        private const string MODULE_TYPE_PREFIX = "Module_";

        /// <summary>
        /// Builds the Modules tab content for the given part.
        /// </summary>
        /// <param name="target">The CorePartData whose GameObject hosts the modules.</param>
        /// <returns>A root VisualElement containing the header row and the module card list.</returns>
        public static VisualElement Build(CorePartData target)
        {
            var root = new VisualElement();
            root.AddToClassList("modules-tab");

            Ksp2UnityToolsStyles.Apply(root, USS_PATH);

            var header = new VisualElement();
            header.AddToClassList("modules-tab-header");

            var countLabel = new Label();
            countLabel.AddToClassList("modules-tab-count");
            header.Add(countLabel);

            var addButton = new Button
            {
                text = "+ Add Module",
                tooltip = "Open the module picker to add a new module Component to this part.",
            };
            addButton.AddToClassList("modules-tab-add-btn");
            header.Add(addButton);

            root.Add(header);

            var list = new VisualElement();
            list.AddToClassList("modules-tab-list");
            root.Add(list);

            void RebuildList()
            {
                list.Clear();
                var modules = target.gameObject.GetComponents<PartBehaviourModule>();
                countLabel.text = $"Modules ({modules.Length})";
                var corePartDataSo = new SerializedObject(target);
                foreach (var module in modules)
                {
                    list.Add(BuildModuleCard(module, corePartDataSo, RebuildList));
                }
            }

            addButton.clicked += () => AddModulePicker.Open(moduleType => AddModule(target, moduleType, RebuildList));

            RebuildList();
            return root;
        }

        private static void AddModule(CorePartData target, Type moduleType, Action onListChanged)
        {
            if (target == null || moduleType == null) return;
            var component = Undo.AddComponent(target.gameObject, moduleType);
            if (component == null) return;
            EditorUtility.SetDirty(target.gameObject);
            component.hideFlags |= HideFlags.HideInInspector;
            EditorModuleDataHydrator.Hydrate(component);
            onListChanged();
        }

        private static VisualElement BuildModuleCard(PartBehaviourModule module, SerializedObject corePartDataSo, Action onListChanged)
        {
            var card = new VisualElement();
            card.AddToClassList("module-card");

            var headerRow = new VisualElement();
            headerRow.AddToClassList("module-card-header");

            var disclosure = new Button { text = "▼" };
            disclosure.AddToClassList("module-card-disclosure");
            disclosure.tooltip = "Collapse or expand this module's editor.";
            headerRow.Add(disclosure);

            var nameLabel = new Label(GetDisplayName(module));
            nameLabel.AddToClassList("module-card-name");
            var description = module.GetType().GetCustomAttribute<ModuleDescriptionAttribute>()?.Description;
            if (!string.IsNullOrEmpty(description))
            {
                nameLabel.tooltip = description;
            }
            headerRow.Add(nameLabel);

            var removeBtn = new Button(() => RemoveModule(module, onListChanged))
            {
                text = "X",
                tooltip = "Remove this module from the part.",
            };
            removeBtn.AddToClassList("module-card-remove-btn");
            headerRow.Add(removeBtn);

            card.Add(headerRow);

            var body = new VisualElement();
            body.AddToClassList("module-card-body");

            body.Add(ReflectionModuleEditor.Build(module, corePartDataSo));
            card.Add(body);

            var expanded = true;
            disclosure.clicked += () =>
            {
                expanded = !expanded;
                body.style.display = expanded ? DisplayStyle.Flex : DisplayStyle.None;
                disclosure.text = expanded ? "▼" : "▶";
            };

            return card;
        }

        private static string GetDisplayName(PartBehaviourModule module)
        {
            var typeName = module.GetType().Name;
            return typeName.StartsWith(MODULE_TYPE_PREFIX) ? typeName.Substring(MODULE_TYPE_PREFIX.Length) : typeName;
        }

        private static void RemoveModule(PartBehaviourModule module, Action onListChanged)
        {
            var go = module.gameObject;
            Undo.DestroyObjectImmediate(module);
            EditorUtility.SetDirty(go);
            onListChanged();
        }
    }
}
