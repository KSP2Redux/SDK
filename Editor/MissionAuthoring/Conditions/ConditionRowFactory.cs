using System;
using KSP.Game.Missions;
using Ksp2UnityTools.Editor.MissionAuthoring.Conditions.Rows;

namespace Ksp2UnityTools.Editor.MissionAuthoring.Conditions
{
    /// <summary>
    /// Dispatches from a runtime <see cref="Condition" /> subclass to the matching row VisualElement.
    /// </summary>
    /// <remarks>
    /// Unknown subclasses fall back to <see cref="UnknownConditionRow" />.
    /// </remarks>
    internal static class ConditionRowFactory
    {
        /// <summary>
        /// Builds the row VisualElement for a condition based on its concrete subclass.
        /// </summary>
        /// <param name="mission">The mission asset that owns the condition tree, used as the Undo target.</param>
        /// <param name="condition">The condition instance the row will edit.</param>
        /// <param name="replace">Callback invoked to swap the condition with another instance or null to delete.</param>
        /// <param name="notifyChanged">Callback invoked when the row mutates its condition.</param>
        /// <param name="moveUp">Callback that moves this row up in its parent's child list, or null when reorder is not available.</param>
        /// <param name="moveDown">Callback that moves this row down in its parent's child list, or null when reorder is not available.</param>
        /// <returns>The row VisualElement for the condition, or an <see cref="UnknownConditionRow" /> if the subclass is not recognized.</returns>
        public static ConditionRowBase Build(
            Mission mission,
            Condition condition,
            Action<Condition> replace,
            Action notifyChanged,
            Action moveUp = null,
            Action moveDown = null)
        {
            return condition switch
            {
                PropertyCondition pc => new PropertyConditionRow(mission, pc, replace, notifyChanged, moveUp, moveDown),
                EventCondition ec => new EventConditionRow(mission, ec, replace, notifyChanged, moveUp, moveDown),
                ConditionSet cs => new ConditionSetRow(mission, cs, replace, notifyChanged, moveUp, moveDown),
                ScriptCondition sc => new ScriptConditionRow(mission, sc, replace, notifyChanged, moveUp, moveDown),
                _ => new UnknownConditionRow(mission, condition, replace, notifyChanged, moveUp, moveDown),
            };
        }
    }
}
