using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.Widgets
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
    ///   (disclosure button, body container) is reused. Only the content children of
    ///   the rebindable slots are replaced.
    /// </remarks>
    public static class CardListSection
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
                card.Bind(arrayProp.serializedObject);
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

        // ---------- Callback-based variant for IList<T> sources ----------

        /// <summary>
        /// Configuration for <see cref="BuildFromList{T}"/>. Caller supplies the per-entry
        /// card builder and the add-click action. Per-entry chrome (delete/reorder buttons)
        /// is the responsibility of the card produced by <see cref="BuildCard"/>.
        /// </summary>
        public sealed class ListConfig<T> where T : class
        {
            public string Title { get; set; }
            public string AddButtonText { get; set; } = "+";
            public string AddButtonTooltip { get; set; }
            public string EmptyHintText { get; set; } = "(none)";
            public Action OnAddClicked { get; set; }
            public Func<T, int, VisualElement> BuildCard { get; set; }
        }

        /// <summary>
        /// Handle returned from <see cref="BuildFromList{T}"/>. <see cref="Root"/> is the
        /// section's mountable VisualElement. <see cref="Rebuild"/> re-renders the list
        /// from the current contents of the resolver-backed source.
        /// </summary>
        public sealed class ListHandle
        {
            public VisualElement Root { get; internal set; }
            public Action Rebuild { get; internal set; }
        }

        /// <summary>
        /// Builds a card-list section over an in-memory <see cref="IList{T}"/> source. The
        /// list is fetched fresh from <paramref name="listResolver"/> on every rebuild so
        /// undo cascades that replace the backing list reference still resolve correctly.
        /// Per-entry visuals are produced by <see cref="ListConfig{T}.BuildCard"/>. The
        /// section owns count header, add button, and empty hint chrome only.
        /// </summary>
        public static ListHandle BuildFromList<T>(Func<IList<T>> listResolver, ListConfig<T> config) where T : class
        {
            return BuildFromObjectList(
                () => listResolver?.Invoke() as IList,
                new ObjectListConfig
                {
                    Title = config.Title,
                    AddButtonText = config.AddButtonText,
                    AddButtonTooltip = config.AddButtonTooltip,
                    EmptyHintText = config.EmptyHintText,
                    OnAddClicked = config.OnAddClicked,
                    BuildCard = (obj, idx) => config.BuildCard?.Invoke((T)obj, idx),
                });
        }

        /// <summary>
        /// Non-generic <see cref="ListConfig{T}"/> for the <see cref="BuildFromObjectList"/>
        /// overload. Use when the element type is dynamic and not known at compile time.
        /// </summary>
        public sealed class ObjectListConfig
        {
            public string Title { get; set; }
            public string AddButtonText { get; set; } = "+";
            public string AddButtonTooltip { get; set; }
            public string EmptyHintText { get; set; } = "(none)";
            public Action OnAddClicked { get; set; }
            public Func<object, int, VisualElement> BuildCard { get; set; }
        }

        /// <summary>
        /// Non-generic variant of <see cref="BuildFromList{T}"/>. Used by reflection-based
        /// callers that work against <see cref="IList"/> with a dynamic element type.
        /// </summary>
        public static ListHandle BuildFromObjectList(Func<IList> listResolver, ObjectListConfig config)
        {
            var handle = new ListHandle();
            var outer = new VisualElement();
            outer.AddToClassList("data-editor-section");

            var headerRow = new VisualElement();
            headerRow.AddToClassList("data-editor-section-header-row");

            var countLabel = new Label();
            countLabel.AddToClassList("data-editor-section-header");
            headerRow.Add(countLabel);

            var addBtn = new Button(() => config.OnAddClicked?.Invoke()) { text = config.AddButtonText };
            if (!string.IsNullOrEmpty(config.AddButtonTooltip)) addBtn.tooltip = config.AddButtonTooltip;
            headerRow.Add(addBtn);

            outer.Add(headerRow);

            var container = new VisualElement();
            container.AddToClassList("data-editor-section-list");
            outer.Add(container);

            var emptyHint = new Label(config.EmptyHintText ?? "(none)");
            emptyHint.AddToClassList("data-editor-section-empty");

            void Rebuild()
            {
                container.Clear();
                var list = listResolver?.Invoke();
                int count = list?.Count ?? 0;
                countLabel.text = $"{config.Title} ({count})";
                if (count == 0)
                {
                    container.Add(emptyHint);
                    return;
                }
                for (int i = 0; i < count; i++)
                {
                    var card = config.BuildCard?.Invoke(list[i], i);
                    if (card != null) container.Add(card);
                }
            }

            handle.Root = outer;
            handle.Rebuild = Rebuild;
            Rebuild();
            return handle;
        }
    }
}
