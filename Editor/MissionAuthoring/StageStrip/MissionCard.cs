using System;
using KSP.Game.Missions;
using KSP.Game.Missions.Definitions;
using KSP.Game.Missions.State;
using Ksp2UnityTools.Editor.MissionAuthoring.Widgets;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.MissionAuthoring.StageStrip
{
    /// <summary>
    /// First card in the strip, representing the mission itself. Surfaces every authorable
    /// top-level <see cref="MissionData" /> field grouped into Identity, Behavior, Display
    /// and Rewards, and Assets sections. Runtime-managed fields (currentStageIndex,
    /// pendingCompletionTest, maxStageID) are intentionally not exposed.
    /// </summary>
    public class MissionCard : VisualElement
    {
        /// <summary>
        /// Gets the mission this card represents.
        /// </summary>
        public Mission Mission { get; }

        /// <summary>
        /// Gets the backing mission data asset edited by this card.
        /// </summary>
        public MissionData MissionData => Mission.missionData;

        /// <summary>
        /// Gets the exception-branch list section hosted in this card's body, or null if the mission has no data asset.
        /// </summary>
        public BranchListSection ExceptionSection { get; private set; }

        /// <summary>
        /// Gets the prerequisite-branch list section hosted in this card's body, or null if the mission has no data asset.
        /// </summary>
        public BranchListSection PrerequisiteSection { get; private set; }

        private TextField _idField;
        private TextField _missionGroupField;
        private TextField _nameField;
        private TextField _descriptionField;
        private MissionGranterKeyField _granterField;
        private GameModeFeatureIdField _gameModeFeatureIdField;
        private EnumField _typeField;
        private EnumField _ownerField;
        private EnumField _stateField;
        private TextField _missionScriptField;
        private Toggle _hiddenToggle;
        private TriumphLoopVideoKeyField _triumphLoopVideoKeyField;
        private Toggle _visibleRewardsToggle;
        private EnumField _uiDisplayTypeField;
        private TextField _missionSaveAssetKeyField;

        /// <summary>
        /// Creates a new mission card bound to the given mission.
        /// </summary>
        /// <param name="mission">The mission to surface in this card.</param>
        public MissionCard(Mission mission)
        {
            Mission = mission;
            AddToClassList("stage-card");
            AddToClassList("mission-card");

            var title = new Label("Mission");
            title.AddToClassList("stage-card-title");
            Add(title);

            BuildMetadataFields();

            if (MissionData != null)
            {
                ExceptionSection = new BranchListSection(
                    Mission,
                    () => Mission?.missionData?.ExceptionBranches,
                    BranchKind.Exception,
                    "Exception",
                    "+",
                    "Add exception branch",
                    DefaultMissionTarget);
                ExceptionSection.AddToClassList("mission-card-branch-section");
                Add(ExceptionSection);

                PrerequisiteSection = new BranchListSection(
                    Mission,
                    () => Mission?.missionData?.PreRequisiteBranches,
                    BranchKind.Prerequisite,
                    "Prerequisite",
                    "+",
                    "Add prerequisite branch",
                    DefaultMissionTarget);
                PrerequisiteSection.AddToClassList("mission-card-branch-section");
                Add(PrerequisiteSection);
            }
        }

        private void BuildMetadataFields()
        {
            if (MissionData == null) return;

            var identity = BuildSection("Identity");
            _idField = MakeText(identity, "ID", MissionData.ID, v => MissionData.ID = v, "Edit mission ID");
            _missionGroupField = MakeText(identity, "Group", MissionData.MissionGroup, v => MissionData.MissionGroup = v, "Edit mission group");
            _nameField = MakeText(identity, "Name", MissionData.name, v => MissionData.name = v, "Edit mission name");
            _descriptionField = MakeText(identity, "Description", MissionData.description, v => MissionData.description = v, "Edit mission description");
            _granterField = new MissionGranterKeyField("Granter", MissionData.MissionGranterKey ?? string.Empty, v =>
            {
                Undo.RecordObject(Mission, "Edit mission granter");
                MissionData.MissionGranterKey = v;
                EditorUtility.SetDirty(Mission);
            });
            _granterField.AddToClassList("mission-card-field");
            identity.Add(_granterField);

            var behavior = BuildSection("Behavior");
            _gameModeFeatureIdField = new GameModeFeatureIdField("Feature Id", MissionData.GameModeFeatureId ?? string.Empty, v =>
            {
                Undo.RecordObject(Mission, "Edit game mode feature id");
                MissionData.GameModeFeatureId = v;
                EditorUtility.SetDirty(Mission);
            });
            _gameModeFeatureIdField.AddToClassList("mission-card-field");
            behavior.Add(_gameModeFeatureIdField);
            _typeField = MakeEnum(behavior, "Type", MissionData.type, v => MissionData.type = (MissionType)v, "Edit mission type");
            _ownerField = MakeEnum(behavior, "Owner", MissionData.Owner, v => MissionData.Owner = (MissionOwner)v, "Edit mission owner");
            _stateField = MakeEnum(behavior, "State", MissionData.state, v => MissionData.state = (MissionState)v, "Edit mission state");
            _missionScriptField = MakeText(behavior, "Script", MissionData.missionScript, v => MissionData.missionScript = v, "Edit mission script");

            var display = BuildSection("Display & Rewards");
            _hiddenToggle = MakeToggle(display, "Hidden", MissionData.Hidden, v => MissionData.Hidden = v, "Edit mission hidden");
            _triumphLoopVideoKeyField = new TriumphLoopVideoKeyField("Triumph Video", MissionData.TriumphLoopVideoKey ?? string.Empty, v =>
            {
                Undo.RecordObject(Mission, "Edit triumph loop video key");
                MissionData.TriumphLoopVideoKey = v;
                EditorUtility.SetDirty(Mission);
            });
            _triumphLoopVideoKeyField.AddToClassList("mission-card-field");
            display.Add(_triumphLoopVideoKeyField);
            _visibleRewardsToggle = MakeToggle(display, "Visible Rewards", MissionData.VisibleRewards, v => MissionData.VisibleRewards = v, "Edit visible rewards");
            _uiDisplayTypeField = MakeEnum(display, "Display Type", MissionData.uiDisplayType, v => MissionData.uiDisplayType = (UIDisplayType)v, "Edit UI display type");

            var assets = BuildSection("Assets");
            _missionSaveAssetKeyField = MakeText(assets, "Save Asset Key", MissionData.MissionSaveAssetKey, v => MissionData.MissionSaveAssetKey = v, "Edit mission save asset key");
        }

        private VisualElement BuildSection(string title)
        {
            var section = new VisualElement();
            section.AddToClassList("mission-card-section");

            var header = new Label(title);
            header.AddToClassList("mission-card-section-header");
            section.Add(header);

            Add(section);
            return section;
        }

        private TextField MakeText(VisualElement parent, string label, string initial, Action<string> setter, string undoLabel)
        {
            var field = new TextField(label) { value = initial ?? string.Empty };
            field.AddToClassList("mission-card-field");
            field.RegisterValueChangedCallback(evt =>
            {
                Undo.RecordObject(Mission, undoLabel);
                setter(evt.newValue ?? string.Empty);
                EditorUtility.SetDirty(Mission);
            });
            parent.Add(field);
            return field;
        }

        private Toggle MakeToggle(VisualElement parent, string label, bool initial, Action<bool> setter, string undoLabel)
        {
            var field = new Toggle(label) { value = initial };
            field.AddToClassList("mission-card-field");
            field.RegisterValueChangedCallback(evt =>
            {
                Undo.RecordObject(Mission, undoLabel);
                setter(evt.newValue);
                EditorUtility.SetDirty(Mission);
            });
            parent.Add(field);
            return field;
        }

        private EnumField MakeEnum(VisualElement parent, string label, Enum initial, Action<Enum> setter, string undoLabel)
        {
            var field = new EnumField(label, initial);
            field.AddToClassList("mission-card-field");
            field.RegisterValueChangedCallback(evt =>
            {
                Undo.RecordObject(Mission, undoLabel);
                setter(evt.newValue);
                EditorUtility.SetDirty(Mission);
            });
            parent.Add(field);
            return field;
        }

        /// <summary>
        /// Refreshes all view-state from the backing mission data. Called by the strip view's
        /// undo cascade so the card displays the current model values after a Ctrl+Z.
        /// </summary>
        public void Reconcile()
        {
            if (MissionData == null) return;

            _idField?.SetValueWithoutNotify(MissionData.ID ?? string.Empty);
            _missionGroupField?.SetValueWithoutNotify(MissionData.MissionGroup ?? string.Empty);
            _nameField?.SetValueWithoutNotify(MissionData.name ?? string.Empty);
            _descriptionField?.SetValueWithoutNotify(MissionData.description ?? string.Empty);
            _granterField?.SetValueWithoutNotify(MissionData.MissionGranterKey ?? string.Empty);
            _gameModeFeatureIdField?.SetValueWithoutNotify(MissionData.GameModeFeatureId ?? string.Empty);
            _typeField?.SetValueWithoutNotify(MissionData.type);
            _ownerField?.SetValueWithoutNotify(MissionData.Owner);
            _stateField?.SetValueWithoutNotify(MissionData.state);
            _missionScriptField?.SetValueWithoutNotify(MissionData.missionScript ?? string.Empty);
            _hiddenToggle?.SetValueWithoutNotify(MissionData.Hidden);
            _triumphLoopVideoKeyField?.SetValueWithoutNotify(MissionData.TriumphLoopVideoKey ?? string.Empty);
            _visibleRewardsToggle?.SetValueWithoutNotify(MissionData.VisibleRewards);
            _uiDisplayTypeField?.SetValueWithoutNotify(MissionData.uiDisplayType);
            _missionSaveAssetKeyField?.SetValueWithoutNotify(MissionData.MissionSaveAssetKey ?? string.Empty);

            ExceptionSection?.Reconcile();
            PrerequisiteSection?.Reconcile();
        }

        private int DefaultMissionTarget()
        {
            var stages = MissionData?.missionStages;
            if (stages == null || stages.Count == 0) return -1;
            return stages[0].StageID;
        }
    }
}
