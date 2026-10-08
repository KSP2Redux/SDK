using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using KSP.Sim.Definitions;
using Ksp2UnityTools.Editor.Reflection;
using Redux.Modules.Attributes;

namespace Ksp2UnityTools.Editor.PartAuthoring.Inspectors.Picker
{
    /// <summary>
    /// Catalog entry for a single <c>Module_*</c> type available in the "Add Module" picker.
    /// </summary>
    public sealed class ModuleCatalogEntry
    {
        /// <summary>The concrete <c>Module_*</c> type that an <c>AddComponent</c> call would create.</summary>
        public Type ModuleType { get; }
        /// <summary>The display name (module type name with the <c>Module_</c> prefix stripped).</summary>
        public string DisplayName { get; }
        /// <summary>The category bucket, from <c>[ModuleCategory]</c>. Defaults to "Uncategorized".</summary>
        public string Category { get; }
        /// <summary>The one-line description, from <c>[ModuleDescription]</c>. May be empty.</summary>
        public string Description { get; }

        internal ModuleCatalogEntry(Type moduleType, string displayName, string category, string description)
        {
            ModuleType = moduleType;
            DisplayName = displayName;
            Category = category;
            Description = description;
        }
    }

    /// <summary>
    /// Builds the catalog of pickable <c>Module_*</c> types for the "Add Module" picker. Excludes
    /// modules tagged <see cref="ModuleHiddenAttribute" />.
    /// </summary>
    /// <remarks>
    /// Uses <see cref="ReduxTypeCache" /> for type discovery rather than Unity's
    /// <c>UnityEditor.TypeCache</c> because the SDK ships into ThunderKit-imported KSP2 contexts where
    /// the latter is unreliable.
    /// </remarks>
    public static class PartModuleCatalog
    {
        private const string MODULE_TYPE_PREFIX = "Module_";
        private const string UNCATEGORIZED = "Uncategorized";

        private static IReadOnlyList<ModuleCatalogEntry> _cached;

        /// <summary>
        /// Returns the catalog, building it lazily on first call.
        /// </summary>
        /// <remarks>
        /// Reuses the same instance for every subsequent call until the type cache is invalidated.
        /// </remarks>
        /// <returns>The cached catalog of pickable <c>Module_*</c> types.</returns>
        public static IReadOnlyList<ModuleCatalogEntry> GetEntries()
        {
            return _cached ??= Build();
        }

        /// <summary>
        /// Returns the catalog grouped by category, with categories sorted alphabetically except "Uncategorized" which sorts last.
        /// </summary>
        /// <returns>The catalog entries grouped and ordered by category.</returns>
        public static IReadOnlyList<IGrouping<string, ModuleCatalogEntry>> GetEntriesByCategory()
        {
            return GetEntries()
                .GroupBy(e => e.Category)
                .OrderBy(g => g.Key == UNCATEGORIZED ? 1 : 0)
                .ThenBy(g => g.Key)
                .ToList();
        }

        private static IReadOnlyList<ModuleCatalogEntry> Build()
        {
            return ReduxTypeCache.GetTypesDerivedFrom<PartBehaviourModule>()
                .Where(t => t.GetCustomAttribute<ModuleHiddenAttribute>() == null)
                .Select(t => new ModuleCatalogEntry(
                    moduleType: t,
                    displayName: GetDisplayName(t),
                    category: t.GetCustomAttribute<ModuleCategoryAttribute>()?.Category ?? UNCATEGORIZED,
                    description: t.GetCustomAttribute<ModuleDescriptionAttribute>()?.Description ?? string.Empty))
                .OrderBy(e => e.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static string GetDisplayName(Type moduleType)
        {
            var typeName = moduleType.Name;
            return typeName.StartsWith(MODULE_TYPE_PREFIX) ? typeName.Substring(MODULE_TYPE_PREFIX.Length) : typeName;
        }
    }
}
