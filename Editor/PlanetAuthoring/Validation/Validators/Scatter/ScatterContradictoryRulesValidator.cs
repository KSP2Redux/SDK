using System.Collections.Generic;
using AwesomeTechnologies.VegetationSystem;
using KSP;
using Ksp2UnityTools.Editor.PlanetAuthoring.Scatter;
using Ksp2UnityTools.Editor.Validation;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Validation.Validators.Scatter
{
    /// <summary>
    /// Errors when an item both requires and forbids the same stratum.
    /// </summary>
    /// <remarks>
    /// Exclusion wins, so an overlapping weight window leaves the include rule unsatisfiable there.
    /// When it is the item's only include rule, the item spawns nowhere at all and reports nothing.
    ///
    /// Stock places vegetation by excluding rock rather than including grass, so items routinely
    /// carry both lists, and a stratum sitting in both is invisible from reading either one alone.
    /// </remarks>
    public sealed class ScatterContradictoryRulesValidator : IPlanetValidator
    {
        /// <summary>
        /// Stable code identifying issues emitted by this validator.
        /// </summary>
        public const string Code = "SCATTER_CONTRADICTORY_RULES";

        /// <inheritdoc />
        public BodyClassFlags AppliesTo => BodyClassFlags.SolidSurface;

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(CoreCelestialBodyData body)
        {
            VegetationSystemPro system = ScatterValidatorHelper.FindSystem(body);
            ScatterStratumLookup lookup = ScatterStratumLookup.FromSystem(system);

            foreach ((VegetationPackagePro package, VegetationItemInfoPro item) in ScatterValidatorHelper.Items(body))
            {
                if (!item.UseProceduralTextureIncludeRules || !item.UseProceduralTextureExcludeRules)
                    continue;

                IReadOnlyList<ScatterRuleAnalysis.Contradiction> found = ScatterRuleAnalysis.FindContradictions(
                    item.ProceduralTextureIncludeRuleList,
                    item.ProceduralTextureExcludeRuleList);

                foreach (ScatterRuleAnalysis.Contradiction contradiction in found)
                {
                    string stratum = lookup.TryGetBySlice(contradiction.SliceIndex, out ScatterStratum resolved)
                        ? $"'{resolved.DisplayName}'"
                        : $"{contradiction.SliceIndex}";

                    yield return new ValidationIssue(
                        Code,
                        ValidationSeverity.Error,
                        $"Item '{item.Name}' in package '{ScatterValidatorHelper.PackageLabel(package)}' both includes and excludes stratum "
                        + $"{stratum} over weights {contradiction.Minimum:0.##} to {contradiction.Maximum:0.##}. "
                        + "Exclusion wins, so the include rule can never be satisfied there.");
                }
            }
        }
    }
}
