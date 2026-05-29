using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using KSP.Game.Missions.Definitions;
using KSP.Messages;
using KSP.Messages.PropertyWatchers;
using Ksp2UnityTools.Editor.Reflection;
using Redux.Missions;

namespace Ksp2UnityTools.Editor.MissionAuthoring.Conditions
{
    /// <summary>
    /// Catalog entry for one pickable <see cref="PropertyWatcher" /> subclass.
    /// </summary>
    public sealed class PropertyWatcherCatalogEntry
    {
        /// <summary>The concrete watcher type the picker resolves to.</summary>
        public Type WatcherType { get; }

        /// <summary>The display name (class name with PropertyWatcher suffix stripped).</summary>
        public string DisplayName { get; }

        /// <summary>The category bucket from <see cref="PropertyWatcherInfo.Category" />. Defaults to "Uncategorized".</summary>
        public string Category { get; }

        /// <summary>The one-line description from <see cref="PropertyWatcherInfo.Description" />. May be empty.</summary>
        public string Description { get; }

        /// <summary>The explicit output type from <see cref="PropertyWatcherInfo.OutputType" />, or the runtime <c>baseType()</c> fallback. Drives threshold field rendering and operator gating.</summary>
        public Type OutputType { get; }

        /// <summary>True when the watcher class declares any <c>GetValueX(string)</c> or <c>GetValueX(MissionData, string)</c> override.</summary>
        public bool TakesInput { get; }

        /// <summary>Author-facing label for the input string field on the condition row. Falls back to a generic "input" when <see cref="PropertyWatcherInfo.InputDescription" /> is unset and the watcher takes input.</summary>
        public string InputDescription { get; }

        /// <summary>Unit suffix for the threshold field's label. Null when no units declared.</summary>
        public string Units { get; }

        /// <summary>The assembly-qualified type name written into <c>PropertyCondition.PropertyTypeAQN</c>.</summary>
        public string AssemblyQualifiedName { get; }

        internal PropertyWatcherCatalogEntry(Type watcherType, string displayName, string category, string description, Type outputType, bool takesInput, string inputDescription, string units)
        {
            WatcherType = watcherType;
            DisplayName = displayName;
            Category = category;
            Description = description;
            OutputType = outputType;
            TakesInput = takesInput;
            InputDescription = inputDescription;
            Units = units;
            AssemblyQualifiedName = watcherType.AssemblyQualifiedName;
        }
    }

    /// <summary>
    /// Builds the catalog of pickable <see cref="PropertyWatcher" /> types for the PropertyCondition watcher picker.
    /// </summary>
    /// <remarks>
    /// Only watchers carrying <see cref="DiscoverableProperty" /> are exposed.
    /// </remarks>
    public static class PropertyWatcherCatalog
    {
        private const string WATCHER_SUFFIX = "PropertyWatcher";
        private const string UNCATEGORIZED = "Uncategorized";

        private static IReadOnlyList<PropertyWatcherCatalogEntry> _cached;
        private static Dictionary<string, PropertyWatcherCatalogEntry> _byAqn;

        /// <summary>Returns the full catalog, building it lazily on first call.</summary>
        /// <returns>The catalog entries sorted alphabetically by display name.</returns>
        public static IReadOnlyList<PropertyWatcherCatalogEntry> GetEntries() => _cached ??= Build();

        /// <summary>Returns entries grouped by Category, sorted alphabetically with "Uncategorized" last.</summary>
        /// <returns>The catalog entries grouped by category.</returns>
        public static IReadOnlyList<IGrouping<string, PropertyWatcherCatalogEntry>> GetEntriesByCategory()
        {
            return GetEntries()
                .GroupBy(e => e.Category)
                .OrderBy(g => g.Key == UNCATEGORIZED ? 1 : 0)
                .ThenBy(g => g.Key)
                .ToList();
        }

        /// <summary>
        /// Looks up an entry by assembly-qualified type name.
        /// </summary>
        /// <remarks>
        /// Matching is normalized to <c>FullName, AssemblyName</c> so version, culture, and token drift between authored assets and the current build does not break the lookup.
        /// </remarks>
        /// <param name="aqn">The assembly-qualified type name to resolve.</param>
        /// <returns>The matching catalog entry, or null when no match exists or the input is null or empty.</returns>
        public static PropertyWatcherCatalogEntry FindByAqn(string aqn)
        {
            if (string.IsNullOrEmpty(aqn)) return null;
            _byAqn ??= GetEntries().ToDictionary(e => AssemblyQualifiedNameUtil.Normalize(e.AssemblyQualifiedName));
            return _byAqn.TryGetValue(AssemblyQualifiedNameUtil.Normalize(aqn), out var entry) ? entry : null;
        }

        private static IReadOnlyList<PropertyWatcherCatalogEntry> Build()
        {
            var entries = new List<PropertyWatcherCatalogEntry>();
            foreach (var type in ReduxTypeCache.GetTypesDerivedFrom<PropertyWatcher>())
            {
                if (type.IsAbstract) continue;
                if (type.GetCustomAttribute<DiscoverableProperty>() == null) continue;

                var info = type.GetCustomAttribute<PropertyWatcherInfo>();
                var displayName = StripWatcherSuffix(type.Name);
                var category = !string.IsNullOrEmpty(info?.Category) ? info.Category : UNCATEGORIZED;
                var description = info?.Description ?? string.Empty;
                var outputType = info?.OutputType ?? ResolveBaseType(type);
                var takesInput = DetectsInputOverride(type);
                var inputDescription = info?.InputDescription;
                if (string.IsNullOrEmpty(inputDescription) && takesInput) inputDescription = "input";
                var units = info?.Units;

                entries.Add(new PropertyWatcherCatalogEntry(type, displayName, category, description, outputType, takesInput, inputDescription, units));
            }
            return entries.OrderBy(e => e.DisplayName, StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static string StripWatcherSuffix(string typeName)
        {
            return typeName.EndsWith(WATCHER_SUFFIX) ? typeName.Substring(0, typeName.Length - WATCHER_SUFFIX.Length) : typeName;
        }

        private static Type ResolveBaseType(Type watcherType)
        {
            try
            {
                var instance = Activator.CreateInstance(watcherType) as PropertyWatcher;
                return instance?.baseType() ?? typeof(double);
            }
            catch
            {
                return typeof(double);
            }
        }

        private static bool DetectsInputOverride(Type watcherType)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly;
            foreach (var method in watcherType.GetMethods(flags))
            {
                if (!method.IsVirtual) continue;
                if (method.Name != "GetValueString" && method.Name != "GetValueBool" && method.Name != "GetValueInt" && method.Name != "GetValueDouble") continue;
                var parameters = method.GetParameters();
                if (parameters.Length == 0) continue;
                if (parameters.Any(p => p.ParameterType == typeof(string))) return true;
            }
            return false;
        }
    }
}
