using System;
using System.Collections.Generic;
using KSP.Game.Missions;
using Ksp2UnityTools.Editor.Widgets;
using UnityEditor;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.MissionAuthoring.StageStrip
{
    /// <summary>
    /// Vertical list of <see cref="MissionBranch" /> cards backed by
    /// <see cref="CardListSection.BuildFromList{T}" /> so chrome (count header, add button,
    /// empty hint, card container) is shared with the rest of the SDK's card-list surfaces.
    /// Cards are cached by <see cref="MissionBranch" /> identity so disclosure and target-
    /// field state survives rebuilds across reorders and undo-driven Reconciles.
    /// </summary>
    public class BranchListSection : VisualElement
    {
        /// <summary>
        /// Gets the mission that owns the backing branch list.
        /// </summary>
        public Mission Mission { get; }

        /// <summary>
        /// Resolves to the current backing list on every access. Survives undo rebuilds of the parent stage or MissionData that would orphan a captured list reference.
        /// </summary>
        public IList<MissionBranch> Branches => _branchesResolver?.Invoke();

        /// <summary>
        /// Gets which runtime container (stage-local, exception, or prerequisite) backs this section.
        /// </summary>
        public BranchKind Kind { get; }

        /// <summary>
        /// Gets the live ordered card view, sorted to match the current backing branch list.
        /// </summary>
        public IReadOnlyList<BranchCard> Cards => _orderedCards;

        private readonly Func<IList<MissionBranch>> _branchesResolver;
        private readonly Dictionary<MissionBranch, BranchCard> _cardsByBranch = new();
        private readonly List<BranchCard> _orderedCards = new();
        private readonly Func<int> _defaultTargetProvider;
        private readonly string _title;
        private readonly string _addUndoLabel;
        private readonly CardListSection.ListHandle _handle;

        /// <summary>
        /// Creates a new branch list section bound to the given mission and backing list.
        /// </summary>
        /// <param name="mission">The mission that owns the backing branch list.</param>
        /// <param name="branchesResolver">Accessor returning the current backing list, re-evaluated on every access to survive undo rebuilds.</param>
        /// <param name="kind">Which runtime container (stage-local, exception, or prerequisite) backs this section.</param>
        /// <param name="title">The header title shown above the card container.</param>
        /// <param name="addLabel">Text displayed inside the add button.</param>
        /// <param name="addUndoLabel">Undo group label recorded when the user adds a branch via the add button.</param>
        /// <param name="defaultTargetProvider">Provider for the TargetStage value assigned to newly-added branches.</param>
        public BranchListSection(
            Mission mission,
            Func<IList<MissionBranch>> branchesResolver,
            BranchKind kind,
            string title,
            string addLabel,
            string addUndoLabel,
            Func<int> defaultTargetProvider)
        {
            Mission = mission;
            _branchesResolver = branchesResolver;
            Kind = kind;
            _title = title;
            _addUndoLabel = addUndoLabel;
            _defaultTargetProvider = defaultTargetProvider;

            AddToClassList("branch-section");

            _handle = CardListSection.BuildFromList<MissionBranch>(branchesResolver, new CardListSection.ListConfig<MissionBranch>
            {
                Title = title,
                AddButtonText = addLabel,
                AddButtonTooltip = addUndoLabel,
                EmptyHintText = "(none)",
                OnAddClicked = OnAddClicked,
                BuildCard = BuildBranchCard,
            });

            Add(_handle.Root);
        }

        /// <summary>
        /// Brings the section into sync with the current branch list. Reuses surviving cards
        /// by branch identity so disclosure / target-field state is preserved across the
        /// reconcile (typical Ctrl+Z restore).
        /// </summary>
        public void Reconcile()
        {
            _handle?.Rebuild?.Invoke();
        }

        /// <summary>
        /// Removes any cached cards whose backing branch is in <paramref name="branches" />.
        /// Called by the strip view when an orphan cleanup at delete-stage time has already
        /// removed the branches from the backing list. The next rebuild will not re-mount
        /// these stale cards.
        /// </summary>
        /// <param name="branches">The set of branches whose cached cards should be discarded.</param>
        public void RemoveCardsForBranches(HashSet<MissionBranch> branches)
        {
            foreach (var b in branches)
            {
                if (_cardsByBranch.TryGetValue(b, out var card))
                {
                    card.RemoveFromHierarchy();
                    _cardsByBranch.Remove(b);
                }
            }
            _handle?.Rebuild?.Invoke();
        }

        private VisualElement BuildBranchCard(MissionBranch branch, int index)
        {
            BranchCard card;
            if (branch != null && _cardsByBranch.TryGetValue(branch, out card))
            {
                card.RemoveFromHierarchy();
                card.Reconcile();
            }
            else
            {
                card = CreateCard(branch);
                if (branch != null) _cardsByBranch[branch] = card;
            }

            int count = Branches?.Count ?? 0;
            card.SetCanMoveUp(index > 0);
            card.SetCanMoveDown(index < count - 1);

            RefreshOrderedCardsTracker();
            return card;
        }

        private BranchCard CreateCard(MissionBranch branch)
        {
            var card = new BranchCard(Mission, branch, Kind, _branchesResolver);
            card.TargetChangeRequested += OnCardTargetChange;
            card.DeleteRequested += OnCardDeleteRequested;
            card.MoveUpRequested += c => MoveCard(c, -1);
            card.MoveDownRequested += c => MoveCard(c, +1);
            return card;
        }

        private void RefreshOrderedCardsTracker()
        {
            _orderedCards.Clear();
            var list = Branches;
            if (list == null) return;
            foreach (var branch in list)
            {
                if (branch != null && _cardsByBranch.TryGetValue(branch, out var card))
                {
                    _orderedCards.Add(card);
                }
            }
        }

        private void MoveCard(BranchCard card, int delta)
        {
            var list = Branches;
            if (list == null) return;
            int oldIndex = list.IndexOf(card.Branch);
            if (oldIndex < 0) return;
            int newIndex = oldIndex + delta;
            if (newIndex < 0 || newIndex >= list.Count) return;

            Undo.RecordObject(Mission, "Reorder branch");
            var branch = card.Branch;
            list.RemoveAt(oldIndex);
            list.Insert(newIndex, branch);
            EditorUtility.SetDirty(Mission);
            _handle?.Rebuild?.Invoke();
        }

        private void OnCardTargetChange(BranchCard card, int newTarget)
        {
            Undo.RecordObject(Mission, "Edit branch target");
            card.Branch.TargetStage = newTarget;
            EditorUtility.SetDirty(Mission);
        }

        private void OnAddClicked()
        {
            Undo.RecordObject(Mission, _addUndoLabel);
            var branch = new MissionBranch
            {
                TargetStage = _defaultTargetProvider?.Invoke() ?? -1,
                ExceptionBranch = Kind == BranchKind.Exception,
                IsPreRequisiteBranch = Kind == BranchKind.Prerequisite,
            };
            var list = Branches;
            if (list == null) return;
            list.Add(branch);
            EditorUtility.SetDirty(Mission);
            _handle?.Rebuild?.Invoke();
        }

        private void OnCardDeleteRequested(BranchCard card)
        {
            var list = Branches;
            if (list == null) return;
            Undo.RecordObject(Mission, "Delete branch");
            list.Remove(card.Branch);
            if (card.Branch != null) _cardsByBranch.Remove(card.Branch);
            EditorUtility.SetDirty(Mission);
            _handle?.Rebuild?.Invoke();
        }
    }
}
