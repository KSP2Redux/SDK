using System;
using System.Collections.Generic;
using KSP.Game.Missions;
using KSP.Game.Missions.Definitions;
using UnityEditor;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.MissionAuthoring.StageStrip
{
    /// <summary>
    /// Two-row strip view. Top row carries the mission timeline (MissionCard, arrow,
    /// StageCards, +Add Stage). Bottom row carries the mission's MissionContentBranch
    /// cards. A small label precedes each row and a thin divider separates them. The
    /// outer ScrollView is column-flexed. Each row is its own horizontal flex container.
    /// </summary>
    public class StageStripView : VisualElement
    {
        /// <summary>
        /// Gets the mission currently displayed by this view, or null if nothing is bound.
        /// </summary>
        public Mission BoundMission { get; private set; }

        /// <summary>
        /// Fires after any coarse-grained mutation completes (Bind, Add, Delete, Move, StageID
        /// change, Reconcile). Validation surfaces and other observers subscribe here for
        /// model-change notifications. Field-level edits on cards (TextField commits, branch
        /// target IntegerField commits) do not currently raise this event.
        /// </summary>
        public event Action ModelChanged;

        private readonly ScrollView _scroll;
        private readonly VisualElement _topRow;
        private readonly VisualElement _bottomRow;
        private readonly List<StageCard> _stageCards = new();
        private readonly Dictionary<int, StageCard> _stageById = new();
        private readonly List<ContentBranchCard> _contentBranchCards = new();
        private MissionCard _missionCard;
        private VisualElement _addStageCard;
        private VisualElement _addContentBranchCard;

        public StageStripView()
        {
            AddToClassList("stage-strip");
            style.flexGrow = 1;

            _scroll = new ScrollView(ScrollViewMode.VerticalAndHorizontal);
            _scroll.style.flexGrow = 1;
            _scroll.AddToClassList("stage-strip-scroll");
            Add(_scroll);

            var topRowLabel = new Label("Mission Flow");
            topRowLabel.AddToClassList("stage-strip-row-label");
            _scroll.contentContainer.Add(topRowLabel);

            _topRow = new VisualElement();
            _topRow.AddToClassList("stage-strip-row");
            _scroll.contentContainer.Add(_topRow);

            var divider = new VisualElement();
            divider.AddToClassList("stage-strip-divider");
            _scroll.contentContainer.Add(divider);

            var bottomRowLabel = new Label("Content Branches");
            bottomRowLabel.AddToClassList("stage-strip-row-label");
            _scroll.contentContainer.Add(bottomRowLabel);

            _bottomRow = new VisualElement();
            _bottomRow.AddToClassList("stage-strip-row");
            _scroll.contentContainer.Add(_bottomRow);

            Undo.undoRedoPerformed += OnUndoRedoPerformed;
            RegisterCallback<DetachFromPanelEvent>(_ => Undo.undoRedoPerformed -= OnUndoRedoPerformed);
        }

        private void OnUndoRedoPerformed()
        {
            if (BoundMission == null) return;
            Reconcile();
        }

        /// <summary>
        /// Brings the entire strip back into sync with the current mission state after an
        /// external mutation (typically Ctrl+Z restoring the Mission SO). Reuses surviving
        /// stage and content-branch cards by reference, removes stale ones, creates cards
        /// for new entries, and cascades a refresh down through MissionCard and every
        /// StageCard / BranchCard.
        /// </summary>
        public void Reconcile()
        {
            if (BoundMission?.missionData == null) return;

            _missionCard?.Reconcile();
            ReconcileStages();
            ReconcileContentBranches();

            ModelChanged?.Invoke();
        }

        private void ReconcileStages()
        {
            if (BoundMission.missionData.missionStages == null) return;

            var existing = new Dictionary<MissionStage, StageCard>();
            foreach (var c in _stageCards)
            {
                if (c.Stage != null) existing[c.Stage] = c;
            }

            var stages = BoundMission.missionData.missionStages;
            var newOrder = new List<StageCard>(stages.Count);
            foreach (var stage in stages)
            {
                if (stage != null && existing.TryGetValue(stage, out var card))
                {
                    existing.Remove(stage);
                    card.Reconcile();
                    newOrder.Add(card);
                }
                else
                {
                    newOrder.Add(CreateStageCard(stage));
                }
            }

            foreach (var stale in existing.Values)
            {
                if (stale.parent != null) stale.RemoveFromHierarchy();
            }

            for (int i = _topRow.childCount - 1; i >= 0; i--)
            {
                var child = _topRow[i];
                if (child.ClassListContains("stage-strip-arrow"))
                {
                    child.RemoveFromHierarchy();
                }
            }
            foreach (var card in newOrder)
            {
                if (card.parent != null) card.RemoveFromHierarchy();
            }

            int insertAt = (_missionCard != null ? _topRow.IndexOf(_missionCard) : -1) + 1;
            if (newOrder.Count > 0)
            {
                _topRow.Insert(insertAt++, MakeArrow());
                for (int i = 0; i < newOrder.Count; i++)
                {
                    _topRow.Insert(insertAt++, newOrder[i]);
                    if (i < newOrder.Count - 1)
                    {
                        _topRow.Insert(insertAt++, MakeArrow());
                    }
                }
                _topRow.Insert(insertAt++, MakeArrow());
            }

            _stageCards.Clear();
            _stageCards.AddRange(newOrder);
            _stageById.Clear();
            foreach (var c in newOrder) _stageById[c.Stage.StageID] = c;

            RefreshMoveButtonStates();
        }

        private void ReconcileContentBranches()
        {
            var branches = BoundMission.missionData.ContentBranches;
            if (branches == null) branches = new List<MissionContentBranch>();

            var existing = new Dictionary<MissionContentBranch, ContentBranchCard>();
            foreach (var c in _contentBranchCards)
            {
                if (c.Branch != null) existing[c.Branch] = c;
            }

            var newOrder = new List<ContentBranchCard>(branches.Count);
            foreach (var branch in branches)
            {
                if (branch != null && existing.TryGetValue(branch, out var card))
                {
                    existing.Remove(branch);
                    card.Reconcile();
                    newOrder.Add(card);
                }
                else
                {
                    newOrder.Add(CreateContentBranchCard(branch));
                }
            }

            foreach (var stale in existing.Values)
            {
                if (stale.parent != null) stale.RemoveFromHierarchy();
            }

            foreach (var card in newOrder)
            {
                if (card.parent != null) card.RemoveFromHierarchy();
            }

            int insertAt = 0;
            foreach (var card in newOrder)
            {
                _bottomRow.Insert(insertAt++, card);
            }

            _contentBranchCards.Clear();
            _contentBranchCards.AddRange(newOrder);

            RefreshContentBranchMoveButtonStates();
        }

        /// <summary>
        /// Rebinds the view to the given mission and rebuilds the entire strip from scratch.
        /// </summary>
        /// <param name="mission">The mission to display, or null to clear the view.</param>
        public void Bind(Mission mission)
        {
            BoundMission = mission;
            BuildFromScratch();
            ModelChanged?.Invoke();
        }

        private void BuildFromScratch()
        {
            _topRow.Clear();
            _bottomRow.Clear();
            _stageCards.Clear();
            _stageById.Clear();
            _contentBranchCards.Clear();
            _missionCard = null;
            _addStageCard = null;
            _addContentBranchCard = null;

            if (BoundMission?.missionData == null) return;
            var data = BoundMission.missionData;

            _missionCard = new MissionCard(BoundMission);
            _topRow.Add(_missionCard);

            if (data.missionStages != null && data.missionStages.Count > 0)
            {
                _topRow.Add(MakeArrow());
            }

            var stages = data.missionStages;
            if (stages != null)
            {
                for (int i = 0; i < stages.Count; i++)
                {
                    var card = CreateStageCard(stages[i]);
                    _topRow.Add(card);
                    _stageCards.Add(card);
                    _stageById[stages[i].StageID] = card;
                    if (i < stages.Count - 1) _topRow.Add(MakeArrow());
                }
                if (stages.Count > 0) _topRow.Add(MakeArrow());
            }

            _addStageCard = BuildAddStageCard();
            _topRow.Add(_addStageCard);

            RefreshMoveButtonStates();

            var contentBranches = data.ContentBranches;
            if (contentBranches != null)
            {
                foreach (var branch in contentBranches)
                {
                    var card = CreateContentBranchCard(branch);
                    _contentBranchCards.Add(card);
                    _bottomRow.Add(card);
                }
            }

            _addContentBranchCard = BuildAddContentBranchCard();
            _bottomRow.Add(_addContentBranchCard);

            RefreshContentBranchMoveButtonStates();
        }

        private StageCard CreateStageCard(MissionStage stage)
        {
            var card = new StageCard(stage, BoundMission);
            card.StageIdChangeRequested += OnStageIdChangeRequested;
            card.MoveLeftRequested += OnMoveLeftRequested;
            card.MoveRightRequested += OnMoveRightRequested;
            card.DeleteRequested += OnDeleteRequested;
            return card;
        }

        private VisualElement BuildAddStageCard()
        {
            var card = new VisualElement { tooltip = "Add a new stage at the end of the mission" };
            card.AddToClassList("stage-card");
            card.AddToClassList("stage-strip-add-card");
            var label = new Label("+");
            label.AddToClassList("stage-strip-add-label");
            card.Add(label);
            card.RegisterCallback<ClickEvent>(evt =>
            {
                AddStage();
                evt.StopPropagation();
            });
            return card;
        }

        private void RefreshMoveButtonStates()
        {
            for (int i = 0; i < _stageCards.Count; i++)
            {
                _stageCards[i].SetCanMoveLeft(i > 0);
                _stageCards[i].SetCanMoveRight(i < _stageCards.Count - 1);
            }
        }

        // Stage CRUD (incremental).

        private void AddStage()
        {
            if (BoundMission?.missionData == null) return;
            var data = BoundMission.missionData;

            int newId = data.maxStageID + 1;
            while (IndexOfStageById(newId) >= 0) newId++;

            Undo.RecordObject(BoundMission, "Add stage");
            var newStage = new MissionStage
            {
                StageID = newId,
                name = string.Empty,
                description = string.Empty,
            };
            data.missionStages.Add(newStage);
            if (newId > data.maxStageID) data.maxStageID = newId;
            EditorUtility.SetDirty(BoundMission);

            int addIdx = _topRow.IndexOf(_addStageCard);
            var card = CreateStageCard(newStage);

            if (_stageCards.Count == 0)
            {
                _topRow.Insert(addIdx, MakeArrow());
                _topRow.Insert(addIdx + 1, card);
                _topRow.Insert(addIdx + 2, MakeArrow());
            }
            else
            {
                _topRow.Insert(addIdx, card);
                _topRow.Insert(addIdx + 1, MakeArrow());
            }

            _stageCards.Add(card);
            _stageById[newId] = card;
            RefreshMoveButtonStates();
            ModelChanged?.Invoke();
        }

        private void OnDeleteRequested(StageCard card) => DeleteStage(card);

        private void DeleteStage(StageCard card)
        {
            if (BoundMission?.missionData == null) return;
            var data = BoundMission.missionData;
            var stage = card.Stage;

            var orphans = CollectBranchesTargeting(stage.StageID);
            if (orphans.Count > 0)
            {
                bool deleteWithBranches = EditorUtility.DisplayDialog(
                    "Delete stage",
                    $"{orphans.Count} branch{(orphans.Count == 1 ? string.Empty : "es")} target Stage {stage.StageID}. " +
                    $"Delete the stage and those branches together?",
                    "Delete with branches",
                    "Cancel");
                if (!deleteWithBranches) return;

                Undo.RecordObject(BoundMission, "Delete stage and orphan branches");
                RemoveBranchesFromRuntime(orphans);
                RemoveCardsForBranches(orphans);
            }
            else
            {
                Undo.RecordObject(BoundMission, "Delete stage");
            }

            data.missionStages.Remove(stage);
            EditorUtility.SetDirty(BoundMission);

            int cardIdx = _topRow.IndexOf(card);
            bool wasOnlyStage = _stageCards.Count == 1;
            bool wasFirst = _stageCards[0] == card;
            bool wasLast = _stageCards[_stageCards.Count - 1] == card;

            if (wasOnlyStage)
            {
                _topRow.RemoveAt(cardIdx + 1);
                _topRow.Remove(card);
                _topRow.RemoveAt(cardIdx - 1);
            }
            else
            {
                _topRow.RemoveAt(cardIdx + 1);
                _topRow.Remove(card);
            }

            _stageCards.Remove(card);
            _stageById.Remove(stage.StageID);

            RefreshMoveButtonStates();
            ModelChanged?.Invoke();
        }

        // Stage reorder (incremental).

        private void OnMoveLeftRequested(StageCard card) => MoveStage(card, -1);
        private void OnMoveRightRequested(StageCard card) => MoveStage(card, +1);

        private void MoveStage(StageCard card, int delta)
        {
            if (BoundMission?.missionData == null) return;
            var data = BoundMission.missionData;
            int listIndex = _stageCards.IndexOf(card);
            int newListIndex = listIndex + delta;
            if (listIndex < 0 || newListIndex < 0 || newListIndex >= _stageCards.Count) return;

            Undo.RecordObject(BoundMission, "Move stage");
            int dataIndex = data.missionStages.IndexOf(card.Stage);
            data.missionStages.RemoveAt(dataIndex);
            data.missionStages.Insert(dataIndex + delta, card.Stage);
            EditorUtility.SetDirty(BoundMission);

            var other = _stageCards[newListIndex];
            int otherContentIdx = _topRow.IndexOf(other);
            int cardContentIdx = _topRow.IndexOf(card);
            int arrowBetweenIdx = (cardContentIdx + otherContentIdx) / 2;
            var arrow = _topRow[arrowBetweenIdx];

            _topRow.Remove(arrow);
            _topRow.Remove(card);
            _topRow.Remove(other);

            int leftIdx = Math.Min(cardContentIdx, otherContentIdx);

            if (delta < 0)
            {
                _topRow.Insert(leftIdx, card);
                _topRow.Insert(leftIdx + 1, arrow);
                _topRow.Insert(leftIdx + 2, other);
            }
            else
            {
                _topRow.Insert(leftIdx, other);
                _topRow.Insert(leftIdx + 1, arrow);
                _topRow.Insert(leftIdx + 2, card);
            }

            _stageCards.RemoveAt(listIndex);
            _stageCards.Insert(newListIndex, card);

            RefreshMoveButtonStates();
            ModelChanged?.Invoke();
        }

        // Stage ID edit (incremental).

        private void OnStageIdChangeRequested(StageCard card, int requestedId)
        {
            if (BoundMission?.missionData == null) return;
            var data = BoundMission.missionData;
            int oldId = card.Stage.StageID;
            if (requestedId == oldId) return;

            int existingIndex = IndexOfStageById(requestedId);
            if (existingIndex >= 0 && data.missionStages[existingIndex] != card.Stage)
            {
                EditorUtility.DisplayDialog(
                    "Stage ID in use",
                    $"Stage ID {requestedId} is already used by another stage. Pick a different number.",
                    "OK");
                card.RevertStageId(oldId);
                return;
            }

            Undo.RecordObject(BoundMission, "Change stage ID");
            var retargeted = CollectBranchesTargeting(oldId);
            foreach (var branch in retargeted) branch.TargetStage = requestedId;
            card.Stage.StageID = requestedId;
            if (requestedId > data.maxStageID) data.maxStageID = requestedId;
            EditorUtility.SetDirty(BoundMission);

            _stageById.Remove(oldId);
            _stageById[requestedId] = card;
            RefreshAllBranchCardDisplays();
            ModelChanged?.Invoke();
        }

        // Content branch CRUD (incremental).

        private ContentBranchCard CreateContentBranchCard(MissionContentBranch branch)
        {
            var card = new ContentBranchCard(BoundMission, branch);
            card.DeleteRequested += OnContentBranchDeleteRequested;
            card.MoveLeftRequested += OnContentBranchMoveLeftRequested;
            card.MoveRightRequested += OnContentBranchMoveRightRequested;
            return card;
        }

        private VisualElement BuildAddContentBranchCard()
        {
            var card = new VisualElement { tooltip = "Add a new content branch at the end of the row" };
            card.AddToClassList("stage-card");
            card.AddToClassList("stage-strip-add-card");
            var label = new Label("+");
            label.AddToClassList("stage-strip-add-label");
            card.Add(label);
            card.RegisterCallback<ClickEvent>(evt =>
            {
                AddContentBranch();
                evt.StopPropagation();
            });
            return card;
        }

        private void RefreshContentBranchMoveButtonStates()
        {
            for (int i = 0; i < _contentBranchCards.Count; i++)
            {
                _contentBranchCards[i].SetCanMoveLeft(i > 0);
                _contentBranchCards[i].SetCanMoveRight(i < _contentBranchCards.Count - 1);
            }
        }

        private void AddContentBranch()
        {
            if (BoundMission?.missionData == null) return;
            var data = BoundMission.missionData;
            if (data.ContentBranches == null) data.ContentBranches = new List<MissionContentBranch>();

            Undo.RegisterCompleteObjectUndo(BoundMission, "Add content branch");
            var newBranch = new MissionContentBranch { ID = string.Empty };
            data.ContentBranches.Add(newBranch);
            EditorUtility.SetDirty(BoundMission);

            var card = CreateContentBranchCard(newBranch);
            int addIdx = _bottomRow.IndexOf(_addContentBranchCard);
            _bottomRow.Insert(addIdx, card);

            _contentBranchCards.Add(card);
            RefreshContentBranchMoveButtonStates();
            ModelChanged?.Invoke();
        }

        private void OnContentBranchDeleteRequested(ContentBranchCard card)
        {
            if (BoundMission?.missionData?.ContentBranches == null) return;
            Undo.RegisterCompleteObjectUndo(BoundMission, "Delete content branch");
            BoundMission.missionData.ContentBranches.Remove(card.Branch);
            EditorUtility.SetDirty(BoundMission);

            _bottomRow.Remove(card);
            _contentBranchCards.Remove(card);
            RefreshContentBranchMoveButtonStates();
            ModelChanged?.Invoke();
        }

        private void OnContentBranchMoveLeftRequested(ContentBranchCard card) => MoveContentBranch(card, -1);
        private void OnContentBranchMoveRightRequested(ContentBranchCard card) => MoveContentBranch(card, +1);

        private void MoveContentBranch(ContentBranchCard card, int delta)
        {
            if (BoundMission?.missionData?.ContentBranches == null) return;
            var list = BoundMission.missionData.ContentBranches;
            int listIndex = _contentBranchCards.IndexOf(card);
            int newListIndex = listIndex + delta;
            if (listIndex < 0 || newListIndex < 0 || newListIndex >= _contentBranchCards.Count) return;

            Undo.RegisterCompleteObjectUndo(BoundMission, "Move content branch");
            int dataIndex = list.IndexOf(card.Branch);
            if (dataIndex < 0) return;
            list.RemoveAt(dataIndex);
            list.Insert(dataIndex + delta, card.Branch);
            EditorUtility.SetDirty(BoundMission);

            int cardIdx = _bottomRow.IndexOf(card);
            _bottomRow.Remove(card);
            _bottomRow.Insert(cardIdx + delta, card);

            _contentBranchCards.RemoveAt(listIndex);
            _contentBranchCards.Insert(newListIndex, card);

            RefreshContentBranchMoveButtonStates();
            ModelChanged?.Invoke();
        }

        // Helpers.

        private int IndexOfStageById(int stageId)
        {
            var stages = BoundMission?.missionData?.missionStages;
            if (stages == null) return -1;
            for (int i = 0; i < stages.Count; i++)
            {
                if (stages[i].StageID == stageId) return i;
            }
            return -1;
        }

        private List<MissionBranch> CollectBranchesTargeting(int stageId)
        {
            var result = new List<MissionBranch>();
            var data = BoundMission.missionData;
            foreach (var s in data.missionStages)
            {
                if (s.branches == null) continue;
                foreach (var b in s.branches)
                {
                    if (b.TargetStage == stageId) result.Add(b);
                }
            }
            foreach (var b in data.ExceptionBranches)
            {
                if (b.TargetStage == stageId) result.Add(b);
            }
            foreach (var b in data.PreRequisiteBranches)
            {
                if (b.TargetStage == stageId) result.Add(b);
            }
            return result;
        }

        private void RemoveBranchesFromRuntime(IEnumerable<MissionBranch> branches)
        {
            var data = BoundMission.missionData;
            var set = new HashSet<MissionBranch>(branches);
            foreach (var s in data.missionStages)
            {
                s.branches?.RemoveAll(b => set.Contains(b));
            }
            data.ExceptionBranches.RemoveAll(b => set.Contains(b));
            data.PreRequisiteBranches.RemoveAll(b => set.Contains(b));
        }

        private void RemoveCardsForBranches(IEnumerable<MissionBranch> branches)
        {
            var set = new HashSet<MissionBranch>(branches);
            if (_missionCard != null)
            {
                _missionCard.ExceptionSection?.RemoveCardsForBranches(set);
                _missionCard.PrerequisiteSection?.RemoveCardsForBranches(set);
            }
            foreach (var card in _stageCards)
            {
                card.BranchSection?.RemoveCardsForBranches(set);
            }
        }

        private IEnumerable<BranchCard> CollectAllBranchCards()
        {
            if (_missionCard != null)
            {
                if (_missionCard.ExceptionSection != null)
                {
                    foreach (var c in _missionCard.ExceptionSection.Cards) yield return c;
                }
                if (_missionCard.PrerequisiteSection != null)
                {
                    foreach (var c in _missionCard.PrerequisiteSection.Cards) yield return c;
                }
            }
            foreach (var card in _stageCards)
            {
                if (card.BranchSection != null)
                {
                    foreach (var c in card.BranchSection.Cards) yield return c;
                }
            }
        }

        private void RefreshAllBranchCardDisplays()
        {
            foreach (var card in CollectAllBranchCards()) card.RefreshTargetDisplay();
        }

        private static Label MakeArrow()
        {
            var arrow = new Label("→");
            arrow.AddToClassList("stage-strip-arrow");
            return arrow;
        }
    }
}
