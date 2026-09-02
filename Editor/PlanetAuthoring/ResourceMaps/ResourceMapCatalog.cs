using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.ResourceMaps
{
    /// <summary>
    /// Supplies the celestial body and resource names the Resource Maps window offers, read from
    /// what the project already declares rather than from a hand-maintained list.
    /// </summary>
    /// <remarks>
    /// Both names end up in file names and in the generated definition, where a typo produces a map
    /// the game silently never associates with anything. Offering the known names removes the most
    /// likely way to get that wrong.
    /// </remarks>
    public static class ResourceMapCatalog
    {
        /// <summary>
        /// Folder holding one JSON per known resource.
        /// </summary>
        public const string RESOURCE_DEFINITIONS_FOLDER = "Assets/ReduxAssets/Definitions/ResourceSystem/ResourceDefinitions";

        /// <summary>
        /// Localization table whose keys enumerate every celestial body.
        /// </summary>
        public const string CELESTIAL_BODY_LOCALIZATION = "Assets/ReduxAssets/Localizations/celestialbody_loc.csv";

        /// <summary>
        /// Entry shown at the end of a name dropdown to allow a name the project does not know yet.
        /// </summary>
        public const string OTHER_ENTRY = "Other...";

        private const string BODY_KEY_PREFIX = "CelestialBody/";

        // Keys under CelestialBody/ that name something other than a body an artist would author
        // resource maps for.
        private static readonly HashSet<string> NON_BODY_KEYS = new(StringComparer.OrdinalIgnoreCase)
        {
            "RaskRuskBarycenter",
            "Dresteroid01",
            "Dresteroid02",
        };

        // Suffixes a ripped biome mask's file name carries around its body name.
        private static readonly string[] MASK_NAME_SUFFIXES =
        {
            "_biome_submask",
            "_biome_mask",
            "_biomes",
            "_biome",
        };

        /// <summary>
        /// Reads every resource name the project defines.
        /// </summary>
        /// <returns>The resource names, sorted, or an empty list when the folder is absent.</returns>
        public static List<string> GetResourceNames()
        {
            var names = new List<string>();
            if (!Directory.Exists(RESOURCE_DEFINITIONS_FOLDER))
                return names;

            foreach (string path in Directory.GetFiles(RESOURCE_DEFINITIONS_FOLDER, "*.json", SearchOption.TopDirectoryOnly))
            {
                names.Add(Path.GetFileNameWithoutExtension(path));
            }

            names.Sort(StringComparer.OrdinalIgnoreCase);
            return names;
        }

        /// <summary>
        /// Reads every celestial body name from the localization table.
        /// </summary>
        /// <returns>The body names, sorted, or an empty list when the table is absent.</returns>
        public static List<string> GetCelestialBodyNames()
        {
            var names = new List<string>();
            if (!File.Exists(CELESTIAL_BODY_LOCALIZATION))
                return names;

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string line in File.ReadLines(CELESTIAL_BODY_LOCALIZATION))
            {
                if (!line.StartsWith(BODY_KEY_PREFIX, StringComparison.Ordinal))
                    continue;

                int comma = line.IndexOf(',');
                string key = comma < 0 ? line : line[..comma];
                string name = key[BODY_KEY_PREFIX.Length..].Trim();

                // An underscore marks a scene object keyed under the body prefix rather than a body.
                if (name.Length == 0 || name.Contains('_') || NON_BODY_KEYS.Contains(name))
                    continue;
                if (seen.Add(name))
                {
                    names.Add(name);
                }
            }

            names.Sort(StringComparer.OrdinalIgnoreCase);
            return names;
        }

        /// <summary>
        /// Guesses which body a biome mask belongs to from its file name.
        /// </summary>
        /// <param name="maskFileName">File name of the mask, with or without its extension.</param>
        /// <param name="knownBodies">Body names to match against.</param>
        /// <returns>The matching body name in its canonical casing, or an empty string when nothing matches.</returns>
        public static string GuessBodyFromMaskName(string maskFileName, IReadOnlyList<string> knownBodies)
        {
            if (string.IsNullOrEmpty(maskFileName) || knownBodies == null)
                return "";

            string stem = Path.GetFileNameWithoutExtension(maskFileName);
            foreach (string suffix in MASK_NAME_SUFFIXES)
            {
                if (stem.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                {
                    stem = stem[..^suffix.Length];
                    break;
                }
            }

            foreach (string body in knownBodies)
            {
                if (string.Equals(body, stem, StringComparison.OrdinalIgnoreCase))
                    return body;
            }

            return "";
        }

        /// <summary>
        /// Reports whether the project defines a resource by this name.
        /// </summary>
        /// <param name="resourceName">The resource name to check.</param>
        /// <returns>True if a matching definition JSON exists, false otherwise.</returns>
        public static bool HasResourceDefinition(string resourceName)
        {
            if (string.IsNullOrEmpty(resourceName))
                return false;
            return File.Exists($"{RESOURCE_DEFINITIONS_FOLDER}/{resourceName}.json");
        }
    }
}
