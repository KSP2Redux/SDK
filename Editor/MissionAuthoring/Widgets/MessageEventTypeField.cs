using System;
using Ksp2UnityTools.Editor.MissionAuthoring.Conditions;
using Ksp2UnityTools.Editor.MissionAuthoring.Conditions.Pickers;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.MissionAuthoring.Widgets
{
    /// <summary>
    /// Editor widget for <see cref="Type"/>-typed fields that resolve to a
    /// <see cref="KSP.Messages.MessageCenterMessage"/> subclass. Renders a labeled row with
    /// a button that opens <see cref="MessageCenterMessagePicker"/>. The button label shows
    /// the resolved entry's <see cref="MessageCenterMessageCatalogEntry.DisplayName"/> or
    /// a "(pick event)" hint when unset.
    /// </summary>
    public sealed class MessageEventTypeField : VisualElement
    {
        private readonly Button _button;
        private readonly Action<Type> _onValueChanged;

        /// <summary>
        /// Creates a new <see cref="MessageEventTypeField" /> backed by a plain in-memory <see cref="Type" />.
        /// </summary>
        /// <param name="label">The author-facing label shown to the left of the field.</param>
        /// <param name="initialValue">Starting value of the field.</param>
        /// <param name="onValueChanged">Raised whenever the field's value changes via the picker.</param>
        public MessageEventTypeField(string label, Type initialValue, Action<Type> onValueChanged)
        {
            _onValueChanged = onValueChanged;
            AddToClassList("picker-row");
            AddToClassList("unity-base-field");
            AddToClassList("unity-base-field__aligned");

            var labelEl = new Label(label);
            labelEl.AddToClassList("unity-base-field__label");
            labelEl.AddToClassList("unity-property-field__label");
            Add(labelEl);

            _button = new Button(OpenPicker);
            _button.AddToClassList("picker-row__button");
            Add(_button);

            SetValueWithoutNotify(initialValue);
        }

        /// <summary>
        /// Sets the field's value and refreshes the button label without invoking the change callback.
        /// </summary>
        /// <param name="type">The new message-event type, or null to display the unset hint.</param>
        public void SetValueWithoutNotify(Type type)
        {
            if (type == null)
            {
                _button.text = "(pick event)";
                _button.tooltip = string.Empty;
                _button.AddToClassList("is-unset");
                return;
            }
            var entry = MessageCenterMessageCatalog.FindByAqn(type.AssemblyQualifiedName);
            _button.text = entry?.DisplayName ?? type.Name;
            _button.tooltip = entry?.Description ?? string.Empty;
            _button.RemoveFromClassList("is-unset");
        }

        private void OpenPicker()
        {
            MessageCenterMessagePicker.Open(entry =>
            {
                if (entry == null) return;
                SetValueWithoutNotify(entry.MessageType);
                _onValueChanged?.Invoke(entry.MessageType);
            });
        }
    }
}
