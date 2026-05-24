using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Ksp2UnityTools.Editor.Reflection;
using VSwift.Modules.Transformers;

namespace Ksp2UnityTools.Editor.PartAuthoring.Inspectors.Picker
{
    /// <summary>
    /// Catalog entry for a single <see cref="ITransformer" /> concrete type available in the "Add Transformer" picker.
    /// </summary>
    public sealed class TransformerCatalogEntry
    {
        /// <summary>The concrete transformer type that <c>Activator.CreateInstance</c> would produce.</summary>
        public Type TransformerType { get; }
        /// <summary>The display name (the transformer's C# class name).</summary>
        public string DisplayName { get; }
        /// <summary>The category bucket, from <c>[TransformerCategory]</c>. Defaults to "Uncategorized".</summary>
        public string Category { get; }
        /// <summary>The one-line description, from <c>[TransformerDescription]</c>. May be empty.</summary>
        public string Description { get; }

        internal TransformerCatalogEntry(Type transformerType, string displayName, string category, string description)
        {
            TransformerType = transformerType;
            DisplayName = displayName;
            Category = category;
            Description = description;
        }
    }

    /// <summary>
    /// Builds the catalog of pickable <see cref="ITransformer" /> concrete types for the "Add Transformer" picker.
    /// </summary>
    /// <remarks>
    /// Uses <see cref="ReduxTypeCache" /> for type discovery rather than Unity's <c>UnityEditor.TypeCache</c> because the SDK ships into ThunderKit-imported KSP2 contexts where the latter is unreliable. Mirrors the shape of <see cref="PartModuleCatalog" />.
    /// </remarks>
    public static class TransformerCatalog
    {
        private const string UNCATEGORIZED = "Uncategorized";

        private static IReadOnlyList<TransformerCatalogEntry> _cached;

        /// <summary>
        /// Returns the catalog, building it lazily on first call.
        /// </summary>
        public static IReadOnlyList<TransformerCatalogEntry> GetEntries()
        {
            return _cached ??= Build();
        }

        /// <summary>
        /// Returns the catalog grouped by category, with categories sorted alphabetically except "Uncategorized" which sorts last.
        /// </summary>
        public static IReadOnlyList<IGrouping<string, TransformerCatalogEntry>> GetEntriesByCategory()
        {
            return GetEntries()
                .GroupBy(e => e.Category)
                .OrderBy(g => g.Key == UNCATEGORIZED ? 1 : 0)
                .ThenBy(g => g.Key)
                .ToList();
        }

        /// <summary>
        /// Drops the cached catalog. Used by editor reload paths.
        /// </summary>
        public static void Invalidate()
        {
            _cached = null;
        }

        private static IReadOnlyList<TransformerCatalogEntry> Build()
        {
            return ReduxTypeCache.GetTypesWithAttribute<Transformer>()
                .Where(t => typeof(ITransformer).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface)
                .Select(t => new TransformerCatalogEntry(
                    transformerType: t,
                    displayName: t.Name,
                    category: t.GetCustomAttribute<TransformerCategory>()?.Category ?? UNCATEGORIZED,
                    description: t.GetCustomAttribute<TransformerDescription>()?.Description ?? string.Empty))
                .OrderBy(e => e.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }
}
