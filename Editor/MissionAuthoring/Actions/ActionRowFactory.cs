using System;
using System.Collections.Generic;
using System.Reflection;
using KSP.Game.Missions;
using Ksp2UnityTools.Editor.MissionAuthoring.Actions.Rows;
using Ksp2UnityTools.Editor.Reflection;

namespace Ksp2UnityTools.Editor.MissionAuthoring.Actions
{
    /// <summary>
    /// Dispatches a runtime <see cref="IMissionAction" /> to its custom row class.
    /// </summary>
    /// <remarks>
    /// Custom rows opt in by carrying <see cref="CustomActionRowAttribute" /> on their type. Falls back to <see cref="GenericActionRow" /> when no custom row is registered for the action's concrete type.
    /// </remarks>
    internal static class ActionRowFactory
    {
        private static Dictionary<Type, Type> _rowByActionType;

        /// <summary>
        /// Builds the row that edits <paramref name="action" />, picking the registered custom row class when present and falling back to <see cref="GenericActionRow" /> otherwise.
        /// </summary>
        /// <param name="mission">The mission asset that owns the action.</param>
        /// <param name="action">The action instance to edit.</param>
        /// <param name="replace">Callback to swap the action with another or null.</param>
        /// <param name="notifyChanged">Callback fired when the action's state changes.</param>
        /// <param name="moveUp">Optional callback to reorder this row up within its parent list.</param>
        /// <param name="moveDown">Optional callback to reorder this row down within its parent list.</param>
        /// <returns>The row instance ready to be added to the parent container.</returns>
        public static ActionRowBase Build(
            Mission mission,
            IMissionAction action,
            System.Action<IMissionAction> replace,
            System.Action notifyChanged,
            System.Action moveUp = null,
            System.Action moveDown = null)
        {
            EnsureCache();
            if (action != null && _rowByActionType.TryGetValue(action.GetType(), out var rowType))
            {
                try
                {
                    return (ActionRowBase)Activator.CreateInstance(rowType, mission, action, replace, notifyChanged, moveUp, moveDown);
                }
                catch
                {
                    // Fall through to generic row on instantiation failure (missing ctor signature, etc.).
                }
            }
            return new GenericActionRow(mission, action, replace, notifyChanged, moveUp, moveDown);
        }

        private static void EnsureCache()
        {
            if (_rowByActionType != null) return;
            _rowByActionType = new Dictionary<Type, Type>();
            foreach (var rowType in ReduxTypeCache.GetTypesDerivedFrom<ActionRowBase>())
            {
                if (rowType.IsAbstract) continue;
                var attr = rowType.GetCustomAttribute<CustomActionRowAttribute>();
                if (attr?.ActionType == null) continue;
                _rowByActionType[attr.ActionType] = rowType;
            }
        }
    }
}
