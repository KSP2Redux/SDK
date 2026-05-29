using System;
using UnityEditor;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.Widgets
{
    /// <summary>
    /// Shared card chrome for list-of-records UI: outer frame, header row, disclosure button,
    /// body container, and an index-resolving remove handler. Used by
    /// <see cref="CardListSection" /> (value-array) and the variant-transformer card list
    /// (<c>TransformerListBlock</c>, <c>[SerializeReference]</c> array).
    /// </summary>
    /// <remarks>
    /// The header is the caller's responsibility past the disclosure: each consumer mounts its
    /// own identity, chip, and remove slots into <see cref="Slots.Header" />. The remove handler
    /// returned by <see cref="BuildRemoveButton" /> resolves the card's current visual position at
    /// click time, so external mutations of the array (auto-detect, undo) don't cause stale-index
    /// deletes.
    /// </remarks>
    public static class CardShell
    {
        /// <summary>
        /// Mountable regions exposed to callers of <see cref="CardShell.Build" />.
        /// </summary>
        public sealed class Slots
        {
            /// <summary>
            /// The card's header row, after the disclosure button.
            /// </summary>
            public VisualElement Header;
            /// <summary>
            /// The card's body container, toggled by the disclosure button.
            /// </summary>
            public VisualElement Body;
            /// <summary>
            /// The disclosure button that toggles <see cref="Body" />.
            /// </summary>
            public Button Disclosure;
        }

        /// <summary>
        /// Builds the card chrome and returns its mountable slots.
        /// </summary>
        /// <param name="slots">Receives the card's header, body, and disclosure references.</param>
        /// <returns>The outer card element.</returns>
        public static VisualElement Build(out Slots slots)
        {
            var card = new VisualElement();
            card.AddToClassList("data-editor-card");

            var header = new VisualElement();
            header.AddToClassList("data-editor-card-header");
            card.Add(header);

            var disclosure = new Button { text = "▼" };
            disclosure.AddToClassList("data-editor-card-disclosure");
            header.Add(disclosure);

            var body = new VisualElement();
            body.AddToClassList("data-editor-card-body");
            card.Add(body);

            var expanded = true;
            body.style.display = DisplayStyle.Flex;
            disclosure.clicked += () =>
            {
                expanded = !expanded;
                body.style.display = expanded ? DisplayStyle.Flex : DisplayStyle.None;
                disclosure.text = expanded ? "▼" : "▶";
            };

            slots = new Slots { Header = header, Body = body, Disclosure = disclosure };
            return card;
        }

        /// <summary>
        /// Builds the standard remove button. On click it resolves the card's CURRENT index via
        /// <see cref="VisualElement.IndexOf" />, deletes that array element, removes the card from
        /// the container, then re-binds every subsequent sibling whose <c>userData</c> is an
        /// <c>Action&lt;int&gt;</c> rebind callback.
        /// </summary>
        /// <param name="card">The card the button belongs to.</param>
        /// <param name="container">The container that hosts the card and its siblings.</param>
        /// <param name="arrayProp">The backing array SerializedProperty.</param>
        /// <param name="afterRemove">Optional callback invoked after the element is removed and siblings are re-bound.</param>
        /// <returns>The remove button.</returns>
        public static Button BuildRemoveButton(VisualElement card, VisualElement container, SerializedProperty arrayProp, Action afterRemove = null)
        {
            var removeBtn = new Button(() =>
            {
                var idx = container.IndexOf(card);
                if (idx < 0) return;
                var so = arrayProp.serializedObject;
                so.Update();
                if (idx >= arrayProp.arraySize) return;
                arrayProp.DeleteArrayElementAtIndex(idx);
                so.ApplyModifiedProperties();
                container.Remove(card);
                so.Update();
                for (var i = idx; i < container.childCount; i++)
                {
                    if (container.ElementAt(i).userData is Action<int> rebind) rebind(i);
                }
                afterRemove?.Invoke();
            }) { text = "X" };
            removeBtn.AddToClassList("data-editor-card-remove-btn");
            return removeBtn;
        }
    }
}
