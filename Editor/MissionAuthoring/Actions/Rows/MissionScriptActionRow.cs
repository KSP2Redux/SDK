using System;
using System.Reflection;
using KSP.Game.Missions;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.MissionAuthoring.Actions.Rows
{
    /// <summary>
    /// Custom row for <see cref="MissionScriptAction" />.
    /// </summary>
    /// <remarks>
    /// Surfaces the <c>luascript</c> reference plus the inherited <see cref="MissionActionBase.StageEvent" />.
    /// </remarks>
    [CustomActionRow(typeof(MissionScriptAction))]
    public sealed class MissionScriptActionRow : ActionRowBase
    {
        /// <summary>
        /// Creates a new <see cref="MissionScriptActionRow" /> bound to the supplied action and populates its body with the script and stage-event fields.
        /// </summary>
        /// <param name="mission">The mission asset that owns the action.</param>
        /// <param name="action">The action instance to edit.</param>
        /// <param name="replace">Callback to swap the action with another or null.</param>
        /// <param name="notifyChanged">Callback fired when the action's state changes.</param>
        /// <param name="moveUp">Optional callback to reorder this row up within its parent list.</param>
        /// <param name="moveDown">Optional callback to reorder this row down within its parent list.</param>
        public MissionScriptActionRow(Mission mission, IMissionAction action, System.Action<IMissionAction> replace, System.Action notifyChanged, System.Action moveUp = null, System.Action moveDown = null)
            : base(mission, action, replace, notifyChanged, moveUp, moveDown)
        {
            var body = BuildCard();

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public;
            var luaField = typeof(MissionScriptAction).GetField("luascript", flags);
            if (luaField != null) BuildScalarField(body, luaField);

            BuildStageEventField(body);
        }
    }
}
