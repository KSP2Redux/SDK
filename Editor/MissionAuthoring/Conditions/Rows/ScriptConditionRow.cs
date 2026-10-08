using System;
using System.Collections.Generic;
using KSP.Game.Missions;
using KSP.Utilities.Scripting;
using Ksp2UnityTools.Editor.MissionAuthoring.Conditions.Pickers;
using UnityEditor;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.MissionAuthoring.Conditions.Rows
{
    /// <summary>
    /// Card-style editor for a <see cref="ScriptCondition" />.
    /// </summary>
    /// <remarks>
    /// The runtime path is stubbed (MessageHandler logs "not implemented" and forces true) so the card carries an amber warning chip at the top. Below the chip sits a Message Type picker matching EventConditionRow, then three flat fields for the EvaluationScript reference.
    /// </remarks>
    public sealed class ScriptConditionRow : ConditionRowBase
    {
        private static readonly ScriptExecutionContext[] _contextChoices = new[]
        {
            ScriptExecutionContext.Main,
            ScriptExecutionContext.Simulation,
            ScriptExecutionContext.Mission,
            ScriptExecutionContext.Mod,
        };

        private readonly ScriptCondition _condition;
        private readonly Button _messageButton;
        private MessageCenterMessageCatalogEntry _entry;

        /// <summary>
        /// Constructs the card editor for a ScriptCondition.
        /// </summary>
        /// <param name="mission">The mission asset that owns the condition, used as the Undo target.</param>
        /// <param name="condition">The ScriptCondition instance this row edits.</param>
        /// <param name="replace">Callback invoked to swap the condition with another instance or null to delete.</param>
        /// <param name="notifyChanged">Callback invoked when the row mutates its condition.</param>
        /// <param name="moveUp">Callback that moves this row up in its parent's child list, or null when reorder is not available.</param>
        /// <param name="moveDown">Callback that moves this row down in its parent's child list, or null when reorder is not available.</param>
        public ScriptConditionRow(Mission mission, ScriptCondition condition, Action<Condition> replace, Action notifyChanged, Action moveUp = null, Action moveDown = null)
            : base(mission, condition, replace, notifyChanged, moveUp, moveDown)
        {
            _condition = condition;
            AddToClassList("condition-row-property-card");

            _entry = MessageCenterMessageCatalog.FindByAqn(_condition?.triggerEventTypeAQN);

            var header = new VisualElement();
            header.AddToClassList("condition-row-card-header");

            var title = new Label("Script Condition");
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

            var warning = new Label("Runtime not implemented - this condition will not evaluate correctly in-game.");
            warning.AddToClassList("condition-row-script-warning");
            body.Add(warning);

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

            BuildScriptFields(body);
        }

        private void OpenMessagePicker()
        {
            MessageCenterMessagePicker.Open(entry =>
            {
                if (entry == null) return;
                Undo.RecordObject(Mission, "Pick script trigger event");
                _condition.triggerEventTypeAQN = entry.AssemblyQualifiedName;
                EditorUtility.SetDirty(Mission);
                _entry = entry;
                _messageButton.text = entry.DisplayName;
                _messageButton.tooltip = entry.Description;
                _messageButton.RemoveFromClassList("is-unset");
                NotifyChanged?.Invoke();
            });
        }

        private void BuildScriptFields(VisualElement body)
        {
            var labels = new List<string>(_contextChoices.Length);
            foreach (var ctx in _contextChoices) labels.Add(ctx.ToString());

            var currentContext = _condition.EvaluationScript?.TargetContext ?? ScriptExecutionContext.Main;
            string currentLabel = currentContext.ToString();
            if (!labels.Contains(currentLabel)) currentLabel = ScriptExecutionContext.Main.ToString();

            var contextField = new DropdownField("Target Context", labels, currentLabel);
            contextField.AddToClassList("condition-row-field");
            contextField.AddToClassList("unity-base-field__aligned");
            contextField.RegisterValueChangedCallback(e =>
            {
                if (string.IsNullOrEmpty(e.newValue)) return;
                if (!Enum.TryParse<ScriptExecutionContext>(e.newValue, out var parsed)) return;
                Undo.RecordObject(Mission, "Edit script target context");
                EnsureEvaluationScript();
                _condition.EvaluationScript.TargetContext = parsed;
                EditorUtility.SetDirty(Mission);
                NotifyChanged?.Invoke();
            });
            body.Add(contextField);

            var fileField = new TextField("Script File Asset")
            {
                value = _condition.EvaluationScript?.ScriptFileAsset ?? string.Empty,
                isDelayed = true,
            };
            fileField.AddToClassList("condition-row-field");
            fileField.AddToClassList("unity-base-field__aligned");
            fileField.RegisterValueChangedCallback(e =>
            {
                Undo.RecordObject(Mission, "Edit script file");
                EnsureEvaluationScript();
                _condition.EvaluationScript.ScriptFileAsset = e.newValue ?? string.Empty;
                EditorUtility.SetDirty(Mission);
                NotifyChanged?.Invoke();
            });
            body.Add(fileField);

            var methodField = new TextField("Script Method")
            {
                value = _condition.EvaluationScript?.ScriptMethod ?? string.Empty,
                isDelayed = true,
            };
            methodField.AddToClassList("condition-row-field");
            methodField.AddToClassList("unity-base-field__aligned");
            methodField.RegisterValueChangedCallback(e =>
            {
                Undo.RecordObject(Mission, "Edit script method");
                EnsureEvaluationScript();
                _condition.EvaluationScript.ScriptMethod = e.newValue ?? string.Empty;
                EditorUtility.SetDirty(Mission);
                NotifyChanged?.Invoke();
            });
            body.Add(methodField);
        }

        private void EnsureEvaluationScript()
        {
            if (_condition.EvaluationScript == null)
            {
                _condition.EvaluationScript = new ScriptMethodReference();
            }
        }
    }
}
