using System;
using System.Collections.Generic;
using KSP.Game.Missions;

namespace Ksp2UnityTools.Editor.MissionAuthoring.Conditions
{
    /// <summary>
    /// Catalog entry for one pickable <see cref="Condition" /> kind in the Add Condition dropdown.
    /// </summary>
    public sealed class ConditionTypeCatalogEntry
    {
        /// <summary>Display name shown in the dropdown.</summary>
        public string DisplayName { get; }

        /// <summary>One-line description for future tooltip surfaces.</summary>
        public string Description { get; }

        /// <summary>Concrete Condition type the entry resolves to.</summary>
        public Type ConditionType { get; }

        /// <summary>Factory delegate for a fresh instance of this Condition kind.</summary>
        public Func<Condition> Create { get; }

        internal ConditionTypeCatalogEntry(string displayName, string description, Type conditionType, Func<Condition> create)
        {
            DisplayName = displayName;
            Description = description;
            ConditionType = conditionType;
            Create = create;
        }
    }

    /// <summary>
    /// Static catalog of <see cref="Condition" /> kinds the editor can author.
    /// </summary>
    /// <remarks>
    /// Consumed by the Add Condition dropdown. Only one ConditionSet entry is exposed. The
    /// operator (AND, OR, XOR, NOT) is chosen on the ConditionSet row itself, not at
    /// instantiation time.
    /// </remarks>
    public static class ConditionTypeCatalog
    {
        private static readonly IReadOnlyList<ConditionTypeCatalogEntry> _entries = new[]
        {
            new ConditionTypeCatalogEntry(
                "PropertyCondition",
                "Polls a PropertyWatcher value against a threshold.",
                typeof(PropertyCondition),
                () => new PropertyCondition()),

            new ConditionTypeCatalogEntry(
                "EventCondition",
                "Fires when a MessageCenterMessage of the chosen type is published.",
                typeof(EventCondition),
                () => new EventCondition()),

            new ConditionTypeCatalogEntry(
                "ScriptCondition",
                "Lua callback. Runtime path is unimplemented; the node is authorable for forward compatibility.",
                typeof(ScriptCondition),
                () => new ScriptCondition()),

            new ConditionTypeCatalogEntry(
                "ConditionSet",
                "Logical AND / OR / XOR / NOT over child conditions. Operator chosen on the row.",
                typeof(ConditionSet),
                () => new ConditionSet { ConditionMode = LogicalOperator.AND }),
        };

        /// <summary>
        /// Returns the full catalog of authorable condition kinds in registration order.
        /// </summary>
        /// <returns>The catalog entries.</returns>
        public static IReadOnlyList<ConditionTypeCatalogEntry> GetEntries() => _entries;

        /// <summary>
        /// Looks up the catalog entry whose <see cref="ConditionTypeCatalogEntry.ConditionType" /> matches the supplied runtime type.
        /// </summary>
        /// <param name="conditionType">The concrete condition type to resolve.</param>
        /// <returns>The matching catalog entry, or null if no entry is registered for the type.</returns>
        public static ConditionTypeCatalogEntry FindByType(Type conditionType)
        {
            foreach (var entry in _entries)
            {
                if (entry.ConditionType == conditionType) return entry;
            }
            return null;
        }
    }
}
