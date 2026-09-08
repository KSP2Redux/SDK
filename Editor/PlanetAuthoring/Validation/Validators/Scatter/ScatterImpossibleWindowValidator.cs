using System.Collections.Generic;
using AwesomeTechnologies.VegetationSystem;
using KSP;
using Ksp2UnityTools.Editor.PlanetAuthoring.Scatter;
using Ksp2UnityTools.Editor.Validation;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Validation.Validators.Scatter
{
    /// <summary>
    /// Errors when an item carries a window whose minimum is above its maximum.
    /// </summary>
    /// <remarks>
    /// The spawn path rejects every candidate against an inverted window and reports nothing, so the
    /// item vanishes and looks identical to one that is out of camera range or gated by a mask.
    ///
    /// A window behind a rule toggle is checked only while that toggle is on. The scale range
    /// carries no toggle and is checked on every item.
    /// </remarks>
    public sealed class ScatterImpossibleWindowValidator : IPlanetValidator
    {
        /// <summary>
        /// Stable code identifying issues emitted by this validator.
        /// </summary>
        public const string Code = "SCATTER_IMPOSSIBLE_WINDOW";

        /// <inheritdoc />
        public BodyClassFlags AppliesTo => BodyClassFlags.SolidSurface;

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(CoreCelestialBodyData body)
        {
            foreach ((VegetationPackagePro package, VegetationItemInfoPro item) in ScatterValidatorHelper.Items(body))
            {
                string where = $"Item '{item.Name}' in package '{ScatterValidatorHelper.PackageLabel(package)}'";

                if (item.UseHeightRule && ScatterCountMath.IsImpossibleWindow(item.MinHeight, item.MaxHeight))
                {
                    yield return Impossible($"{where} has a height band of {item.MinHeight} to {item.MaxHeight}");
                }

                if (item.UseSteepnessRule && ScatterCountMath.IsImpossibleWindow(item.MinSteepness, item.MaxSteepness))
                {
                    yield return Impossible(
                        $"{where} has a steepness band of {item.MinSteepness} to {item.MaxSteepness} degrees");
                }

                if (ScatterCountMath.IsImpossibleWindow(item.MinScale, item.MaxScale))
                {
                    yield return Impossible($"{where} has a scale range of {item.MinScale} to {item.MaxScale}");
                }

                if (item.UseProceduralTextureIncludeRules)
                {
                    foreach (ValidationIssue issue in RuleWindows(item.ProceduralTextureIncludeRuleList, where, "include"))
                    {
                        yield return issue;
                    }
                }

                if (!item.UseProceduralTextureExcludeRules)
                    continue;

                foreach (ValidationIssue issue in RuleWindows(item.ProceduralTextureExcludeRuleList, where, "exclude"))
                {
                    yield return issue;
                }
            }
        }

        private static IEnumerable<ValidationIssue> RuleWindows(
            List<TerrainTextureRule> rules,
            string where,
            string kind)
        {
            if (rules == null)
                yield break;

            foreach (TerrainTextureRule rule in rules)
            {
                if (rule != null && ScatterCountMath.IsImpossibleWindow(rule.MinimumValue, rule.MaximumValue))
                {
                    yield return Impossible(
                        $"{where} has an {kind} rule on stratum {rule.TextureIndex} with a weight window of "
                        + $"{rule.MinimumValue} to {rule.MaximumValue}");
                }
            }
        }

        private static ValidationIssue Impossible(string description) =>
            new ValidationIssue(
                Code,
                ValidationSeverity.Error,
                $"{description}, which nothing can satisfy. The item will not spawn.");
    }
}
