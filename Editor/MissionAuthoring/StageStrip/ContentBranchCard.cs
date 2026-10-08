using System;
using KSP.Game.Missions;
using Ksp2UnityTools.Editor.MissionAuthoring.Actions;
using Ksp2UnityTools.Editor.MissionAuthoring.Widgets;
using UnityEditor;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.MissionAuthoring.StageStrip
{
    /// <summary>
    /// Sideways card representing one <see cref="MissionContentBranch" /> in the strip
    /// view's bottom row. Title row carries the autocomplete-backed ID field, reorder
    /// controls, and delete. Body hosts a per-branch <see cref="ActionListSection" />.
    /// </summary>
    public class ContentBranchCard : VisualElement
    {
        /// <summary>
        /// Gets the mission that owns the backing content branch.
        /// </summary>
        public Mission Mission { get; }

        /// <summary>
        /// Gets the runtime content branch this card represents.
        /// </summary>
        public MissionContentBranch Branch { get; }

        /// <summary>
        /// Raised when the user clicks the delete control button.
        /// </summary>
        public event Action<ContentBranchCard> DeleteRequested;

        /// <summary>
        /// Raised when the user clicks the move-left control button.
        /// </summary>
        public event Action<ContentBranchCard> MoveLeftRequested;

        /// <summary>
        /// Raised when the user clicks the move-right control button.
        /// </summary>
        public event Action<ContentBranchCard> MoveRightRequested;

        private readonly ContentBranchIdField _idField;
        private readonly Button _moveLeftButton;
        private readonly Button _moveRightButton;
        private readonly ActionListSection _actionSection;

        /// <summary>
        /// Creates a new content branch card bound to the given runtime branch.
        /// </summary>
        /// <param name="mission">The mission that owns the backing content branch.</param>
        /// <param name="branch">The runtime content branch this card represents.</param>
        public ContentBranchCard(Mission mission, MissionContentBranch branch)
        {
            Mission = mission;
            Branch = branch;
            AddToClassList("stage-card");
            AddToClassList("content-branch-card");

            var titleRow = new VisualElement();
            titleRow.AddToClassList("stage-card-title-row");
            titleRow.AddToClassList("content-branch-card-title-row");

            _idField = new ContentBranchIdField("ID", branch?.ID ?? string.Empty, v =>
            {
                if (branch == null) return;
                Undo.RecordObject(Mission, "Edit content branch ID");
                branch.ID = v ?? string.Empty;
                EditorUtility.SetDirty(Mission);
            });
            _idField.AddToClassList("content-branch-card-id-field");
            titleRow.Add(_idField);

            var spacer = new VisualElement();
            spacer.AddToClassList("stage-card-title-spacer");
            titleRow.Add(spacer);

            _moveLeftButton = MakeControlButton("◀", "Move content branch left", () => MoveLeftRequested?.Invoke(this));
            _moveRightButton = MakeControlButton("▶", "Move content branch right", () => MoveRightRequested?.Invoke(this));
            titleRow.Add(_moveLeftButton);
            titleRow.Add(_moveRightButton);

            var deleteBtn = MakeControlButton("X", "Delete content branch", () => DeleteRequested?.Invoke(this));
            titleRow.Add(deleteBtn);

            Add(titleRow);

            _actionSection = new ActionListSection(
                Mission,
                () =>
                {
                    var list = Mission?.missionData?.ContentBranches;
                    if (list == null || branch == null) return null;
                    int idx = list.IndexOf(branch);
                    if (idx < 0) return null;
                    return list[idx]?.actions;
                },
                "Actions");
            _actionSection.AddToClassList("content-branch-card-action-section");
            Add(_actionSection);
        }

        /// <summary>
        /// Refreshes all view-state from the backing branch. Called by the strip view's
        /// undo cascade so the card displays the current model values after a Ctrl+Z.
        /// </summary>
        public void Reconcile()
        {
            _idField?.SetValueWithoutNotify(Branch?.ID ?? string.Empty);
            _actionSection?.Reconcile();
        }

        /// <summary>
        /// Enables or disables the move-left control button.
        /// </summary>
        /// <param name="can">True to enable the button, false to disable it.</param>
        public void SetCanMoveLeft(bool can) => _moveLeftButton.SetEnabled(can);

        /// <summary>
        /// Enables or disables the move-right control button.
        /// </summary>
        /// <param name="can">True to enable the button, false to disable it.</param>
        public void SetCanMoveRight(bool can) => _moveRightButton.SetEnabled(can);

        private static Button MakeControlButton(string text, string tooltip, Action onClick)
        {
            var btn = new Button(onClick) { text = text, tooltip = tooltip };
            btn.AddToClassList("stage-card-control-button");
            return btn;
        }
    }
}
