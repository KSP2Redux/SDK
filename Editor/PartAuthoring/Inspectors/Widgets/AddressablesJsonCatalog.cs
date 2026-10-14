using System;
using System.Collections.Generic;
using System.Linq;
using Ksp2UnityTools.Editor.API;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Ksp2UnityTools.Editor.PartAuthoring.Inspectors.Widgets
{
    /// <summary>
    /// Shared loader for catalogs that enumerate string identifiers from addressable <see cref="TextAsset" /> entries with a given label.
    /// </summary>
    /// <remarks>
    /// Loads each TextAsset, runs the caller-supplied extractor against its JSON, deduplicates, and returns the alphabetically-sorted list. Stock entries only appear in the runtime <see cref="Addressables" /> catalogs, since KSP2 is imported via ThunderKit, and the project's own only in its Addressables settings until they are built, so both are read.
    /// </remarks>
    internal static class AddressablesJsonCatalog
    {
        /// <summary>
        /// Loads every addressable TextAsset with the given label and returns the deduplicated, alphabetically-sorted list of non-empty extractor results.
        /// </summary>
        /// <param name="label">The Addressables label to query.</param>
        /// <param name="tryExtract">Callback that extracts the identifier string from a TextAsset's JSON, returning null or empty to skip the entry.</param>
        /// <returns>The deduplicated, alphabetically-sorted list of extracted identifiers.</returns>
        public static List<string> Build(string label, Func<string, string> tryExtract)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            void Add(string json)
            {
                string name = tryExtract(json);
                if (!string.IsNullOrEmpty(name))
                {
                    names.Add(name);
                }
            }

            // The project's own entries are not in any catalog until its Addressables are built
            foreach (string json in AddressablesTools.GetCatalogText(label).Concat(AddressablesTools.GetProjectText(label)))
            {
                Add(json);
            }

            return names.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
        }
    }
}
