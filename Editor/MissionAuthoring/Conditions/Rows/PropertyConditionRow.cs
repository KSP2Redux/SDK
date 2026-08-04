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
    /// Bold "Property Condition" header with delete on the right, followed by aligned labeled rows for the watcher picker, operator, threshold, current-value requirement, and optional input string. Mirrors the Propellant card pattern used in part-authoring.
    /// </remarks>
    public sealed class PropertyConditionRow : ConditionRowBase
    {
        private readonly PropertyCondition _condition;
        private readonly Button _watcherButton;
        private readonly VisualElement _body;
        private readonly DropdownField _operatorField;
        private readonly VisualElement _thresholdSlot;
        private readonly VisualElement _inputSlot;
        private readonly VisualElement _variableSlot;
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

            var requireCurrentValueToggle = new Toggle("Require Current Value")
            {
                value = _condition.RequireCurrentValue,
                tooltip = "When enabled, the condition must still be true when evaluated instead of staying complete after it was met once.",
            };
            requireCurrentValueToggle.AddToClassList("condition-row-field");
            requireCurrentValueToggle.AddToClassList("unity-base-field__aligned");
            requireCurrentValueToggle.RegisterValueChangedCallback(OnRequireCurrentValueChanged);
            _body.Add(requireCurrentValueToggle);

            _inputSlot = new VisualElement();
            _inputSlot.AddToClassList("condition-row-input-slot");
            _body.Add(_inputSlot);

            _variableSlot = new VisualElement();
            _variableSlot.AddToClassList("condition-row-variable-slot");
            _body.Add(_variableSlot);

            RebuildOperatorOptions();
            RebuildThresholdField();
            RebuildInputField();
            RebuildVariableFields();
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
                RebuildVariableFields();
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

        private void OnRequireCurrentValueChanged(ChangeEvent<bool> evt)
        {
            Undo.RecordObject(Mission, "Edit current value requirement");
            _condition.RequireCurrentValue = evt.newValue;
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

        private void RebuildVariableFields()
        {
            _variableSlot.Clear();

            if (_entry?.OutputType == typeof(string))
            {
                var thresholdVariable = new TextField("Threshold Variable")
                {
                    value = _condition.TestWatchedStringVariable ?? string.Empty,
                    isDelayed = true,
                    tooltip = "Optional mission variable used instead of the literal string threshold. The condition remains false until this variable exists.",
                };
                thresholdVariable.AddToClassList("condition-row-field");
                thresholdVariable.AddToClassList("unity-base-field__aligned");
                thresholdVariable.RegisterValueChangedCallback(e =>
                {
                    Undo.RecordObject(Mission, "Edit condition threshold variable");
                    _condition.TestWatchedStringVariable = e.newValue ?? string.Empty;
                    EditorUtility.SetDirty(Mission);
                    NotifyChanged?.Invoke();
                });
                _variableSlot.Add(thresholdVariable);
            }

            var captureVariable = new TextField("Capture As Variable")
            {
                value = _condition.CaptureValueAsVariable ?? string.Empty,
                isDelayed = true,
                tooltip = "When the condition matches, stores the capture watcher's string value in this mission-scoped variable. Empty disables capture.",
            };
            captureVariable.AddToClassList("condition-row-field");
            captureVariable.AddToClassList("unity-base-field__aligned");
            captureVariable.RegisterValueChangedCallback(e =>
            {
                Undo.RecordObject(Mission, "Edit condition capture variable");
                _condition.CaptureValueAsVariable = e.newValue ?? string.Empty;
                EditorUtility.SetDirty(Mission);
                NotifyChanged?.Invoke();
            });
            _variableSlot.Add(captureVariable);

            var captureEntry = PropertyWatcherCatalog.FindByAqn(_condition.CapturePropertyTypeAQN);
            var captureRow = new VisualElement();
            captureRow.AddToClassList("picker-row");
            captureRow.AddToClassList("unity-base-field");
            captureRow.AddToClassList("unity-base-field__aligned");

            var captureLabel = new Label("Capture Watcher");
            captureLabel.AddToClassList("unity-base-field__label");
            captureLabel.AddToClassList("unity-property-field__label");
            captureRow.Add(captureLabel);

            var captureButton = new Button(() => OpenCaptureWatcherPicker())
            {
                text = captureEntry?.DisplayName ?? "(watched value)",
                tooltip = captureEntry?.Description ?? "Capture the main property watcher's string value.",
            };
            captureButton.AddToClassList("picker-row__button");
            captureRow.Add(captureButton);

            if (captureEntry != null)
            {
                var clearButton = new Button(ClearCaptureWatcher)
                {
                    text = "Clear",
                    tooltip = "Capture the main property watcher's value instead.",
                };
                captureRow.Add(clearButton);
            }

            _variableSlot.Add(captureRow);

            if (captureEntry?.TakesInput == true)
            {
                string label = !string.IsNullOrEmpty(captureEntry.InputDescription)
                    ? char.ToUpperInvariant(captureEntry.InputDescription[0]) + captureEntry.InputDescription.Substring(1)
                    : "Capture Input";
                var captureInput = new TextField(label)
                {
                    value = _condition.CaptureInputstring ?? string.Empty,
                    isDelayed = true,
                };
                captureInput.AddToClassList("condition-row-field");
                captureInput.AddToClassList("unity-base-field__aligned");
                captureInput.RegisterValueChangedCallback(e =>
                {
                    Undo.RecordObject(Mission, "Edit condition capture input");
                    _condition.CaptureInputstring = e.newValue ?? string.Empty;
                    _condition.CaptureIsInput = !string.IsNullOrEmpty(_condition.CaptureInputstring);
                    EditorUtility.SetDirty(Mission);
                    NotifyChanged?.Invoke();
                });
                _variableSlot.Add(captureInput);
            }
        }

        private void OpenCaptureWatcherPicker()
        {
            PropertyWatcherPicker.Open(entry =>
            {
                if (entry == null) return;
                if (entry.OutputType != typeof(string))
                {
                    EditorUtility.DisplayDialog(
                        "String watcher required",
                        "Mission variables currently store strings, so the capture watcher must return a string.",
                        "OK");
                    return;
                }

                Undo.RecordObject(Mission, "Pick condition capture watcher");
                _condition.CapturePropertyTypeAQN = entry.AssemblyQualifiedName;
                _condition.CaptureInputstring = string.Empty;
                _condition.CaptureIsInput = false;
                EditorUtility.SetDirty(Mission);
                RebuildVariableFields();
                NotifyChanged?.Invoke();
            });
        }

        private void ClearCaptureWatcher()
        {
            Undo.RecordObject(Mission, "Clear condition capture watcher");
            _condition.CapturePropertyTypeAQN = string.Empty;
            _condition.CaptureInputstring = string.Empty;
            _condition.CaptureIsInput = false;
            EditorUtility.SetDirty(Mission);
            RebuildVariableFields();
            NotifyChanged?.Invoke();
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
