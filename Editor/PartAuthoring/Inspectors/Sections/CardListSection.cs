using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PartAuthoring.Inspectors.Sections
{
    /// <summary>
    /// Reusable scaffold for the "named record card list" pattern used by Engine modes,
    /// Science experiments, and ResourceConverter formulas. Renders an array SerializedProperty
    /// as a section with a count-header plus add button, one card per element, each card with
    /// disclosure toggle, identity field, optional summary chip, delete button, and a
    /// caller-supplied body.
    /// </summary>
    /// <remarks>
    /// Manages its own refresh state via a closure over the section container, so callers do
    /// not need to track containers, count labels, or invalidation callbacks themselves.
    /// </remarks>
    internal static class CardListSection
    {
        /// <summary>
        /// Per-card configuration. Most fields are optional; required fields are
        /// <see cref="Title" />, <see cref="AddButtonText" />, and <see cref="BuildBody" />.
        /// </summary>
        public sealed class Config
        {
            /// <summary>Section header text. The element count is appended in parentheses.</summary>
            public string Title { get; set; }

            /// <summary>Text on the section's add button.</summary>
            public string AddButtonText { get; set; }

            /// <summary>
            /// Name of the array-element field used as the card's primary identity. The field
            /// is shown in the card header. Leave null to omit an identity field.
            /// </summary>
            public string IdentityFieldName { get; set; }

            /// <summary>
            /// Optional custom widget factory for the identity field. Defaults to a plain
            /// delayed TextField when null.
            /// </summary>
            public Func<SerializedProperty, VisualElement> BuildIdentityField { get; set; }

            /// <summary>
            /// Name of the array-element field whose value drives the right-aligned summary chip.
            /// Leave null to omit the chip.
            /// </summary>
            public string ChipFieldName { get; set; }

            /// <summary>
            /// Formats the chip text from the chip property. Return null or empty to hide the
            /// chip for that card. Re-evaluated whenever the chip property changes.
            /// </summary>
            public Func<SerializedProperty, string> ChipFormatter { get; set; }

            /// <summary>
            /// Builds the card body. Receives the array-element SerializedProperty and the
            /// (empty) body container; populate the body with the per-card field rows.
            /// </summary>
            public Action<SerializedProperty, VisualElement> BuildBody { get; set; }

            /// <summary>
            /// Optional pre-fill hook invoked on a newly-added array element. Receives the new
            /// element's SerializedProperty and its index.
            /// </summary>
            public Action<SerializedProperty, int> OnAddSeed { get; set; }
        }

        /// <summary>
        /// Builds a card-list section bound to <paramref name="arrayProp" />.
        /// </summary>
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

            void Refresh()
            {
                container.Clear();
                countLabel.text = $"{config.Title} ({arrayProp.arraySize})";
                for (var i = 0; i < arrayProp.arraySize; i++)
                {
                    container.Add(BuildCard(arrayProp, i, config, Refresh));
                }
            }

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
                Refresh();
            };

            Refresh();
            return outer;
        }

        private static VisualElement BuildCard(SerializedProperty arrayProp, int index, Config config, Action onRefresh)
        {
            var entry = arrayProp.GetArrayElementAtIndex(index);

            var card = new VisualElement();
            card.AddToClassList("data-editor-card");

            var headerRow = new VisualElement();
            headerRow.AddToClassList("data-editor-card-header");

            var disclosure = new Button { text = "▶" };
            disclosure.AddToClassList("data-editor-card-disclosure");
            headerRow.Add(disclosure);

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
                    idField.AddToClassList("data-editor-card-name-field");
                    headerRow.Add(idField);
                }
            }

            if (!string.IsNullOrEmpty(config.ChipFieldName) && config.ChipFormatter != null)
            {
                var chipProp = entry.FindPropertyRelative(config.ChipFieldName);
                if (chipProp != null)
                {
                    var chip = new Label();
                    chip.AddToClassList("data-editor-card-summary-chip");
                    UpdateChip(chip, chipProp, config.ChipFormatter);
                    chip.TrackPropertyValue(chipProp, p => UpdateChip(chip, p, config.ChipFormatter));
                    headerRow.Add(chip);
                }
            }

            var capturedIndex = index;
            var removeBtn = new Button(() =>
            {
                arrayProp.serializedObject.Update();
                arrayProp.DeleteArrayElementAtIndex(capturedIndex);
                arrayProp.serializedObject.ApplyModifiedProperties();
                onRefresh();
            }) { text = "X" };
            removeBtn.AddToClassList("data-editor-card-remove-btn");
            headerRow.Add(removeBtn);

            card.Add(headerRow);

            var body = new VisualElement();
            body.AddToClassList("data-editor-card-body");
            config.BuildBody?.Invoke(entry, body);
            card.Add(body);

            var expanded = false;
            body.style.display = DisplayStyle.None;
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
