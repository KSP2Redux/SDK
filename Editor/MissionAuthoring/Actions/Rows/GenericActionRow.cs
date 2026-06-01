using System;
using System.Collections.Generic;
using System.Reflection;
using KSP.Game.Missions;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.MissionAuthoring.Actions.Rows
{
    /// <summary>
    /// Reflection-based row for any <see cref="IMissionAction" /> type that lacks a custom row registered via <see cref="CustomActionRowAttribute" />.
    /// </summary>
    /// <remarks>
    /// Walks the action's serializable public fields (anything not tagged <c>[JsonIgnore]</c> or <c>[NonSerialized]</c>) and produces a basic widget per scalar type via <see cref="ActionRowBase.BuildScalarField" />. Complex fields render a placeholder hint and are deferred to per-type custom rows.
    /// </remarks>
    public sealed class GenericActionRow : ActionRowBase
    {
        /// <summary>
        /// Creates a new <see cref="GenericActionRow" /> bound to the supplied action and populates its body via reflection.
        /// </summary>
        /// <param name="mission">The mission asset that owns the action.</param>
        /// <param name="action">The action instance to edit.</param>
        /// <param name="replace">Callback to swap the action with another or null.</param>
        /// <param name="notifyChanged">Callback fired when the action's state changes.</param>
        /// <param name="moveUp">Optional callback to reorder this row up within its parent list.</param>
        /// <param name="moveDown">Optional callback to reorder this row down within its parent list.</param>
        public GenericActionRow(Mission mission, IMissionAction action, System.Action<IMissionAction> replace, System.Action notifyChanged, System.Action moveUp = null, System.Action moveDown = null)
            : base(mission, action, replace, notifyChanged, moveUp, moveDown)
        {
            var body = BuildCard();
            BuildFields(body);
        }

        private void BuildFields(VisualElement body)
        {
            if (Action == null) return;
            BuildFieldsBySection(body, GetSerializedFields(Action.GetType()));
        }
    }
}
