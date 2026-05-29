using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using KSP.Messages;
using Ksp2UnityTools.Editor.Reflection;
using Redux.Missions;

namespace Ksp2UnityTools.Editor.MissionAuthoring.Conditions
{
    /// <summary>
    /// Catalog entry for one pickable <see cref="MessageCenterMessage" /> subclass.
    /// </summary>
    public sealed class MessageCenterMessageCatalogEntry
    {
        /// <summary>The concrete message type the picker resolves to.</summary>
        public Type MessageType { get; }

        /// <summary>The display name (class name).</summary>
        public string DisplayName { get; }

        /// <summary>The category bucket from <see cref="MessageInfo.Category" />. Defaults to "Uncategorized".</summary>
        public string Category { get; }

        /// <summary>One-line description shown in the picker.</summary>
        public string Description { get; }

        /// <summary>Author-facing label for the InputString filter on the EventCondition row. Null when the message publisher does not populate <c>InputStringList</c>. Hides the filter field entirely.</summary>
        public string InputFilterHint { get; }

        /// <summary>The assembly-qualified type name written into <c>EventCondition.EventTypeAQN</c>.</summary>
        public string AssemblyQualifiedName { get; }

        internal MessageCenterMessageCatalogEntry(Type messageType, string displayName, string category, string description, string inputFilterHint)
        {
            MessageType = messageType;
            DisplayName = displayName;
            Category = category;
            Description = description;
            InputFilterHint = inputFilterHint;
            AssemblyQualifiedName = messageType.AssemblyQualifiedName;
        }
    }

    /// <summary>
    /// Builds the catalog of pickable <see cref="MessageCenterMessage" /> types for the EventCondition message picker.
    /// </summary>
    /// <remarks>
    /// Excludes abstract types.
    /// </remarks>
    public static class MessageCenterMessageCatalog
    {
        private const string UNCATEGORIZED = "Uncategorized";

        private static IReadOnlyList<MessageCenterMessageCatalogEntry> _cached;
        private static Dictionary<string, MessageCenterMessageCatalogEntry> _byAqn;

        /// <summary>
        /// Returns the full catalog, building it lazily on first call.
        /// </summary>
        /// <returns>The catalog entries sorted alphabetically by display name.</returns>
        public static IReadOnlyList<MessageCenterMessageCatalogEntry> GetEntries() => _cached ??= Build();

        /// <summary>
        /// Returns entries grouped by Category, sorted alphabetically with "Uncategorized" last.
        /// </summary>
        /// <returns>The catalog entries grouped by category.</returns>
        public static IReadOnlyList<IGrouping<string, MessageCenterMessageCatalogEntry>> GetEntriesByCategory()
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
        public static MessageCenterMessageCatalogEntry FindByAqn(string aqn)
        {
            if (string.IsNullOrEmpty(aqn)) return null;
            _byAqn ??= GetEntries().ToDictionary(e => AssemblyQualifiedNameUtil.Normalize(e.AssemblyQualifiedName));
            return _byAqn.TryGetValue(AssemblyQualifiedNameUtil.Normalize(aqn), out var entry) ? entry : null;
        }

        private static IReadOnlyList<MessageCenterMessageCatalogEntry> Build()
        {
            var entries = new List<MessageCenterMessageCatalogEntry>();
            foreach (var type in ReduxTypeCache.GetTypesDerivedFrom<MessageCenterMessage>())
            {
                if (type.IsAbstract) continue;

                var info = type.GetCustomAttribute<MessageInfo>();
                var category = !string.IsNullOrEmpty(info?.Category) ? info.Category : UNCATEGORIZED;
                var description = info?.Description ?? string.Empty;
                var inputFilterHint = info?.InputFilterHint;

                entries.Add(new MessageCenterMessageCatalogEntry(type, type.Name, category, description, inputFilterHint));
            }
            return entries.OrderBy(e => e.DisplayName, StringComparer.OrdinalIgnoreCase).ToList();
        }
    }
}
