using System;
using System.Collections.Generic;
using KSP.Game.Missions;
using Ksp2UnityTools.Editor.MissionAuthoring.Conditions.Pickers;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.MissionAuthoring.Conditions.Rows
{
    /// <summary>
    /// Card-style editor for a <see cref="PropertyCondition" />.
    /// </summary>
    /// <remarks>
    /// Bold "Property Condition" header with delete on the right, followed by aligned labeled rows for the watcher picker, operator, threshold, and optional input string. Mirrors the Propellant card pattern used in part-authoring.
    /// </remarks>
    public sealed class PropertyConditionRow : ConditionRowBase
    {
        private readonly PropertyCondition _condition;
        private readonly Button _watcherButton;
        private readonly VisualElement _body;
        private readonly DropdownField _operatorField;
        private readonly VisualElement _thresholdSlot;
        private readonly VisualElement _inputSlot;
        private PropertyWatcherCatalogEntry _entry;

        /// <summary>
        /// Constructs the card editor for a PropertyCondition.
        /// </summary>
        /// <param name="mission">The mission asset that owns the condition, used as the Undo target.</param>
        /// <param name="condition">The PropertyCondition instance this row edits.</param>
        /// <param name="replace">Callback invoked to swap the condition with another instance or null to delete.</param>
        /// <param name="notifyChanged">Callback invoked when the row mutates its condition.</param>
        /// <param name="moveUp">Callback that moves this row up in its parent's child list, or null when reorder is not available.</param>
        /// <param name="moveDown">Callback that moves this row down in its parent's child list, or null when reorder is not available.</param>
        public PropertyConditionRow(Mission mission, PropertyCondition condition, Action<Condition> replace, Action notifyChanged, Action moveUp = null, Action moveDown = null)
            : base(mission, condition, replace, notifyChanged, moveUp, moveDown)
        {
            _condition = condition;
            AddToClassList("condition-row-property-card");

            _entry = PropertyWatcherCatalog.FindByAqn(_condition?.PropertyTypeAQN);

            var header = new VisualElement();
            header.AddToClassList("condition-row-card-header");

            var title = new Label("Property Condition");
            title.AddToClassList("condition-row-card-title");
            header.Add(title);

            var spacer = new VisualElement();
            spacer.AddToClassList("condition-row-header-spacer");
            header.Add(spacer);

            BuildHeaderReorderAndWrapButtons(header);
            BuildHeaderDeleteButton(header);

            Add(header);

            _body = new VisualElement();
            _body.AddToClassList("condition-row-property-body");
            Add(_body);

            var watcherRow = new VisualElement();
            watcherRow.AddToClassList("picker-row");
            watcherRow.AddToClassList("unity-base-field");
            watcherRow.AddToClassList("unity-base-field__aligned");

            var watcherLabel = new Label("Property Watcher");
            watcherLabel.AddToClassList("unity-base-field__label");
            watcherLabel.AddToClassList("unity-property-field__label");
            watcherRow.Add(watcherLabel);

            _watcherButton = new Button(OpenWatcherPicker) { text = _entry?.DisplayName ?? "(pick watcher)" };
            _watcherButton.AddToClassList("picker-row__button");
            if (_entry == null) _watcherButton.AddToClassList("is-unset");
            if (_entry != null) _watcherButton.tooltip = _entry.Description;
            watcherRow.Add(_watcherButton);
            _body.Add(watcherRow);

            _operatorField = new DropdownField("Operator");
            _operatorField.AddToClassList("condition-row-field");
            _operatorField.AddToClassList("unity-base-field__aligned");
            _operatorField.RegisterValueChangedCallback(OnOperatorChanged);
            _body.Add(_operatorField);

            _thresholdSlot = new VisualElement();
            _thresholdSlot.AddToClassList("condition-row-threshold-slot");
            _body.Add(_thresholdSlot);

            _inputSlot = new VisualElement();
            _inputSlot.AddToClassList("condition-row-input-slot");
            _body.Add(_inputSlot);

            RebuildOperatorOptions();
            RebuildThresholdField();
            RebuildInputField();
        }

        private void OpenWatcherPicker()
        {
            PropertyWatcherPicker.Open(entry =>
            {
                if (entry == null) return;
                Undo.RecordObject(Mission, "Pick property watcher");
                _condition.PropertyTypeAQN = entry.AssemblyQualifiedName;
                EditorUtility.SetDirty(Mission);
                _entry = entry;
                _watcherButton.text = entry.DisplayName;
                _watcherButton.tooltip = entry.Description;
                _watcherButton.RemoveFromClassList("is-unset");
                RebuildOperatorOptions();
                RebuildThresholdField();
                RebuildInputField();
                NotifyChanged?.Invoke();
            });
        }

        private void RebuildOperatorOptions()
        {
            var allowed = AllowedOperators(_entry?.OutputType);
            var labels = new List<string>(allowed.Count);
            foreach (var op in allowed) labels.Add(OperatorLabel(op));
            _operatorField.choices = labels;

            var current = _condition.propOperator;
            if (!allowed.Contains(current))
            {
                current = allowed[0];
                _condition.propOperator = current;
            }
            _operatorField.SetValueWithoutNotify(OperatorLabel(current));
        }

        private void OnOperatorChanged(ChangeEvent<string> evt)
        {
            var op = ParseOperatorLabel(evt.newValue);
            if (op == _condition.propOperator) return;
            Undo.RecordObject(Mission, "Edit condition operator");
            _condition.propOperator = op;
            EditorUtility.SetDirty(Mission);
            NotifyChanged?.Invoke();
        }

        private void RebuildThresholdField()
        {
            _thresholdSlot.Clear();
            var outputType = _entry?.OutputType ?? typeof(double);
            string label = string.IsNullOrEmpty(_entry?.Units)
                ? "Threshold"
                : $"Threshold ({_entry.Units})";

            VisualElement widget;
            if (outputType == typeof(bool))
            {
                var toggle = new Toggle(label) { value = _condition.TestWatchedBool };
                toggle.RegisterValueChangedCallback(e =>
                {
                    Undo.RecordObject(Mission, "Edit condition threshold");
                    _condition.TestWatchedBool = e.newValue;
                    EditorUtility.SetDirty(Mission);
                    NotifyChanged?.Invoke();
                });
                widget = toggle;
            }
            else if (outputType == typeof(string))
            {
                var field = new TextField(label) { value = _condition.TestWatchedstring ?? string.Empty, isDelayed = true };
                field.RegisterValueChangedCallback(e =>
                {
                    Undo.RecordObject(Mission, "Edit condition threshold");
                    _condition.TestWatchedstring = e.newValue ?? string.Empty;
                    EditorUtility.SetDirty(Mission);
                    NotifyChanged?.Invoke();
                });
                widget = field;
            }
            else if (outputType.IsEnum)
            {
                var values = Enum.GetNames(outputType);
                var underlying = Enum.GetUnderlyingType(outputType);
                object boxed = null;
                try { boxed = Convert.ChangeType(_condition.TestWatchedInt, underlying); }
                catch { }
                string current = boxed != null && Enum.IsDefined(outputType, boxed)
                    ? Enum.GetName(outputType, boxed)
                    : values.Length > 0 ? values[0] : string.Empty;
                var dropdown = new DropdownField(label, new List<string>(values), current);
                dropdown.RegisterValueChangedCallback(e =>
                {
                    if (string.IsNullOrEmpty(e.newValue)) return;
                    var parsed = Enum.Parse(outputType, e.newValue);
                    Undo.RecordObject(Mission, "Edit condition threshold");
                    _condition.TestWatchedInt = Convert.ToInt32(parsed);
                    EditorUtility.SetDirty(Mission);
                    NotifyChanged?.Invoke();
                });
                widget = dropdown;
            }
            else if (outputType == typeof(int))
            {
                var field = new IntegerField(label) { value = _condition.TestWatchedInt, isDelayed = true };
                field.RegisterValueChangedCallback(e =>
                {
                    Undo.RecordObject(Mission, "Edit condition threshold");
                    _condition.TestWatchedInt = e.newValue;
                    EditorUtility.SetDirty(Mission);
                    NotifyChanged?.Invoke();
                });
                widget = field;
            }
            else
            {
                var field = new DoubleField(label) { value = _condition.TestWatchedValue, isDelayed = true };
                field.RegisterValueChangedCallback(e =>
                {
                    Undo.RecordObject(Mission, "Edit condition threshold");
                    _condition.TestWatchedValue = e.newValue;
                    EditorUtility.SetDirty(Mission);
                    NotifyChanged?.Invoke();
                });
                widget = field;
            }
            widget.AddToClassList("condition-row-field");
            widget.AddToClassList("unity-base-field__aligned");
            _thresholdSlot.Add(widget);
        }

        private void RebuildInputField()
        {
            _inputSlot.Clear();
            if (_entry == null || !_entry.TakesInput) return;

            string label = !string.IsNullOrEmpty(_entry.InputDescription)
                ? char.ToUpperInvariant(_entry.InputDescription[0]) + _entry.InputDescription.Substring(1)
                : "Input";

            var field = new TextField(label)
            {
                value = _condition.Inputstring ?? string.Empty,
                isDelayed = true,
            };
            field.AddToClassList("condition-row-field");
            field.AddToClassList("unity-base-field__aligned");
            field.RegisterValueChangedCallback(e =>
            {
                Undo.RecordObject(Mission, "Edit condition input");
                _condition.Inputstring = e.newValue ?? string.Empty;
                _condition.isInput = !string.IsNullOrEmpty(_condition.Inputstring);
                EditorUtility.SetDirty(Mission);
                NotifyChanged?.Invoke();
            });
            _inputSlot.Add(field);
        }

        private static List<PropertyOperator> AllowedOperators(Type outputType)
        {
            if (outputType == typeof(bool))
            {
                return new List<PropertyOperator> { PropertyOperator.EQUAL };
            }
            return new List<PropertyOperator>
            {
                PropertyOperator.LESSER,
                PropertyOperator.EQUAL,
                PropertyOperator.GREATER,
            };
        }

        private static string OperatorLabel(PropertyOperator op) => op switch
        {
            PropertyOperator.LESSER => "<",
            PropertyOperator.EQUAL => "=",
            PropertyOperator.GREATER => ">",
            _ => "?",
        };

        private static PropertyOperator ParseOperatorLabel(string label) => label switch
        {
            "<" => PropertyOperator.LESSER,
            "=" => PropertyOperator.EQUAL,
            ">" => PropertyOperator.GREATER,
            _ => PropertyOperator.EQUAL,
        };
    }
}
