using System;
using KSP.Game.Missions;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.MissionAuthoring.Conditions.Rows
{
    /// <summary>
    /// Fallback row for a <see cref="Condition" /> subclass the editor does not know how to render.
    /// </summary>
    /// <remarks>
    /// Modder-defined types land here until a <c>[ConditionNodeView]</c> opt-in mechanism is added.
    /// </remarks>
    public sealed class UnknownConditionRow : ConditionRowBase
    {
        /// <summary>
        /// Constructs the fallback row for an unrecognized condition subclass.
        /// </summary>
        /// <param name="mission">The mission asset that owns the condition, used as the Undo target.</param>
        /// <param name="condition">The unrecognized condition instance.</param>
        /// <param name="replace">Callback invoked to swap the condition with another instance or null to delete.</param>
        /// <param name="notifyChanged">Callback invoked when the row mutates its condition.</param>
        /// <param name="moveUp">Callback that moves this row up in its parent's child list, or null when reorder is not available.</param>
        /// <param name="moveDown">Callback that moves this row down in its parent's child list, or null when reorder is not available.</param>
        public UnknownConditionRow(Mission mission, Condition condition, Action<Condition> replace, Action notifyChanged, Action moveUp = null, Action moveDown = null)
            : base(mission, condition, replace, notifyChanged, moveUp, moveDown)
        {
            AddToClassList("condition-row-unknown");
            string typeName = condition?.GetType().FullName ?? "(null)";
            Add(new Label($"Unrecognized Condition type: {typeName}"));
        }
    }
}
