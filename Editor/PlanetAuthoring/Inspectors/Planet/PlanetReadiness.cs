using System;
using System.Collections.Generic;
using Ksp2UnityTools.Editor.PlanetAuthoring.Atmosphere;
using Ksp2UnityTools.Editor.PlanetAuthoring.Authoring;
using Ksp2UnityTools.Editor.PlanetAuthoring.Clouds;
using Ksp2UnityTools.Editor.PlanetAuthoring.Ocean;
using Ksp2UnityTools.Editor.PlanetAuthoring.Validation;
using Ksp2UnityTools.Editor.Validation;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Inspectors.Planet
{
    /// <summary>
    /// How ready one part of a body is, as a readiness chip shows it.
    /// </summary>
    public enum ReadinessState
    {
        /// <summary>Nothing to do.</summary>
        Ready,

        /// <summary>Something is stale or missing, but the body still loads.</summary>
        Warning,

        /// <summary>Something is broken.</summary>
        Error,
    }

    /// <summary>
    /// One readiness chip: a part of the body, the validation codes that speak for it and the section that fixes it.
    /// </summary>
    public class ReadinessChip
    {
        /// <summary>
        /// Initializes a chip.
        /// </summary>
        /// <param name="name">The part of the body the chip is about, such as "Ocean".</param>
        /// <param name="readyText">The chip's text when nothing is wrong.</param>
        /// <param name="codePrefixes">Validation codes, or code prefixes ending in an underscore, the chip reports.</param>
        /// <param name="tab">The tab holding the section that fixes the chip's issues.</param>
        /// <param name="section">The name of that section's foldout, or its text for a section built in code.</param>
        /// <param name="appliesTo">Whether the chip is shown for a body.</param>
        public ReadinessChip(string name, string readyText, string[] codePrefixes, PlanetInspectorTab tab, string section, Func<PlanetInspectorContext, bool> appliesTo)
        {
            Name = name;
            ReadyText = readyText;
            CodePrefixes = codePrefixes;
            Tab = tab;
            Section = section;
            AppliesTo = appliesTo;
        }

        /// <summary>
        /// Gets the part of the body the chip is about.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the chip's text when nothing is wrong.
        /// </summary>
        public string ReadyText { get; }

        /// <summary>
        /// Gets the validation codes, or code prefixes ending in an underscore, the chip reports.
        /// </summary>
        public string[] CodePrefixes { get; }

        /// <summary>
        /// Gets the tab holding the section that fixes the chip's issues.
        /// </summary>
        public PlanetInspectorTab Tab { get; }

        /// <summary>
        /// Gets the name, or text, of the section's foldout.
        /// </summary>
        public string Section { get; }

        /// <summary>
        /// Gets whether the chip is shown for a body.
        /// </summary>
        public Func<PlanetInspectorContext, bool> AppliesTo { get; }
    }

    /// <summary>
    /// The planet inspector's readiness chips, worked out from the body's validation issues.
    /// </summary>
    /// <remarks>
    /// The chips read the same cheap validation run as the validation chip, plus whatever expensive results a full
    /// run left in <see cref="ValidationExpensiveCache" />, so they never disagree with the Validation Report.
    /// </remarks>
    public static class PlanetReadiness
    {
        /// <summary>
        /// The chips, in header order.
        /// </summary>
        public static readonly ReadinessChip[] CHIPS =
        {
            new("Surface bake", "Surface baked", new[] { "SURFACE_BAKE_DRIFT", "SMALL_TILES_PACK_DRIFT" },
                PlanetInspectorTab.Terrain, "section-bake", IsSolid),
            new("Biome lookup", "Biome lookup baked", new[] { "BIOME_LOOKUP_" },
                PlanetInspectorTab.Surface, "Biome lookup", IsSolid),
            new("Decals", "Decals baked", new[] { "DECALS_UNBAKED", "DECAL_" },
                PlanetInspectorTab.Features, "section-decals", IsSolid),
            new("Atmosphere", "Atmosphere ready", new[] { "ATMO_" },
                PlanetInspectorTab.Environment, "section-atmosphere", context => AtmosphereSetup.HasAtmosphere(context.Body)),
            new("Clouds", "Clouds ready", new[] { "CLOUD_" },
                PlanetInspectorTab.Environment, "section-clouds", context => CloudSetup.HasClouds(context.Body)),
            new("Ocean", "Ocean ready", new[] { "OCEAN_" },
                PlanetInspectorTab.Environment, "section-ocean", context => OceanSetup.HasOcean(context.Body)),
            new("Science", "Science baked", new[] { "SR_", "DISCOVERABLE_", "MISSING_SCIENCE_REGIONS", "SCIENCE_REGION_MAP_NOT_READABLE" },
                PlanetInspectorTab.Science, null, IsSolid),
        };

        /// <summary>
        /// Works out a chip's state from a body's issues.
        /// </summary>
        /// <remarks>
        /// An Info issue does not make a chip anything but ready. It still shows in the Validation Report.
        /// </remarks>
        /// <param name="chip">The chip.</param>
        /// <param name="issues">Every issue the body has.</param>
        /// <param name="matched">Receives the issues the chip reports, Info issues left out.</param>
        /// <returns>The worst severity among the matched issues.</returns>
        public static ReadinessState Evaluate(ReadinessChip chip, IEnumerable<ValidationIssue> issues, List<ValidationIssue> matched)
        {
            var state = ReadinessState.Ready;
            foreach (ValidationIssue issue in issues)
            {
                if (issue.Severity == ValidationSeverity.Info || !Reports(chip, issue.Code))
                    continue;

                matched.Add(issue);
                if (issue.Severity == ValidationSeverity.Error)
                {
                    state = ReadinessState.Error;
                }
                else if (state == ReadinessState.Ready)
                {
                    state = ReadinessState.Warning;
                }
            }

            return state;
        }

        /// <summary>
        /// Gets the text a chip shows.
        /// </summary>
        /// <param name="chip">The chip.</param>
        /// <param name="state">The chip's state.</param>
        /// <param name="issueCount">How many issues the chip reports.</param>
        /// <returns>The chip's text.</returns>
        public static string Describe(ReadinessChip chip, ReadinessState state, int issueCount) =>
            state == ReadinessState.Ready
                ? chip.ReadyText
                : $"{chip.Name}: {issueCount} {(state == ReadinessState.Error ? "error" : "warning")}{(issueCount == 1 ? string.Empty : "s")}";

        /// <summary>
        /// Gets the issues for a surface bake that has never run, which the expensive drift check alone cannot tell.
        /// </summary>
        /// <param name="context">The inspector's context.</param>
        /// <returns>A warning when the body has PQS data but no recorded surface bake, otherwise nothing.</returns>
        public static IEnumerable<ValidationIssue> CheapSurfaceBakeIssues(PlanetInspectorContext context)
        {
            if (context.PqsData == null)
                yield break;

            PQSDataAuthoring authoring = AuthoringSidecars.Find(context.PqsData);
            if (authoring == null || !string.IsNullOrEmpty(authoring.LastSurfaceBakeFingerprint))
                yield break;

            yield return new ValidationIssue("SURFACE_BAKE_DRIFT", ValidationSeverity.Warning, "The body surface has never been baked.");
        }

        private static bool IsSolid(PlanetInspectorContext context) => context.BodyClass == BodyClassFlags.SolidSurface;

        private static bool Reports(ReadinessChip chip, string code)
        {
            foreach (string prefix in chip.CodePrefixes)
            {
                bool matches = prefix.EndsWith("_") ? code.StartsWith(prefix, StringComparison.Ordinal) : code == prefix;
                if (matches)
                    return true;
            }

            return false;
        }
    }
}
