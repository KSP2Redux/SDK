using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Ksp2UnityTools.Editor.MissionAuthoring.Widgets
{
    /// <summary>
    /// Enumerates known celestial body keys for the destination-position picker's
    /// autocomplete.
    /// </summary>
    /// <remarks>
    /// Stock body names are inlined so the SDK works without the ksp2-assets dump. Redux
    /// bodies are discovered by scanning <c>Assets/ReduxAssets/Definitions/CelestialBodies</c>
    /// for JSON files containing a <c>"bodyName"</c> field. Editor-only static cache.
    /// Domain reload invalidates.
    /// </remarks>
    public static class CelestialBodyKeyCatalog
    {
        private const string REDUX_BODIES_REL = "ReduxAssets/Definitions/CelestialBodies";

        private static readonly Regex BodyNameRegex =
            new("\"bodyName\"\\s*:\\s*\"([^\"]+)\"", RegexOptions.Compiled);

        private static readonly string[] StockBodyKeys =
        {
            "Kerbol",
            "Moho",
            "Eve",
            "Gilly",
            "Kerbin",
            "Mun",
            "Minmus",
            "Duna",
            "Ike",
            "Dres",
            "Jool",
            "Laythe",
            "Vall",
            "Tylo",
            "Bop",
            "Pol",
            "Eeloo",
        };

        // Redux-added bodies. Hand-maintained alongside the stock list so they appear in
        // mod-author autocompletes when the SDK is consumed outside the Redux main project.
        // Update this array whenever Redux ships a new celestial body.
        private static readonly string[] ReduxBodyKeys =
        {
            "Drast",
        };

        private static List<string> _cached;

        /// <summary>
        /// Returns the alphabetically-sorted, deduplicated list of known celestial body keys.
        /// </summary>
        /// <returns>The known body keys.</returns>
        public static IReadOnlyList<string> GetKnownBodyKeys()
        {
            if (_cached != null) return _cached;

            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var k in StockBodyKeys) keys.Add(k);
            foreach (var k in ReduxBodyKeys) keys.Add(k);
            ScanProjectBodyDefinitions(keys);

            _cached = keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToList();
            return _cached;
        }

        private static void ScanProjectBodyDefinitions(HashSet<string> sink)
        {
            var dir = Path.Combine(Application.dataPath, REDUX_BODIES_REL);
            if (!Directory.Exists(dir)) return;
            foreach (var path in Directory.EnumerateFiles(dir, "*.json", SearchOption.AllDirectories))
            {
                try
                {
                    var text = File.ReadAllText(path);
                    foreach (Match m in BodyNameRegex.Matches(text))
                    {
                        var v = m.Groups[1].Value.Trim();
                        if (!string.IsNullOrEmpty(v)) sink.Add(v);
                    }
                }
                catch
                {
                }
            }
        }
    }
}
