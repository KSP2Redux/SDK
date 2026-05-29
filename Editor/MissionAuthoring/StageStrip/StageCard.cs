using System;
using System.Linq;
using KSP.Game.Missions;
using KSP.Game.Missions.Definitions;
using Ksp2UnityTools.Editor.MissionAuthoring.Actions;
using Ksp2UnityTools.Editor.MissionAuthoring.Conditions;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.MissionAuthoring.StageStrip
{
    /// <summary>
    /// One card per <see cref="MissionStage" /> in the strip view. Title row carries the
    /// editable Stage ID plus reorder + delete controls. Body hosts editable scalar fields
    /// and the outgoing stage-local branch chips. The strip view owns
    /// add/delete/move/id-change validation, this class just raises events.
    /// </summary>
    public class StageCard : VisualElement
    {
        /// <summary>
        /// Gets the runtime stage this card represents.
        /// </summary>
        public MissionStage Stage { get; }

        /// <summary>
        /// Gets the mission that owns the backing stage.
        /// </summary>
        public Mission Mission { get; }

        /// <summary>
        /// Raised when the user commits a new value into the Stage ID IntegerField. The strip view validates
        /// uniqueness and either applies the change or calls <see cref="RevertStageId" /> to roll back the display.
        /// </summary>
        public event Action<StageCard, int> StageIdChangeRequested;

        /// <summary>
        /// Raised when the user clicks the move-left control button.
        /// </summary>
        public event Action<StageCard> MoveLeftRequested;

        /// <summary>
        /// Raised when the user clicks the move-right control button.
        /// </summary>
        public event Action<StageCard> MoveRightRequested;

        /// <summary>
        /// Raised when the user clicks the delete control button.
        /// </summary>
        public event Action<StageCard> DeleteRequested;

        /// <summary>
        /// Gets the stage-local branch list section hosted in this card's body.
        /// </summary>
        public BranchListSection BranchSection { get; private set; }

        /// <summary>
        /// Gets the action list section hosted in this card's body.
        /// </summary>
        public ActionListSection ActionSection { get; private set; }

        /// <summary>
        /// Gets the reward list section hosted in this card's body.
        /// </summary>
        public RewardListSection RewardSection { get; private set; }

        private readonly VisualElement _body;
        private readonly IntegerField _stageIdField;
        private readonly Button _moveLeftButton;
        private readonly Button _moveRightButton;
        private readonly Label _nameLabel;
        private TextField _nameField;
        private TextField _descriptionField;
        private TextField _objectiveField;
        private Toggle _displayObjectiveToggle;
        private Toggle _revealOnActivateToggle;
        private Toggle _ignoreExceptionsToggle;

        /// <summary>
        /// Creates a new stage card bound to the given stage.
        /// </summary>
        /// <param name="stage">The runtime stage this card represents.</param>
        /// <param name="mission">The mission that owns the backing stage.</param>
        public StageCard(MissionStage stage, Mission mission)
        {
            Stage = stage;
            Mission = mission;
            AddToClassList("stage-card");

            // Title row: "Stage" label + ID field + spacer + control buttons.
            var titleRow = new VisualElement();
            titleRow.AddToClassList("stage-card-title-row");

            var stageWord = new Label("Stage");
            stageWord.AddToClassList("stage-card-title");
            stageWord.AddToClassList("stage-card-title-word");
            titleRow.Add(stageWord);

            _stageIdField = new IntegerField { value = stage.StageID, isDelayed = true };
            _stageIdField.AddToClassList("stage-card-id-field");
            _stageIdField.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue == evt.previousValue) return;
                StageIdChangeRequested?.Invoke(this, evt.newValue);
            });
            titleRow.Add(_stageIdField);

            var spacer = new VisualElement();
            spacer.AddToClassList("stage-card-title-spacer");
            titleRow.Add(spacer);

            _moveLeftButton = MakeControlButton("<", "Move stage left in flow order", () => MoveLeftRequested?.Invoke(this));
            _moveRightButton = MakeControlButton(">", "Move stage right in flow order", () => MoveRightRequested?.Invoke(this));
            var deleteButton = MakeControlButton("X", "Delete stage", () => DeleteRequested?.Invoke(this));

            titleRow.Add(_moveLeftButton);
            titleRow.Add(_moveRightButton);
            titleRow.Add(deleteButton);

            Add(titleRow);

            _nameLabel = new Label(stage.name ?? string.Empty);
            _nameLabel.AddToClassList("stage-card-name");
            UpdateNameLabelVisibility();
            Add(_nameLabel);

            _body = new VisualElement();
            _body.AddToClassList("stage-card-body");
            Add(_body);

            BuildDetailFields();
            BuildBranchSection();
        }

        private Button MakeControlButton(string label, string tooltip, Action onClick)
        {
            var btn = new Button(onClick) { text = label, tooltip = tooltip };
            btn.AddToClassList("stage-card-control-button");
            btn.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());
            return btn;
        }

        /// <summary>
        /// Reverts the ID field to a known value without firing the change callback. Used by
        /// the strip view when a requested ID change is rejected (duplicate).
        /// </summary>
        /// <param name="value">The StageID value to display.</param>
        public void RevertStageId(int value)
        {
            _stageIdField.SetValueWithoutNotify(value);
        }

        /// <summary>
        /// Enables or disables the move-left control button.
        /// </summary>
        /// <param name="canMove">True to enable the button, false to disable it.</param>
        public void SetCanMoveLeft(bool canMove) => _moveLeftButton.SetEnabled(canMove);

        /// <summary>
        /// Enables or disables the move-right control button.
        /// </summary>
        /// <param name="canMove">True to enable the button, false to disable it.</param>
        public void SetCanMoveRight(bool canMove) => _moveRightButton.SetEnabled(canMove);

        private void BuildDetailFields()
        {
            _nameField = MakeTextField("Name", Stage.name, v =>
            {
                Stage.name = v;
                _nameLabel.text = v ?? string.Empty;
                UpdateNameLabelVisibility();
            }, "Edit stage name");
            _body.Add(_nameField);

            _descriptionField = MakeTextField("Description", Stage.description, v => Stage.description = v, "Edit stage description", multiline: true);
            _body.Add(_descriptionField);

            _objectiveField = MakeTextField("Objective", Stage.Objective, v => Stage.Objective = v, "Edit stage objective");
            _body.Add(_objectiveField);

            _displayObjectiveToggle = MakeToggle("Display Objective", Stage.DisplayObjective, v => Stage.DisplayObjective = v, "Toggle DisplayObjective");
            _body.Add(_displayObjectiveToggle);

            _revealOnActivateToggle = MakeToggle("Reveal on Activate", Stage.RevealObjectiveOnActivate, v => Stage.RevealObjectiveOnActivate = v, "Toggle RevealObjectiveOnActivate");
            _body.Add(_revealOnActivateToggle);

            _ignoreExceptionsToggle = MakeToggle("Ignore Exceptions", Stage.IgnoreExceptionBranches, v => Stage.IgnoreExceptionBranches = v, "Toggle IgnoreExceptionBranches");
            _body.Add(_ignoreExceptionsToggle);
        }

        /// <summary>
        /// Refreshes all view-state from the backing stage. Called by the strip view's
        /// undo cascade so the card displays the current model values after a Ctrl+Z.
        /// </summary>
        public void Reconcile()
        {
            _stageIdField.SetValueWithoutNotify(Stage.StageID);

            var nameValue = Stage.name ?? string.Empty;
            _nameField.SetValueWithoutNotify(nameValue);
            _nameLabel.text = nameValue;
            UpdateNameLabelVisibility();

            _descriptionField.SetValueWithoutNotify(Stage.description ?? string.Empty);
            _objectiveField.SetValueWithoutNotify(Stage.Objective ?? string.Empty);
            _displayObjectiveToggle.SetValueWithoutNotify(Stage.DisplayObjective);
            _revealOnActivateToggle.SetValueWithoutNotify(Stage.RevealObjectiveOnActivate);
            _ignoreExceptionsToggle.SetValueWithoutNotify(Stage.IgnoreExceptionBranches);

            BranchSection?.Reconcile();
            ActionSection?.Reconcile();
            RewardSection?.Reconcile();
        }

        private void UpdateNameLabelVisibility()
        {
            _nameLabel.style.display = string.IsNullOrEmpty(_nameLabel.text)
                ? DisplayStyle.None
                : DisplayStyle.Flex;
        }

        private TextField MakeTextField(string label, string initial, Action<string> setter, string undoLabel, bool multiline = false)
        {
            var field = new TextField(label) { value = initial ?? string.Empty, multiline = multiline };
            field.AddToClassList("stage-card-field");
            field.RegisterValueChangedCallback(evt =>
            {
                Undo.RecordObject(Mission, undoLabel);
                setter(evt.newValue);
                EditorUtility.SetDirty(Mission);
            });
            return field;
        }

        private Toggle MakeToggle(string label, bool initial, Action<bool> setter, string undoLabel)
        {
            var field = new Toggle(label) { value = initial };
            field.AddToClassList("stage-card-field");
            field.RegisterValueChangedCallback(evt =>
            {
                Undo.RecordObject(Mission, undoLabel);
                setter(evt.newValue);
                EditorUtility.SetDirty(Mission);
            });
            return field;
        }

        private void BuildBranchSection()
        {
            if (Mission != null && Stage != null)
            {
                var conditionEditor = new ConditionTreeEditor(
                    Mission,
                    "Edit stage condition",
                    () => Stage?.condition,
                    v => { if (Stage != null) Stage.condition = v; });
                conditionEditor.AddToClassList("stage-card-condition-editor");
                _body.Add(conditionEditor);
            }

            BranchSection = new BranchListSection(
                Mission,
                () => Mission?.missionData?.missionStages?.FirstOrDefault(s => s != null && s.StageID == Stage.StageID)?.branches,
                BranchKind.StageLocal,
                "Branches",
                "+",
                "Add stage branch",
                DefaultStageLocalTarget);
            BranchSection.AddToClassList("stage-card-branch-section");
            _body.Add(BranchSection);

            ActionSection = new ActionListSection(
                Mission,
                () => Mission?.missionData?.missionStages?.FirstOrDefault(s => s != null && s.StageID == Stage.StageID)?.actions,
                "Actions");
            ActionSection.AddToClassList("stage-card-action-section");
            _body.Add(ActionSection);

            RewardSection = new RewardListSection(
                Mission,
                () => Mission?.missionData?.missionStages?.FirstOrDefault(s => s != null && s.StageID == Stage.StageID)?.MissionReward?.MissionRewardDefinitions,
                "Rewards");
            RewardSection.AddToClassList("stage-card-reward-section");
            _body.Add(RewardSection);
        }

        private int DefaultStageLocalTarget()
        {
            var stages = Mission?.missionData?.missionStages;
            if (stages == null || stages.Count == 0) return -1;
            int idx = stages.IndexOf(Stage);
            if (idx < 0) return stages[0].StageID;
            if (idx + 1 < stages.Count) return stages[idx + 1].StageID;
            return stages[0].StageID;
        }
    }
}
