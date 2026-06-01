using System;
using KSP.Game.Missions;
using UnityEditor;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.MissionAuthoring.Conditions
{
    /// <summary>
    /// Inline editor for a <see cref="Condition" /> tree mounted in a card body.
    /// </summary>
    /// <remarks>
    /// Edits either a stage's spine condition or a branch's condition via the supplied getter
    /// and setter pair. Empty state shows a "(no condition)" hint with an Add button. Reused
    /// across stage cards and branch cards. The getter and setter pair decouples the editor
    /// from the owning model type, so the same component works for <c>MissionStage.condition</c>
    /// and <c>MissionBranch.condition</c> without referencing either type directly.
    /// </remarks>
    public class ConditionTreeEditor : VisualElement
    {
        private readonly Mission _mission;
        private readonly string _undoLabel;
        private readonly Func<Condition> _getRoot;
        private readonly Action<Condition> _setRoot;

        private readonly Button _addButton;
        private readonly VisualElement _content;

        /// <summary>Fires after the root condition is added, replaced, or removed.</summary>
        public event Action RootChanged;

        /// <summary>
        /// Constructs the inline condition tree editor for the supplied root accessor.
        /// </summary>
        /// <param name="mission">The mission asset that owns the condition, used as the Undo target.</param>
        /// <param name="undoLabel">The label recorded with the root-level Undo entries.</param>
        /// <param name="getRoot">Callback that returns the current root condition, or null when none is assigned.</param>
        /// <param name="setRoot">Callback that writes a new root condition (or null) back to the owning model.</param>
        public ConditionTreeEditor(Mission mission, string undoLabel, Func<Condition> getRoot, Action<Condition> setRoot)
        {
            _mission = mission;
            _undoLabel = undoLabel;
            _getRoot = getRoot;
            _setRoot = setRoot;
            AddToClassList("condition-tree-editor");

            var header = new VisualElement();
            header.AddToClassList("condition-tree-header");

            var title = new Label("Condition");
            title.AddToClassList("condition-tree-title");
            header.Add(title);

            _addButton = new Button(OnAddClicked) { text = "+", tooltip = "Add condition" };
            _addButton.AddToClassList("condition-tree-add-btn");
            header.Add(_addButton);

            Add(header);

            _content = new VisualElement();
            _content.AddToClassList("condition-tree-content");
            Add(_content);

            Undo.undoRedoPerformed += OnUndoRedo;
            RegisterCallback<DetachFromPanelEvent>(_ => Undo.undoRedoPerformed -= OnUndoRedo);

            Refresh();
        }

        private void OnUndoRedo()
        {
            Refresh();
            RootChanged?.Invoke();
        }

        /// <summary>Rebuilds the tree UI from the current root. Called on construction and on Reconcile.</summary>
        public void Refresh()
        {
            _content.Clear();
            var root = _getRoot?.Invoke();
            if (root == null)
            {
                var hint = new Label("(none)");
                hint.AddToClassList("condition-tree-empty-hint");
                _content.Add(hint);
                _addButton.style.display = DisplayStyle.Flex;
                return;
            }

            var row = ConditionRowFactory.Build(
                _mission,
                root,
                replace: newRoot => ReplaceRoot(newRoot),
                notifyChanged: () => RootChanged?.Invoke());
            _content.Add(row);
            _addButton.style.display = DisplayStyle.None;
        }

        private void OnAddClicked()
        {
            var menu = new GenericDropdownMenu();
            foreach (var entry in ConditionTypeCatalog.GetEntries())
            {
                menu.AddItem(entry.DisplayName, false, () =>
                {
                    if (entry.Create == null) return;
                    ReplaceRoot(entry.Create());
                });
            }
            menu.DropDown(_addButton.worldBound, _addButton, anchored: false);
        }

        private void ReplaceRoot(Condition newRoot)
        {
            Undo.RecordObject(_mission, _undoLabel);
            _setRoot?.Invoke(newRoot);
            EditorUtility.SetDirty(_mission);
            Refresh();
            RootChanged?.Invoke();
        }
    }
}
