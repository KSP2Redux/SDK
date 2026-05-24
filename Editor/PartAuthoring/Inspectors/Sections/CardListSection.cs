using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PartAuthoring.Inspectors.Sections
{
    /// <summary>
    /// Reusable scaffold for the "named record card list" pattern used by Engine modes,
    /// Science experiments, ResourceConverter formulas, and Variants. Renders an array
    /// SerializedProperty as a section with a count-header plus add button, one card per
    /// element, each card with disclosure toggle, identity field, optional summary chip,
    /// delete button, and a caller-supplied body.
    /// </summary>
    /// <remarks>
    /// Mutations are surgical and state-preserving:
    /// - Add appends a single new card without touching existing cards.
    /// - Remove deletes the array element at the card's CURRENT visual position
    ///   (<see cref="VisualElement.IndexOf" />), then re-binds every subsequent sibling
    ///   to its new index so their identity / chip / body fields point at the correct
    ///   serialized data. Foldout expansion state survives because each card's chrome
    ///   (disclosure button, body container) is reused; only the content children of
    ///   the rebindable slots are replaced.
    /// </remarks>
    internal static class CardListSection
    {
        public sealed class Config
        {
            public string Title { get; set; }
            public string AddButtonText { get; set; }
            public string IdentityFieldName { get; set; }
            public Func<SerializedProperty, VisualElement> BuildIdentityField { get; set; }
            public string ChipFieldName { get; set; }
            public Func<SerializedProperty, string> ChipFormatter { get; set; }
            public Action<SerializedProperty, VisualElement> BuildBody { get; set; }
            public Action<SerializedProperty, int> OnAddSeed { get; set; }
        }

        public static VisualElement Build(SerializedProperty arrayProp, Config config)
        {
            var outer = new VisualElement();
            outer.AddToClassList("data-editor-section");

            var headerRow = new VisualElement();
            headerRow.AddToClassList("data-editor-section-header-row");

            var countLabel = new Label();
            countLabel.AddToClassList("data-editor-section-header");
            headerRow.Add(countLabel);

            var addBtn = new Button { text = config.AddButtonText };
            headerRow.Add(addBtn);

            outer.Add(headerRow);

            var container = new VisualElement();
            container.AddToClassList("data-editor-section-list");
            outer.Add(container);

            void UpdateCount()
            {
                countLabel.text = $"{config.Title} ({arrayProp.arraySize})";
            }

            for (var i = 0; i < arrayProp.arraySize; i++)
            {
                container.Add(BuildCard(arrayProp, i, container, config, UpdateCount));
            }
            UpdateCount();

            addBtn.clicked += () =>
            {
                arrayProp.serializedObject.Update();
                var newIndex = arrayProp.arraySize;
                arrayProp.arraySize++;
                arrayProp.serializedObject.ApplyModifiedProperties();
                if (config.OnAddSeed != null)
                {
                    arrayProp.serializedObject.Update();
                    config.OnAddSeed(arrayProp.GetArrayElementAtIndex(newIndex), newIndex);
                    arrayProp.serializedObject.ApplyModifiedProperties();
                }
                arrayProp.serializedObject.Update();
                container.Add(BuildCard(arrayProp, newIndex, container, config, UpdateCount));
                UpdateCount();
            };

            return outer;
        }

        private static VisualElement BuildCard(SerializedProperty arrayProp, int initialIndex, VisualElement container, Config config, Action updateCount)
        {
            var card = new VisualElement();
            card.AddToClassList("data-editor-card");

            var headerRow = new VisualElement();
            headerRow.AddToClassList("data-editor-card-header");

            var disclosure = new Button { text = "▼" };
            disclosure.AddToClassList("data-editor-card-disclosure");
            headerRow.Add(disclosure);

            var idSlot = new VisualElement { style = { flexGrow = 1f, flexShrink = 1f } };
            idSlot.AddToClassList("data-editor-card-name-field");
            headerRow.Add(idSlot);

            var chipSlot = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
            headerRow.Add(chipSlot);

            VisualElement cardRef = card;
            var removeBtn = new Button(() =>
            {
                int currentIndex = container.IndexOf(cardRef);
                if (currentIndex < 0)
                {
                    return;
                }
                arrayProp.serializedObject.Update();
                if (currentIndex >= arrayProp.arraySize)
                {
                    return;
                }
                arrayProp.DeleteArrayElementAtIndex(currentIndex);
                arrayProp.serializedObject.ApplyModifiedProperties();
                container.Remove(cardRef);
                arrayProp.serializedObject.Update();
                // Rebind subsequent cards to their new indexes.
                for (int i = currentIndex; i < container.childCount; i++)
                {
                    if (container.ElementAt(i).userData is Action<int> rebind)
                    {
                        rebind(i);
                    }
                }
                updateCount?.Invoke();
            }) { text = "X" };
            removeBtn.AddToClassList("data-editor-card-remove-btn");
            headerRow.Add(removeBtn);

            card.Add(headerRow);

            var body = new VisualElement();
            body.AddToClassList("data-editor-card-body");
            card.Add(body);

            void Bind(int index)
            {
                if (index < 0 || index >= arrayProp.arraySize)
                {
                    return;
                }
                var entry = arrayProp.GetArrayElementAtIndex(index);

                idSlot.Clear();
                if (!string.IsNullOrEmpty(config.IdentityFieldName))
                {
                    var idProp = entry.FindPropertyRelative(config.IdentityFieldName);
                    if (idProp != null)
                    {
                        VisualElement idField;
                        if (config.BuildIdentityField != null)
                        {
                            idField = config.BuildIdentityField(idProp);
                        }
                        else
                        {
                            var textField = new TextField { value = idProp.stringValue, isDelayed = true };
                            textField.RegisterValueChangedCallback(evt =>
                            {
                                idProp.serializedObject.Update();
                                idProp.stringValue = evt.newValue ?? string.Empty;
                                idProp.serializedObject.ApplyModifiedProperties();
                            });
                            idField = textField;
                        }
                        idField.style.flexGrow = 1f;
                        idSlot.Add(idField);
                    }
                }

                chipSlot.Clear();
                if (!string.IsNullOrEmpty(config.ChipFieldName) && config.ChipFormatter != null)
                {
                    var chipProp = entry.FindPropertyRelative(config.ChipFieldName);
                    if (chipProp != null)
                    {
                        var chip = new Label();
                        chip.AddToClassList("data-editor-card-summary-chip");
                        UpdateChip(chip, chipProp, config.ChipFormatter);
                        chip.TrackPropertyValue(chipProp, p => UpdateChip(chip, p, config.ChipFormatter));
                        chipSlot.Add(chip);
                    }
                }

                body.Clear();
                config.BuildBody?.Invoke(entry, body);
            }

            card.userData = (Action<int>)Bind;
            Bind(initialIndex);

            var expanded = true;
            body.style.display = DisplayStyle.Flex;
            disclosure.clicked += () =>
            {
                expanded = !expanded;
                body.style.display = expanded ? DisplayStyle.Flex : DisplayStyle.None;
                disclosure.text = expanded ? "▼" : "▶";
            };

            return card;
        }

        private static void UpdateChip(Label chip, SerializedProperty prop, Func<SerializedProperty, string> formatter)
        {
            var text = formatter(prop);
            if (string.IsNullOrEmpty(text))
            {
                chip.text = string.Empty;
                chip.style.display = DisplayStyle.None;
            }
            else
            {
                chip.text = text;
                chip.style.display = DisplayStyle.Flex;
            }
        }
    }
}
