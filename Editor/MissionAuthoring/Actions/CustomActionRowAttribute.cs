using System;

namespace Ksp2UnityTools.Editor.MissionAuthoring.Actions
{
    /// <summary>
    /// Marks an <see cref="Rows.ActionRowBase" /> subclass as the editor for a specific <see cref="KSP.Game.Missions.IMissionAction" /> implementation.
    /// </summary>
    /// <remarks>
    /// Picked up by <see cref="ActionRowFactory" /> via reflection.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class CustomActionRowAttribute : Attribute
    {
        /// <summary>
        /// Gets the concrete action type the decorated row class edits.
        /// </summary>
        public Type ActionType { get; }

        /// <summary>
        /// Creates a new <see cref="CustomActionRowAttribute" /> bound to the supplied action type.
        /// </summary>
        /// <param name="actionType">The concrete action type the decorated row class edits.</param>
        public CustomActionRowAttribute(Type actionType)
        {
            ActionType = actionType;
        }
    }
}
