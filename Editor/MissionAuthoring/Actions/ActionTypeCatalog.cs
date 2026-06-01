using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using KSP.Game.Missions;
using Ksp2UnityTools.Editor.Reflection;
using Redux.Missions;

namespace Ksp2UnityTools.Editor.MissionAuthoring.Actions
{
    /// <summary>
    /// Catalog entry for one pickable <see cref="IMissionAction" /> implementation.
    /// </summary>
    public sealed class ActionTypeCatalogEntry
    {
        /// <summary>
        /// Gets the concrete action type the picker resolves to.
        /// </summary>
        public Type ActionType { get; }

        /// <summary>
        /// Gets the display name from <see cref="ActionInfo.DisplayName" /> or the type name.
        /// </summary>
        public string DisplayName { get; }

        /// <summary>
        /// Gets the category bucket from <see cref="ActionInfo.Category" />. Defaults to "Uncategorized".
        /// </summary>
        public string Category { get; }

        /// <summary>
        /// Gets the one-line description from <see cref="ActionInfo.Description" /> or the runtime <c>GetEditorDescription()</c> fallback.
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// Gets the factory that produces a fresh instance of this action.
        /// </summary>
        public Func<IMissionAction> Create { get; }

        /// <summary>
        /// Creates a new <see cref="ActionTypeCatalogEntry" /> with the supplied metadata and factory.
        /// </summary>
        /// <param name="actionType">The concrete action type the picker resolves to.</param>
        /// <param name="displayName">The display name shown in the picker.</param>
        /// <param name="category">The category bucket used to group the entry.</param>
        /// <param name="description">The one-line description shown in the picker.</param>
        /// <param name="create">The factory invoked to produce a fresh action instance.</param>
        internal ActionTypeCatalogEntry(Type actionType, string displayName, string category, string description, Func<IMissionAction> create)
        {
            ActionType = actionType;
            DisplayName = displayName;
            Category = category;
            Description = description;
            Create = create;
        }
    }

    /// <summary>
    /// Builds the catalog of pickable <see cref="IMissionAction" /> implementations.
    /// </summary>
    /// <remarks>
    /// Enumerates non-abstract types derived from <see cref="MissionActionBase" /> that implement <see cref="IMissionAction" />.
    /// </remarks>
    public static class ActionTypeCatalog
    {
        private const string UNCATEGORIZED = "Uncategorized";

        private static IReadOnlyList<ActionTypeCatalogEntry> _cached;

        /// <summary>
        /// Returns the cached catalog entries, building it on first call.
        /// </summary>
        /// <returns>The catalog entries in display-name order.</returns>
        public static IReadOnlyList<ActionTypeCatalogEntry> GetEntries() => _cached ??= Build();

        /// <summary>
        /// Returns the catalog entries grouped by category, with "Uncategorized" sorted last.
        /// </summary>
        /// <returns>The catalog entries grouped by their <see cref="ActionTypeCatalogEntry.Category" /> value.</returns>
        public static IReadOnlyList<IGrouping<string, ActionTypeCatalogEntry>> GetEntriesByCategory()
        {
            return GetEntries()
                .GroupBy(e => e.Category)
                .OrderBy(g => g.Key == UNCATEGORIZED ? 1 : 0)
                .ThenBy(g => g.Key)
                .ToList();
        }

        /// <summary>
        /// Looks up the catalog entry whose <see cref="ActionTypeCatalogEntry.ActionType" /> matches <paramref name="actionType" />.
        /// </summary>
        /// <param name="actionType">The concrete action type to look up.</param>
        /// <returns>The matching entry, or null if no entry was found or <paramref name="actionType" /> is null.</returns>
        public static ActionTypeCatalogEntry FindByType(Type actionType)
        {
            if (actionType == null) return null;
            foreach (var entry in GetEntries())
            {
                if (entry.ActionType == actionType) return entry;
            }
            return null;
        }

        private static IReadOnlyList<ActionTypeCatalogEntry> Build()
        {
            var entries = new List<ActionTypeCatalogEntry>();
            foreach (var type in ReduxTypeCache.GetTypesDerivedFrom<MissionActionBase>())
            {
                if (type.IsAbstract) continue;
                if (!typeof(IMissionAction).IsAssignableFrom(type)) continue;

                var info = type.GetCustomAttribute<ActionInfo>();
                var category = !string.IsNullOrEmpty(info?.Category) ? info.Category : UNCATEGORIZED;
                var displayName = !string.IsNullOrEmpty(info?.DisplayName) ? info.DisplayName : type.Name;
                var description = !string.IsNullOrEmpty(info?.Description) ? info.Description : ResolveRuntimeDescription(type);

                Func<IMissionAction> create = () =>
                {
                    try { return (IMissionAction)Activator.CreateInstance(type); }
                    catch { return null; }
                };

                entries.Add(new ActionTypeCatalogEntry(type, displayName, category, description, create));
            }
            return entries.OrderBy(e => e.DisplayName, StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static string ResolveRuntimeDescription(Type type)
        {
            try
            {
                var instance = Activator.CreateInstance(type) as IMissionAction;
                return instance?.GetEditorDescription() ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
