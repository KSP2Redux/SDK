using System;
using System.Collections.Generic;
using System.Linq;
using KSP.Game.Missions;
using Ksp2UnityTools.Editor.MissionAuthoring.Conditions;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.MissionAuthoring.StageStrip
{
    /// <summary>
    /// Disclosure-toggleable card representing one <see cref="MissionBranch" />. Lives inside
    /// a stage card body (stage-local branches) or the mission card body (Exception /
    /// Prerequisite branches). Header carries the condition-presence dot, target IntegerField,
    /// and delete button. Body is reserved for the future condition tree editor and is empty
    /// in Phase 1.
    /// </summary>
    public class BranchCard : VisualElement
    {
        /// <summary>
        /// Gets the mission that owns the backing branch.
        /// </summary>
        public Mission Mission { get; }

        /// <summary>
        /// Gets the runtime branch this card represents.
        /// </summary>
        public MissionBranch Branch { get; }

        /// <summary>
        /// Gets which runtime container (stage-local, exception, or prerequisite) holds the backing branch.
        /// </summary>
        public BranchKind Kind { get; }

        /// <summary>
        /// Gets the current target StageID of the backing branch, or -1 if the branch is null.
        /// </summary>
        public int TargetStageId => Branch?.TargetStage ?? -1;

        /// <summary>
        /// Raised when the user commits a new value into the target IntegerField. The strip view validates
        /// and applies the change, or calls <see cref="RevertTarget" /> to roll back the displayed value.
        /// </summary>
        public event Action<BranchCard, int> TargetChangeRequested;

        /// <summary>
        /// Raised when the user clicks the card's delete button.
        /// </summary>
        public event Action<BranchCard> DeleteRequested;

        /// <summary>
        /// Raised when the user clicks the up-arrow reorder button.
        /// </summary>
        public event Action<BranchCard> MoveUpRequested;

        /// <summary>
        /// Raised when the user clicks the down-arrow reorder button.
        /// </summary>
        public event Action<BranchCard> MoveDownRequested;

        private readonly Label _conditionDot;
        private readonly IntegerField _targetField;
        private readonly Button _disclosure;
        private readonly Button _moveUpButton;
        private readonly Button _moveDownButton;
        private readonly VisualElement _body;
        private readonly Func<IList<MissionBranch>> _listResolver;
        private readonly int _capturedIndex;
        private bool _expanded = true;

        /// <summary>
        /// Creates a new branch card bound to the given runtime branch.
        /// </summary>
        /// <param name="mission">The mission that owns the backing branch.</param>
        /// <param name="branch">The runtime branch this card represents.</param>
        /// <param name="kind">Which runtime container (stage-local, exception, or prerequisite) holds the branch.</param>
        /// <param name="listResolver">Optional accessor for the backing list, used by <see cref="ResolveBranch" /> to recover after an undo orphans the original branch reference.</param>
        public BranchCard(Mission mission, MissionBranch branch, BranchKind kind, Func<IList<MissionBranch>> listResolver = null)
        {
            Mission = mission;
            Branch = branch;
            Kind = kind;
            _listResolver = listResolver;
            _capturedIndex = listResolver?.Invoke()?.IndexOf(branch) ?? -1;
            AddToClassList("branch-card");
            AddToClassList($"branch-card-{kind.ToString().ToLowerInvariant()}");

            var header = new VisualElement();
            header.AddToClassList("branch-card-header");
            Add(header);

            _disclosure = new Button(ToggleExpanded) { text = "▼" };
            _disclosure.AddToClassList("branch-card-disclosure");
            header.Add(_disclosure);

            _conditionDot = new Label("•");
            _conditionDot.AddToClassList("branch-card-condition-dot");
            header.Add(_conditionDot);

            var arrow = new Label("→");
            arrow.AddToClassList("branch-card-arrow");
            header.Add(arrow);

            _targetField = new IntegerField { value = branch?.TargetStage ?? -1, isDelayed = true };
            _targetField.AddToClassList("branch-card-target-field");
            _targetField.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue == evt.previousValue) return;
                TargetChangeRequested?.Invoke(this, evt.newValue);
            });
            header.Add(_targetField);

            var spacer = new VisualElement();
            spacer.AddToClassList("branch-card-header-spacer");
            header.Add(spacer);

            _moveUpButton = new Button(() => MoveUpRequested?.Invoke(this)) { text = "▲", tooltip = "Move branch up" };
            _moveUpButton.AddToClassList("branch-card-move-btn");
            header.Add(_moveUpButton);

            _moveDownButton = new Button(() => MoveDownRequested?.Invoke(this)) { text = "▼", tooltip = "Move branch down" };
            _moveDownButton.AddToClassList("branch-card-move-btn");
            header.Add(_moveDownButton);

            var removeBtn = new Button(() => DeleteRequested?.Invoke(this)) { text = "X", tooltip = "Delete branch" };
            removeBtn.AddToClassList("branch-card-remove-btn");
            header.Add(removeBtn);

            _body = new VisualElement();
            _body.AddToClassList("branch-card-body");
            Add(_body);

            if (Mission != null && Branch != null)
            {
                var editor = new ConditionTreeEditor(
                    Mission,
                    "Edit branch condition",
                    () => ResolveBranch()?.condition,
                    v => { var b = ResolveBranch(); if (b != null) b.condition = v; });
                editor.RootChanged += RefreshConditionDot;
                _body.Add(editor);
            }

            RefreshConditionDot();
        }

        /// <summary>
        /// Pushes the current <see cref="MissionBranch.TargetStage" /> into the IntegerField
        /// without raising the value-changed callback. Called by the strip view after an
        /// upstream StageID renumber rewrites this branch's target.
        /// </summary>
        public void RefreshTargetDisplay()
        {
            _targetField.SetValueWithoutNotify(Branch?.TargetStage ?? -1);
        }

        /// <summary>
        /// Re-checks <see cref="MissionBranch.condition" /> and shows or hides the
        /// condition-presence dot accordingly.
        /// </summary>
        public void RefreshConditionDot()
        {
            _conditionDot.style.display = Branch?.condition != null
                ? DisplayStyle.Flex
                : DisplayStyle.None;
        }

        /// <summary>
        /// Reverts the IntegerField to a known value without firing the change callback.
        /// Used when the parent rejects a committed target value.
        /// </summary>
        /// <param name="value">The StageID value to display.</param>
        public void RevertTarget(int value)
        {
            _targetField.SetValueWithoutNotify(value);
        }

        /// <summary>
        /// Enables or disables the up-arrow reorder button.
        /// </summary>
        /// <param name="canMove">True to enable the button, false to disable it.</param>
        public void SetCanMoveUp(bool canMove) => _moveUpButton.SetEnabled(canMove);

        /// <summary>
        /// Enables or disables the down-arrow reorder button.
        /// </summary>
        /// <param name="canMove">True to enable the button, false to disable it.</param>
        public void SetCanMoveDown(bool canMove) => _moveDownButton.SetEnabled(canMove);

        /// <summary>
        /// Refreshes all view-state from the backing branch. Called by the strip view's
        /// undo cascade so the card displays the current model values after a Ctrl+Z.
        /// </summary>
        public void Reconcile()
        {
            RefreshTargetDisplay();
            RefreshConditionDot();
        }

        /// <summary>
        /// Returns the MissionBranch this card should currently operate on. Prefers identity
        /// match against the live list (correct after reorder), then falls back to the
        /// captured construction-time index (correct after an undo that orphaned the
        /// original Branch reference). Returns null if neither resolves.
        /// </summary>
        private MissionBranch ResolveBranch()
        {
            var list = _listResolver?.Invoke();
            if (list == null) return Branch;
            if (Branch != null)
            {
                int idx = list.IndexOf(Branch);
                if (idx >= 0) return list[idx];
            }
            if (_capturedIndex >= 0 && _capturedIndex < list.Count) return list[_capturedIndex];
            return null;
        }

        private void ToggleExpanded()
        {
            _expanded = !_expanded;
            _body.style.display = _expanded ? DisplayStyle.Flex : DisplayStyle.None;
            _disclosure.text = _expanded ? "▼" : "▶";
        }
    }
}
