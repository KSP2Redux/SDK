using System;
using KSP;
using Ksp2UnityTools.Editor.PartAuthoring.Inspectors.Picker;
using Ksp2UnityTools.Editor.PartAuthoring.Inspectors.Variants;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using VSwift.Modules.Behaviours;
using VSwift.Modules.Transformers;

namespace Ksp2UnityTools.Editor.PartAuthoring.Inspectors.Tabs.Variants
{
    /// <summary>
    /// List scaffold for <see cref="Variant.Transformers" /> (polymorphic <c>[SerializeReference]</c> list). Mirrors the chrome of <see cref="Ksp2UnityTools.Editor.PartAuthoring.Inspectors.Sections.CardListSection" /> but uses the SerializedProperty API for both reads and writes because <c>[SerializeReference]</c> arrays require an explicit <c>managedReferenceValue</c> assignment after <c>arraySize++</c> to bind a concrete type into the new slot.
    /// </summary>
    /// <remarks>
    /// Mutations are surgical and state-preserving. Add appends one card without disturbing existing ones. Remove deletes at the card's CURRENT visual position and rebinds every subsequent card to its new index so their body content reflects the correct transformer instance and serialized property.
    /// </remarks>
    internal static class TransformerListBlock
    {
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
                style = { unityFontStyleAndWeight = UnityEngine.FontStyle.Italic },
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
                int n = transformersArrayProp.arraySize;
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
                        int newIndex = transformersArrayProp.arraySize;
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
                        UnityEngine.Debug.LogError($"[AddTransformerPicker] Failed to instantiate {type}: {e.Message}");
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

            for (int i = 0; i < transformersArrayProp.arraySize; i++)
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
            var card = new VisualElement();
            card.AddToClassList("data-editor-card");

            var headerRow = new VisualElement();
            headerRow.AddToClassList("data-editor-card-header");

            var disclosure = new Button { text = "▶" };
            disclosure.AddToClassList("data-editor-card-disclosure");
            headerRow.Add(disclosure);

            var nameLabel = new Label();
            nameLabel.AddToClassList("data-editor-card-name-field");
            headerRow.Add(nameLabel);

            VisualElement cardRef = card;
            var removeBtn = new Button(() =>
            {
                int currentIndex = container.IndexOf(cardRef);
                if (currentIndex < 0)
                {
                    return;
                }
                var so = transformersArrayProp.serializedObject;
                so.Update();
                if (currentIndex >= transformersArrayProp.arraySize)
                {
                    return;
                }
                transformersArrayProp.DeleteArrayElementAtIndex(currentIndex);
                so.ApplyModifiedProperties();
                context?.MarkDirty?.Invoke();
                container.Remove(cardRef);
                for (int i = currentIndex; i < container.childCount; i++)
                {
                    if (container.ElementAt(i).userData is Action<int> rebind)
                    {
                        rebind(i);
                    }
                }
                updateCountAndEmpty?.Invoke();
            }) { text = "X" };
            removeBtn.AddToClassList("data-editor-card-remove-btn");
            headerRow.Add(removeBtn);

            card.Add(headerRow);

            var body = new VisualElement();
            body.AddToClassList("data-editor-card-body");
            card.Add(body);

            void Bind(int index)
            {
                transformersArrayProp.serializedObject.Update();
                if (index < 0 || index >= transformersArrayProp.arraySize)
                {
                    return;
                }
                SerializedProperty entryProp = transformersArrayProp.GetArrayElementAtIndex(index);
                ITransformer transformer = entryProp?.managedReferenceValue as ITransformer;
                nameLabel.text = transformer?.GetType().Name ?? "(null transformer)";

                body.Clear();
                if (transformer != null)
                {
                    Type t = transformer.GetType();
                    VisualElement content = TransformerEditorRegistry.TryCreate(t, out var customEditor)
                        ? customEditor.Build(transformer, entryProp, context)
                        : ReflectionTransformerEditor.Build(entryProp, context);
                    if (content != null)
                    {
                        body.Add(content);
                    }
                }
                else
                {
                    body.Add(new HelpBox("Transformer reference is null.", HelpBoxMessageType.Error));
                }
            }

            card.userData = (Action<int>)Bind;
            Bind(initialIndex);

            bool expanded = false;
            body.style.display = DisplayStyle.None;
            disclosure.clicked += () =>
            {
                expanded = !expanded;
                body.style.display = expanded ? DisplayStyle.Flex : DisplayStyle.None;
                disclosure.text = expanded ? "▼" : "▶";
            };

            return card;
        }
    }
}
