using System;
using KSP.Game.Missions;
using Ksp2UnityTools.Editor.MissionAuthoring.Conditions.Pickers;
using UnityEditor;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.MissionAuthoring.Conditions.Rows
{
    /// <summary>
    /// Card-style editor for an <see cref="EventCondition" />.
    /// </summary>
    /// <remarks>
    /// "Event Condition" header with delete on the right, then a "Message Type" picker row and an optional "Input Filter" labeled row visible only when the picked message has an <see cref="Redux.Missions.MessageInfo.InputFilterHint" />.
    /// </remarks>
    public sealed class EventConditionRow : ConditionRowBase
    {
        private readonly EventCondition _condition;
        private readonly Button _messageButton;
        private readonly VisualElement _inputSlot;
        private MessageCenterMessageCatalogEntry _entry;

        /// <summary>
        /// Constructs the card editor for an EventCondition.
        /// </summary>
        /// <param name="mission">The mission asset that owns the condition, used as the Undo target.</param>
        /// <param name="condition">The EventCondition instance this row edits.</param>
        /// <param name="replace">Callback invoked to swap the condition with another instance or null to delete.</param>
        /// <param name="notifyChanged">Callback invoked when the row mutates its condition.</param>
        /// <param name="moveUp">Callback that moves this row up in its parent's child list, or null when reorder is not available.</param>
        /// <param name="moveDown">Callback that moves this row down in its parent's child list, or null when reorder is not available.</param>
        public EventConditionRow(Mission mission, EventCondition condition, Action<Condition> replace, Action notifyChanged, Action moveUp = null, Action moveDown = null)
            : base(mission, condition, replace, notifyChanged, moveUp, moveDown)
        {
            _condition = condition;
            AddToClassList("condition-row-property-card");

            _entry = MessageCenterMessageCatalog.FindByAqn(_condition?.EventTypeAQN);

            var header = new VisualElement();
            header.AddToClassList("condition-row-card-header");

            var title = new Label("Event Condition");
            title.AddToClassList("condition-row-card-title");
            header.Add(title);

            var spacer = new VisualElement();
            spacer.AddToClassList("condition-row-header-spacer");
            header.Add(spacer);

            BuildHeaderReorderAndWrapButtons(header);
            BuildHeaderDeleteButton(header);

            Add(header);

            var body = new VisualElement();
            body.AddToClassList("condition-row-property-body");
            Add(body);

            var messageRow = new VisualElement();
            messageRow.AddToClassList("picker-row");
            messageRow.AddToClassList("unity-base-field");
            messageRow.AddToClassList("unity-base-field__aligned");

            var messageLabel = new Label("Message Type");
            messageLabel.AddToClassList("unity-base-field__label");
            messageLabel.AddToClassList("unity-property-field__label");
            messageRow.Add(messageLabel);

            _messageButton = new Button(OpenMessagePicker) { text = _entry?.DisplayName ?? "(pick message)" };
            _messageButton.AddToClassList("picker-row__button");
            if (_entry == null) _messageButton.AddToClassList("is-unset");
            if (_entry != null) _messageButton.tooltip = _entry.Description;
            messageRow.Add(_messageButton);
            body.Add(messageRow);

            _inputSlot = new VisualElement();
            _inputSlot.AddToClassList("condition-row-input-slot");
            body.Add(_inputSlot);

            RebuildInputField();
        }

        private void OpenMessagePicker()
        {
            MessageCenterMessagePicker.Open(entry =>
            {
                if (entry == null) return;
                Undo.RecordObject(Mission, "Pick message type");
                _condition.EventTypeAQN = entry.AssemblyQualifiedName;
                EditorUtility.SetDirty(Mission);
                _entry = entry;
                _messageButton.text = entry.DisplayName;
                _messageButton.tooltip = entry.Description;
                _messageButton.RemoveFromClassList("is-unset");
                RebuildInputField();
                NotifyChanged?.Invoke();
            });
        }

        private void RebuildInputField()
        {
            _inputSlot.Clear();
            if (_entry == null || string.IsNullOrEmpty(_entry.InputFilterHint)) return;

            string label = char.ToUpperInvariant(_entry.InputFilterHint[0]) + _entry.InputFilterHint.Substring(1);

            var inputStringField = typeof(EventCondition).GetField("inputString",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            string current = inputStringField?.GetValue(_condition) as string ?? string.Empty;

            var field = new TextField(label)
            {
                value = current,
                isDelayed = true,
            };
            field.AddToClassList("condition-row-field");
            field.AddToClassList("unity-base-field__aligned");
            field.RegisterValueChangedCallback(e =>
            {
                Undo.RecordObject(Mission, "Edit event filter");
                inputStringField?.SetValue(_condition, e.newValue ?? string.Empty);
                EditorUtility.SetDirty(Mission);
                NotifyChanged?.Invoke();
            });
            _inputSlot.Add(field);
        }
    }
}
