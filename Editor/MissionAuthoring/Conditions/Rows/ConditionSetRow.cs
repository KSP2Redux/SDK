using System;
using System.Collections.Generic;
using KSP.Game.Missions;
using Ksp2UnityTools.Editor.Widgets;
using UnityEditor;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.MissionAuthoring.Conditions.Rows
{
    /// <summary>
    /// Card-style editor for a <see cref="ConditionSet" />.
    /// </summary>
    /// <remarks>
    /// Operator dropdown, child-list section (via shared <see cref="CardListSection.BuildFromList{T}" /> chrome), and an Unwrap affordance under the stack when the set holds a single non-NOT child.
    /// </remarks>
    public sealed class ConditionSetRow : ConditionRowBase
    {
        private readonly ConditionSet _set;
        private readonly DropdownField _operatorField;
        private readonly Button _unwrapButton;
        private readonly CardListSection.ListHandle _handle;

        /// <summary>
        /// Constructs the card editor for a ConditionSet.
        /// </summary>
        /// <param name="mission">The mission asset that owns the condition, used as the Undo target.</param>
        /// <param name="set">The ConditionSet instance this row edits.</param>
        /// <param name="replace">Callback invoked to swap the set with another condition or null to delete.</param>
        /// <param name="notifyChanged">Callback invoked when the row mutates its condition.</param>
        /// <param name="moveUp">Callback that moves this row up in its parent's child list, or null when reorder is not available.</param>
        /// <param name="moveDown">Callback that moves this row down in its parent's child list, or null when reorder is not available.</param>
        public ConditionSetRow(Mission mission, ConditionSet set, Action<Condition> replace, Action notifyChanged, Action moveUp = null, Action moveDown = null)
            : base(mission, set, replace, notifyChanged, moveUp, moveDown)
        {
            _set = set;
            AddToClassList("condition-row-property-card");

            var header = new VisualElement();
            header.AddToClassList("condition-row-card-header");

            var title = new Label("Condition Set");
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

            _operatorField = new DropdownField("Operator", BuildOperatorChoices(), OperatorLabel(_set.ConditionMode));
            _operatorField.AddToClassList("condition-row-field");
            _operatorField.AddToClassList("unity-base-field__aligned");
            _operatorField.RegisterValueChangedCallback(OnOperatorChanged);
            body.Add(_operatorField);

            _handle = CardListSection.BuildFromList<Condition>(() => _set.Children, new CardListSection.ListConfig<Condition>
            {
                Title = "Children",
                AddButtonTooltip = "Add child condition",
                EmptyHintText = "(none)",
                OnAddClicked = OnAddChildClicked,
                BuildCard = BuildChildCard,
            });
            body.Add(_handle.Root);

            _unwrapButton = new Button(OnUnwrapClicked) { text = "Unwrap", tooltip = "Replace this set with its single child" };
            _unwrapButton.AddToClassList("condition-set-unwrap-btn");
            body.Add(_unwrapButton);

            UpdateUnwrapVisibility();
        }

        private void OnOperatorChanged(ChangeEvent<string> evt)
        {
            var newOp = ParseOperatorLabel(evt.newValue);
            if (newOp == _set.ConditionMode) return;

            if (newOp == LogicalOperator.NOT && _set.Children.Count > 1)
            {
                bool keepFirst = EditorUtility.DisplayDialog(
                    "Condition Set",
                    "NOT requires a single child. Keep only the first child and discard the rest?",
                    "Keep first",
                    "Cancel");
                if (!keepFirst)
                {
                    _operatorField.SetValueWithoutNotify(OperatorLabel(_set.ConditionMode));
                    return;
                }
                Undo.RecordObject(Mission, "Switch ConditionSet to NOT");
                while (_set.Children.Count > 1) _set.Children.RemoveAt(_set.Children.Count - 1);
                _set.ConditionMode = newOp;
                EditorUtility.SetDirty(Mission);
                RebuildChildren();
                NotifyChanged?.Invoke();
                return;
            }

            Undo.RecordObject(Mission, "Change ConditionSet operator");
            _set.ConditionMode = newOp;
            EditorUtility.SetDirty(Mission);
            UpdateUnwrapVisibility();
            NotifyChanged?.Invoke();
        }

        private void OnAddChildClicked()
        {
            if (_set.ConditionMode == LogicalOperator.NOT && _set.Children.Count >= 1)
            {
                EditorUtility.DisplayDialog(
                    "Condition Set",
                    "NOT can only have one child. Remove the existing child first or change the operator.",
                    "OK");
                return;
            }

            var menu = new GenericDropdownMenu();
            foreach (var entry in ConditionTypeCatalog.GetEntries())
            {
                menu.AddItem(entry.DisplayName, false, () =>
                {
                    if (entry.Create == null) return;
                    Undo.RecordObject(Mission, "Add child condition");
                    _set.Children.Add(entry.Create());
                    EditorUtility.SetDirty(Mission);
                    RebuildChildren();
                    NotifyChanged?.Invoke();
                });
            }
            menu.DropDown(worldBound, this, anchored: false);
        }

        private void OnUnwrapClicked()
        {
            if (_set.Children == null || _set.Children.Count != 1) return;
            if (_set.ConditionMode == LogicalOperator.NOT) return;
            if (Replace == null) return;
            Undo.RecordObject(Mission, "Unwrap ConditionSet");
            var only = _set.Children[0];
            Replace.Invoke(only);
            EditorUtility.SetDirty(Mission);
        }

        private void RebuildChildren()
        {
            _handle?.Rebuild?.Invoke();
            UpdateUnwrapVisibility();
        }

        private VisualElement BuildChildCard(Condition child, int index)
        {
            int count = _set.Children?.Count ?? 0;

            Action moveChildUp = index > 0 ? (Action)(() => MoveChild(index, -1)) : null;
            Action moveChildDown = index < count - 1 ? (Action)(() => MoveChild(index, +1)) : null;

            var childRow = ConditionRowFactory.Build(
                Mission,
                child,
                newChild => ReplaceChild(index, newChild),
                NotifyChanged,
                moveChildUp,
                moveChildDown);
            childRow.AddToClassList("condition-set-child");
            return childRow;
        }

        private void UpdateUnwrapVisibility()
        {
            int count = _set.Children?.Count ?? 0;
            bool show = count == 1 && _set.ConditionMode != LogicalOperator.NOT;
            _unwrapButton.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void ReplaceChild(int index, Condition newChild)
        {
            if (index < 0 || index >= _set.Children.Count) return;
            Undo.RecordObject(Mission, newChild == null ? "Delete child condition" : "Replace child condition");
            if (newChild == null) _set.Children.RemoveAt(index);
            else _set.Children[index] = newChild;
            EditorUtility.SetDirty(Mission);
            RebuildChildren();
            NotifyChanged?.Invoke();
        }

        private void MoveChild(int index, int delta)
        {
            int target = index + delta;
            if (target < 0 || target >= _set.Children.Count) return;
            Undo.RegisterCompleteObjectUndo(Mission, "Reorder child condition");
            var item = _set.Children[index];
            _set.Children.RemoveAt(index);
            _set.Children.Insert(target, item);
            EditorUtility.SetDirty(Mission);
            RebuildChildren();
            NotifyChanged?.Invoke();
        }

        private static List<string> BuildOperatorChoices() => new()
        {
            OperatorLabel(LogicalOperator.AND),
            OperatorLabel(LogicalOperator.OR),
            OperatorLabel(LogicalOperator.XOR),
            OperatorLabel(LogicalOperator.NOT),
        };

        private static string OperatorLabel(LogicalOperator op) => op switch
        {
            LogicalOperator.AND => "AND",
            LogicalOperator.OR => "OR",
            LogicalOperator.XOR => "XOR",
            LogicalOperator.NOT => "NOT",
            _ => "AND",
        };

        private static LogicalOperator ParseOperatorLabel(string label) => label switch
        {
            "AND" => LogicalOperator.AND,
            "OR" => LogicalOperator.OR,
            "XOR" => LogicalOperator.XOR,
            "NOT" => LogicalOperator.NOT,
            _ => LogicalOperator.AND,
        };
    }
}
