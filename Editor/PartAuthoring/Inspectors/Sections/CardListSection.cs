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
        /// <summary>
        /// Caller-supplied configuration for one card-list section.
        /// </summary>
        public sealed class Config
        {
            /// <summary>
            /// Gets or sets the section title rendered in the count-header.
            /// </summary>
            public string Title { get; set; }
            /// <summary>
            /// Gets or sets the label shown on the add button.
            /// </summary>
            public string AddButtonText { get; set; }
            /// <summary>
            /// Gets or sets the relative property name used as the identity field on each card.
            /// </summary>
            public string IdentityFieldName { get; set; }
            /// <summary>
            /// Gets or sets an optional identity-field builder. When null, the section renders a delayed TextField bound to the identity property.
            /// </summary>
            public Func<SerializedProperty, VisualElement> BuildIdentityField { get; set; }
            /// <summary>
            /// Gets or sets the optional relative property name backing the summary chip in each card header.
            /// </summary>
            public string ChipFieldName { get; set; }
            /// <summary>
            /// Gets or sets the optional formatter that turns the chip property into a display string. Return null or empty to hide the chip.
            /// </summary>
            public Func<SerializedProperty, string> ChipFormatter { get; set; }
            /// <summary>
            /// Gets or sets the body builder invoked once per card with the array element's SerializedProperty and the card's body container.
            /// </summary>
            public Action<SerializedProperty, VisualElement> BuildBody { get; set; }
            /// <summary>
            /// Gets or sets the optional seed callback invoked after the add button appends a new element. Receives the new entry's SerializedProperty and its index.
            /// </summary>
            public Action<SerializedProperty, int> OnAddSeed { get; set; }
        }

        /// <summary>
        /// Builds the card-list section for the given array SerializedProperty.
        /// </summary>
        /// <param name="arrayProp">The array SerializedProperty to render.</param>
        /// <param name="config">Section configuration.</param>
        /// <returns>The built section element.</returns>
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
            var card = CardShell.Build(out var slots);

            var idSlot = new VisualElement();
            idSlot.AddToClassList("data-editor-card-name-field");
            slots.Header.Add(idSlot);

            var chipSlot = new VisualElement();
            chipSlot.AddToClassList("data-editor-card-chip-slot");
            slots.Header.Add(chipSlot);

            slots.Header.Add(CardShell.BuildRemoveButton(card, container, arrayProp, updateCount));

            void Bind(int index)
            {
                if (index < 0 || index >= arrayProp.arraySize) return;
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

                slots.Body.Clear();
                config.BuildBody?.Invoke(entry, slots.Body);
            }

            card.userData = (Action<int>)Bind;
            Bind(initialIndex);

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
