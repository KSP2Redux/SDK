using System;
using KSP;
using Ksp2UnityTools.Editor.PartAuthoring.Inspectors.Picker;
using Ksp2UnityTools.Editor.PartAuthoring.Inspectors.Variants;
using Ksp2UnityTools.Editor.Widgets;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using VSwift.Modules.Behaviours;
using VSwift.Modules.Transformers;

namespace Ksp2UnityTools.Editor.PartAuthoring.Inspectors.Tabs.Variants
{
    /// <summary>
    /// List scaffold for <see cref="Variant.Transformers" /> (polymorphic <c>[SerializeReference]</c> list).
    /// </summary>
    /// <remarks>
    /// Mirrors the chrome of <see cref="Ksp2UnityTools.Editor.PartAuthoring.Inspectors.Sections.CardListSection" /> but uses the SerializedProperty API for both reads and writes because <c>[SerializeReference]</c> arrays require an explicit <c>managedReferenceValue</c> assignment after <c>arraySize++</c> to bind a concrete type into the new slot. Mutations are surgical and state-preserving. Add appends one card without disturbing existing ones. Remove deletes at the card's current visual position and rebinds every subsequent card to its new index so their body content reflects the correct transformer instance and serialized property.
    /// </remarks>
    internal static class TransformerListBlock
    {
        /// <summary>
        /// Builds the transformer-list block for a single variant.
        /// </summary>
        /// <param name="module">The owning <see cref="Module_PartSwitch" />.</param>
        /// <param name="part">The owning part used to scope transformer editors.</param>
        /// <param name="transformersArrayProp">The <c>[SerializeReference]</c> array property to render.</param>
        /// <param name="markDirty">Callback invoked after mutations so the editor records dirty state.</param>
        /// <returns>A VisualElement containing the header row, empty hint, and per-transformer cards.</returns>
        public static VisualElement Build(
            Module_PartSwitch module,
            CorePartData part,
            SerializedProperty transformersArrayProp,
            Action markDirty)
        {
            var outer = new VisualElement();
            outer.AddToClassList("data-editor-section");

            if (transformersArrayProp == null)
            {
                outer.Add(new HelpBox("Transformers array property not provided.", HelpBoxMessageType.Error));
                return outer;
            }

            transformersArrayProp.serializedObject.Update();

            var headerRow = new VisualElement();
            headerRow.AddToClassList("data-editor-section-header-row");

            var countLabel = new Label();
            countLabel.AddToClassList("data-editor-section-header");
            headerRow.Add(countLabel);

            var emptyHint = new Label("(no transformers - add one to apply effects when this variant is active)")
            {
                style = { unityFontStyleAndWeight = FontStyle.Italic },
            };
            emptyHint.AddToClassList("transformer-list-empty");

            var container = new VisualElement();
            container.AddToClassList("data-editor-section-list");

            var context = new TransformerEditorContext
            {
                Part = part,
                Module = module,
                MarkDirty = markDirty,
            };

            void UpdateCountAndEmpty()
            {
                transformersArrayProp.serializedObject.Update();
                var n = transformersArrayProp.arraySize;
                countLabel.text = $"Transformers ({n})";
                emptyHint.style.display = n == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            }

            var addBtn = new Button(() =>
            {
                AddTransformerPicker.Open(type =>
                {
                    if (type == null)
                    {
                        return;
                    }
                    try
                    {
                        var instance = Activator.CreateInstance(type) as ITransformer;
                        if (instance == null)
                        {
                            return;
                        }
                        var so = transformersArrayProp.serializedObject;
                        so.Update();
                        var newIndex = transformersArrayProp.arraySize;
                        transformersArrayProp.arraySize++;
                        so.ApplyModifiedProperties();
                        so.Update();
                        var newEntry = transformersArrayProp.GetArrayElementAtIndex(newIndex);
                        newEntry.managedReferenceValue = instance;
                        so.ApplyModifiedProperties();
                        markDirty?.Invoke();
                        container.Add(BuildTransformerCard(transformersArrayProp, newIndex, container, context, UpdateCountAndEmpty));
                        UpdateCountAndEmpty();
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[AddTransformerPicker] Failed to instantiate {type}: {e.Message}");
                    }
                });
            })
            {
                text = "+ Add",
                tooltip = "Open the transformer picker.",
            };
            headerRow.Add(addBtn);
            outer.Add(headerRow);

            outer.Add(emptyHint);
            outer.Add(container);

            for (var i = 0; i < transformersArrayProp.arraySize; i++)
            {
                container.Add(BuildTransformerCard(transformersArrayProp, i, container, context, UpdateCountAndEmpty));
            }

            UpdateCountAndEmpty();
            return outer;
        }

        private static VisualElement BuildTransformerCard(
            SerializedProperty transformersArrayProp,
            int initialIndex,
            VisualElement container,
            TransformerEditorContext context,
            Action updateCountAndEmpty)
        {
            var card = CardShell.Build(out var slots);

            var nameLabel = new Label();
            nameLabel.AddToClassList("data-editor-card-name-field");
            slots.Header.Add(nameLabel);

            slots.Header.Add(CardShell.BuildRemoveButton(card, container, transformersArrayProp, () =>
            {
                context?.MarkDirty?.Invoke();
                updateCountAndEmpty?.Invoke();
            }));

            void Bind(int index)
            {
                transformersArrayProp.serializedObject.Update();
                if (index < 0 || index >= transformersArrayProp.arraySize) return;
                var entryProp = transformersArrayProp.GetArrayElementAtIndex(index);
                var transformer = entryProp?.managedReferenceValue as ITransformer;
                nameLabel.text = transformer?.GetType().Name ?? "(null transformer)";

                slots.Body.Clear();
                if (transformer != null)
                {
                    var t = transformer.GetType();
                    var content = TransformerEditorRegistry.TryCreate(t, out var customEditor)
                        ? customEditor.Build(transformer, entryProp, context)
                        : ReflectionTransformerEditor.Build(entryProp, context);
                    if (content != null) slots.Body.Add(content);
                }
                else
                {
                    slots.Body.Add(new HelpBox("Transformer reference is null.", HelpBoxMessageType.Error));
                }
            }

            card.userData = (Action<int>)Bind;
            Bind(initialIndex);

            return card;
        }
    }
}
