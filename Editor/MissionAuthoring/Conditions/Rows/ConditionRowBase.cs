using System;
using KSP.Game.Missions;
using UnityEditor;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.MissionAuthoring.Conditions.Rows
{
    /// <summary>
    /// Shared chrome for every condition row.
    /// </summary>
    /// <remarks>
    /// Holds the row's backing <see cref="Condition" /> and the callbacks the row needs to
    /// mutate its place in the tree. <see cref="Replace" /> swaps the Condition with another
    /// or null. <see cref="NotifyChanged" /> asks the parent tree editor to refresh derived
    /// UI state. <see cref="MoveUp" /> and <see cref="MoveDown" /> are populated when the row
    /// is rendered as a child of a ConditionSet, and null otherwise.
    /// </remarks>
    public abstract class ConditionRowBase : VisualElement
    {
        /// <summary>Gets the mission asset that owns the condition, used as the Undo target.</summary>
        public Mission Mission { get; }

        /// <summary>Gets the condition instance edited by this row.</summary>
        public Condition Condition { get; }

        /// <summary>Gets the callback that swaps the row's condition with another instance, or null to delete it.</summary>
        protected Action<Condition> Replace { get; }

        /// <summary>Gets the callback that signals the parent tree editor to refresh derived UI state after a mutation.</summary>
        protected Action NotifyChanged { get; }

        /// <summary>Gets the callback that moves this row up in its parent's child list, or null when reorder is not available.</summary>
        protected Action MoveUp { get; }

        /// <summary>Gets the callback that moves this row down in its parent's child list, or null when reorder is not available.</summary>
        protected Action MoveDown { get; }

        /// <summary>
        /// Constructs the shared row chrome and wires the supplied callbacks.
        /// </summary>
        /// <param name="mission">The mission asset that owns the condition, used as the Undo target.</param>
        /// <param name="condition">The condition instance this row edits.</param>
        /// <param name="replace">Callback invoked to swap the condition with another instance or null to delete.</param>
        /// <param name="notifyChanged">Callback invoked when the row mutates its condition.</param>
        /// <param name="moveUp">Callback that moves this row up in its parent's child list, or null when reorder is not available.</param>
        /// <param name="moveDown">Callback that moves this row down in its parent's child list, or null when reorder is not available.</param>
        protected ConditionRowBase(Mission mission, Condition condition, Action<Condition> replace, Action notifyChanged, Action moveUp = null, Action moveDown = null)
        {
            Mission = mission;
            Condition = condition;
            Replace = replace;
            NotifyChanged = notifyChanged;
            MoveUp = moveUp;
            MoveDown = moveDown;
            AddToClassList("condition-row");
        }

        /// <summary>
        /// Adds the reorder (when applicable) and wrap buttons to the supplied header.
        /// </summary>
        /// <remarks>
        /// Subclasses call this before <see cref="BuildHeaderDeleteButton" /> so they can inject row-specific buttons in between.
        /// </remarks>
        /// <param name="header">The header VisualElement to append the buttons to.</param>
        protected void BuildHeaderReorderAndWrapButtons(VisualElement header)
        {
            if (MoveUp != null || MoveDown != null)
            {
                var upBtn = new Button(MoveUp ?? (() => { })) { text = "▲", tooltip = "Move up" };
                upBtn.AddToClassList("condition-row-reorder-btn");
                upBtn.SetEnabled(MoveUp != null);
                header.Add(upBtn);

                var downBtn = new Button(MoveDown ?? (() => { })) { text = "▼", tooltip = "Move down" };
                downBtn.AddToClassList("condition-row-reorder-btn");
                downBtn.SetEnabled(MoveDown != null);
                header.Add(downBtn);
            }

            var wrapBtn = new Button(Wrap) { text = "Wrap", tooltip = "Wrap in ConditionSet" };
            wrapBtn.AddToClassList("condition-row-wrap-btn");
            header.Add(wrapBtn);
        }

        /// <summary>Adds the delete (X) button as the rightmost element of the header.</summary>
        /// <param name="header">The header VisualElement to append the delete button to.</param>
        protected void BuildHeaderDeleteButton(VisualElement header)
        {
            var deleteBtn = new Button(() => Replace?.Invoke(null)) { text = "X", tooltip = "Delete condition" };
            deleteBtn.AddToClassList("condition-row-delete-btn");
            header.Add(deleteBtn);
        }

        private void Wrap()
        {
            if (Condition == null || Replace == null) return;
            Undo.RecordObject(Mission, "Wrap in ConditionSet");
            var wrapper = new ConditionSet { ConditionMode = LogicalOperator.AND };
            wrapper.Children.Add(Condition);
            Replace.Invoke(wrapper);
            EditorUtility.SetDirty(Mission);
        }
    }
}
