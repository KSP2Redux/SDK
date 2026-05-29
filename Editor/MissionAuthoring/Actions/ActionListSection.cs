using System;
using System.Collections.Generic;
using KSP.Game.Missions;
using Ksp2UnityTools.Editor.Widgets;
using UnityEditor;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.MissionAuthoring.Actions
{
    /// <summary>
    /// Vertical list of <see cref="IMissionAction" /> cards.
    /// </summary>
    /// <remarks>
    /// Backed by <see cref="CardListSection.BuildFromList{T}" /> so chrome (count header, add button, empty hint, card container) is shared with the rest of the SDK's card-list surfaces. Per-entry visuals come from <see cref="ActionRowFactory" />.
    /// </remarks>
    public class ActionListSection : VisualElement
    {
        /// <summary>
        /// Gets the mission asset the actions belong to.
        /// </summary>
        public Mission Mission { get; }

        /// <summary>
        /// Gets the current backing list of actions resolved through the supplied resolver delegate.
        /// </summary>
        public IList<IMissionAction> Actions => _actionsResolver?.Invoke();

        private readonly Func<IList<IMissionAction>> _actionsResolver;
        private readonly CardListSection.ListHandle _handle;

        /// <summary>
        /// Creates a new <see cref="ActionListSection" /> bound to the supplied mission and action list.
        /// </summary>
        /// <param name="mission">The mission asset the actions belong to.</param>
        /// <param name="actionsResolver">Delegate that returns the backing list of actions to render.</param>
        /// <param name="title">The header title shown above the action cards.</param>
        public ActionListSection(Mission mission, Func<IList<IMissionAction>> actionsResolver, string title)
        {
            Mission = mission;
            _actionsResolver = actionsResolver;

            AddToClassList("action-list-section");

            _handle = CardListSection.BuildFromList<IMissionAction>(actionsResolver, new CardListSection.ListConfig<IMissionAction>
            {
                Title = title,
                AddButtonTooltip = "Add action",
                EmptyHintText = "(none)",
                OnAddClicked = OnAddClicked,
                BuildCard = BuildActionCard,
            });

            Add(_handle.Root);
        }

        /// <summary>
        /// Rebuilds the card list against the current backing list.
        /// </summary>
        public void Reconcile() => _handle?.Rebuild?.Invoke();

        private void OnAddClicked()
        {
            ActionTypePicker.Open(entry =>
            {
                if (entry?.Create == null) return;
                var newAction = entry.Create();
                if (newAction == null) return;
                Undo.RegisterCompleteObjectUndo(Mission, "Add action");
                var list = Actions;
                if (list == null) return;
                list.Add(newAction);
                EditorUtility.SetDirty(Mission);
                _handle?.Rebuild?.Invoke();
            });
        }

        private VisualElement BuildActionCard(IMissionAction action, int index)
        {
            var list = Actions;
            int count = list?.Count ?? 0;

            System.Action moveUp = index > 0 ? (System.Action)(() => MoveAction(index, -1)) : null;
            System.Action moveDown = index < count - 1 ? (System.Action)(() => MoveAction(index, +1)) : null;

            var row = ActionRowFactory.Build(
                Mission,
                action,
                newAction => ReplaceAction(index, newAction),
                () => { },
                moveUp,
                moveDown);
            row.AddToClassList("action-list-card");
            return row;
        }

        private void ReplaceAction(int index, IMissionAction newAction)
        {
            var list = Actions;
            if (list == null || index < 0 || index >= list.Count) return;
            Undo.RegisterCompleteObjectUndo(Mission, newAction == null ? "Delete action" : "Replace action");
            if (newAction == null) list.RemoveAt(index);
            else list[index] = newAction;
            EditorUtility.SetDirty(Mission);
            _handle?.Rebuild?.Invoke();
        }

        private void MoveAction(int index, int delta)
        {
            var list = Actions;
            if (list == null) return;
            int target = index + delta;
            if (target < 0 || target >= list.Count) return;
            Undo.RegisterCompleteObjectUndo(Mission, "Reorder action");
            var item = list[index];
            list.RemoveAt(index);
            list.Insert(target, item);
            EditorUtility.SetDirty(Mission);
            _handle?.Rebuild?.Invoke();
        }
    }
}
