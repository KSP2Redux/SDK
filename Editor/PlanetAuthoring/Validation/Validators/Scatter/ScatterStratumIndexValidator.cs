using System.Collections.Generic;
using AwesomeTechnologies.VegetationSystem;
using KSP;
using Ksp2UnityTools.Editor.PlanetAuthoring.Scatter;
using Ksp2UnityTools.Editor.Validation;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Validation.Validators.Scatter
{
    /// <summary>
    /// Errors when a texture rule names a stratum the body does not have.
    /// </summary>
    /// <remarks>
    /// A rule stores a packed slice index, and the packer allocates exactly as many slices as there
    /// are filled cells. An index outside that range addresses nothing and the rule can never match,
    /// which kills the item outright as an include rule and leaves it unrestricted as an exclude
    /// rule. Neither reports anything.
    ///
    /// Checks run only when the body's strata resolve, since every index is unverifiable otherwise.
    /// </remarks>
    public sealed class ScatterStratumIndexValidator : IPlanetValidator
    {
        /// <summary>
        /// Stable code identifying issues emitted by this validator.
        /// </summary>
        public const string Code = "SCATTER_DEAD_STRATUM";

        /// <inheritdoc />
        public BodyClassFlags AppliesTo => BodyClassFlags.SolidSurface;

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(CoreCelestialBodyData body)
        {
            VegetationSystemPro system = ScatterValidatorHelper.FindSystem(body);
            if (system == null)
                yield break;

            ScatterStratumLookup lookup = ScatterStratumLookup.FromSystem(system);
            if (!lookup.IsAvailable)
                yield break;

            foreach ((VegetationPackagePro package, VegetationItemInfoPro item) in ScatterValidatorHelper.Items(body))
            {
                string where = $"Item '{item.Name}' in package '{ScatterValidatorHelper.PackageLabel(package)}'";

                if (item.UseProceduralTextureIncludeRules)
                {
                    foreach (ValidationIssue issue in Check(item.ProceduralTextureIncludeRuleList, lookup, where, "include"))
                    {
                        yield return issue;
                    }
                }

                if (!item.UseProceduralTextureExcludeRules)
                    continue;

                foreach (ValidationIssue issue in Check(item.ProceduralTextureExcludeRuleList, lookup, where, "exclude"))
                {
                    yield return issue;
                }
            }
        }

        private static IEnumerable<ValidationIssue> Check(
            List<TerrainTextureRule> rules,
            ScatterStratumLookup lookup,
            string where,
            string kind)
        {
            if (rules == null)
                yield break;

            foreach (TerrainTextureRule rule in rules)
            {
                if (rule == null || lookup.IsSliceInRange(rule.TextureIndex))
                    continue;

                yield return new ValidationIssue(
                    Code,
                    ValidationSeverity.Error,
                    $"{where} has an {kind} rule on stratum {rule.TextureIndex}, but this body only has "
                    + $"{lookup.PackedSliceCount} packed strata. The rule can never match.");
            }
        }
    }
}
