using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Ksp2UnityTools.Editor.MissionAuthoring.Widgets
{
    /// <summary>
    /// Enumerates known mission granter NameKeys for the Granter field's autocomplete.
    /// </summary>
    /// <remarks>
    /// Stock granter keys are inlined as a static list so the SDK works in environments where
    /// the ksp2-assets dump is not present (i.e. shipping to mod authors who don't have the
    /// ripped base-game assets sitting alongside the project). Stock data doesn't change, so
    /// inlining is correct and cheap. Per-project granter definitions are still discovered
    /// dynamically by scanning the project's MissionGranters folder for NameKey values.
    /// Editor-only static cache. Domain reload invalidates.
    /// </remarks>
    public static class MissionGranterKeyCatalog
    {
        private const string REDUX_GRANTERS_REL = "ReduxAssets/Definitions/MissionGranters";

        private static readonly Regex NameKeyRegex =
            new("\"NameKey\"\\s*:\\s*\"([^\"]+)\"", RegexOptions.Compiled);

        private static readonly string[] StockGranterKeys =
        {
            "MissionGranterCoopsConnectors",
            "MissionGranterEEG",
            "MissionGranterIonic",
            "MissionGranterJebsJunkyard",
            "MissionGranterJoelsLenses",
            "MissionGranterKHS",
            "MissionGranterKSC",
            "MissionGranterKerbodyne",
            "MissionGranterLightyear",
            "MissionGranterMajorComms",
            "MissionGranterMaxo",
            "MissionGranterOMB",
            "MissionGranterProbodobodyne",
            "MissionGranterReactionSystems",
            "MissionGranterRockomax",
            "MissionGranterRokea",
            "MissionGranterSeansCannery",
            "MissionGranterStrutCo",
            "MissionGranterWinterOwl",
        };

        // Redux-added granters. Hand-maintained alongside the stock list so they appear in
        // mod-author autocompletes when the SDK is consumed outside the Redux main project.
        // Update this array whenever Redux ships a new granter.
        private static readonly string[] ReduxGranterKeys =
        {
            "MissionGranterKerbalFossil",
            "MissionGranterKerbinWorldFirst",
            "MissionGranterMegaMining",
            "MissionGranterPerpetuWarm",
            "MissionGranterShallowStone",
            "MissionGranterUrika",
        };

        private static List<string> _cached;

        /// <summary>
        /// Returns the alphabetically-sorted, deduplicated list of known granter NameKeys.
        /// </summary>
        /// <returns>The known granter NameKeys.</returns>
        public static IReadOnlyList<string> GetKnownGranterKeys()
        {
            if (_cached != null) return _cached;

            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var k in StockGranterKeys) keys.Add(k);
            foreach (var k in ReduxGranterKeys) keys.Add(k);
            ScanProjectGranterDefinitions(keys);

            _cached = keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToList();
            return _cached;
        }

        private static void ScanProjectGranterDefinitions(HashSet<string> sink)
        {
            var dir = Path.Combine(Application.dataPath, REDUX_GRANTERS_REL);
            if (!Directory.Exists(dir)) return;
            foreach (var path in Directory.EnumerateFiles(dir, "*.json", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    var text = File.ReadAllText(path);
                    foreach (Match m in NameKeyRegex.Matches(text))
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
