using System;
using System.Collections.Generic;
using KSP;
using KSP.Sim.ResourceSystem;
using Ksp2UnityTools.Editor.PartAuthoring.Inspectors.DataEditors;
using UnityEditor;
using UnityEngine.UIElements;
using VSwift.Modules.Transformers;

namespace Ksp2UnityTools.Editor.PartAuthoring.Inspectors.Variants.Editors
{
    /// <summary>
    /// Custom editor for <see cref="ResourceContainerRemover" />. Each row in the Containers list renders as a <see cref="DropdownField" /> constrained to the part's existing resource-container names, so the author picks a container to remove rather than typing its name.
    /// </summary>
    /// <remarks>
    /// Choices are read from <c>context.Part.Data.resourceContainers</c> at Build time. If the part's resource set changes while the Variants tab is open, the choices in this editor's dropdowns don't refresh until the tab is rebuilt. Stored entries whose name no longer matches any current container stay displayed as-is so authored state isn't silently lost.
    /// </remarks>
    [TransformerEditor(typeof(ResourceContainerRemover))]
    public sealed class ResourceContainerRemoverEditor : ITransformerEditor
    {
        /// <inheritdoc />
        public VisualElement Build(ITransformer transformer, SerializedProperty transformerProp, TransformerEditorContext context)
        {
            var outer = new VisualElement();

            SerializedProperty containersProp = transformerProp?.FindPropertyRelative("Containers");
            if (containersProp == null)
            {
                outer.Add(new HelpBox("Containers array not found on this transformer.", HelpBoxMessageType.Error));
                return outer;
            }

            List<string> choices = GetPartContainerNames(context?.Part);

            outer.Add(InlineListBlock.Build(
                containersProp,
                titleFormat: "Containers ({0})",
                addButtonText: "+ Add",
                emptyHint: "(no containers selected)",
                rowBuilder: (entry, index, onDelete) => BuildRow(entry, onDelete, choices)));

            return outer;
        }

        private static VisualElement BuildRow(SerializedProperty entry, Action onDelete, List<string> choices)
        {
            var row = new VisualElement();
            row.AddToClassList("data-editor-inline-row");

            var dropdown = new DropdownField();
            dropdown.AddToClassList("data-editor-inline-row__grow");

            if (choices.Count == 0)
            {
                dropdown.choices = new List<string> { "(no containers on part)" };
                dropdown.SetValueWithoutNotify("(no containers on part)");
                dropdown.SetEnabled(false);
            }
            else
            {
                dropdown.choices = choices;
                string stored = entry.stringValue ?? string.Empty;
                dropdown.SetValueWithoutNotify(stored);
                dropdown.RegisterValueChangedCallback(evt =>
                {
                    entry.serializedObject.Update();
                    entry.stringValue = evt.newValue ?? string.Empty;
                    entry.serializedObject.ApplyModifiedProperties();
                });
            }

            row.Add(dropdown);

            var removeBtn = new Button(onDelete) { text = "X" };
            removeBtn.AddToClassList("data-editor-card-remove-btn");
            row.Add(removeBtn);

            return row;
        }

        private static List<string> GetPartContainerNames(CorePartData part)
        {
            var result = new List<string>();
            var containers = part?.Data?.resourceContainers;
            if (containers == null)
            {
                return result;
            }
            foreach (ContainedResourceDefinition c in containers)
            {
                if (c == null || string.IsNullOrEmpty(c.name))
                {
                    continue;
                }
                if (!result.Contains(c.name))
                {
                    result.Add(c.name);
                }
            }
            return result;
        }
    }
}
