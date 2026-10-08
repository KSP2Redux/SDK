using System.Collections.Generic;
using AwesomeTechnologies.VegetationSystem;
using KSP;
using Ksp2UnityTools.Editor.Validation;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Validation.Validators.Scatter
{
    /// <summary>
    /// Errors when a dependency rule names an item that is not on the body, or that spawns too late.
    /// </summary>
    /// <remarks>
    /// Spawning walks the packages and their items in order, and a dependency rule can only read the
    /// buffer of an item that has already spawned. Depending on an item further down the list leaves
    /// the rule with nothing to sample, and it fails exactly like a missing target while the target
    /// sits right there in the inspector. Both failures are silent.
    /// </remarks>
    public sealed class ScatterDependencyTargetValidator : IPlanetValidator
    {
        /// <summary>
        /// Stable code identifying issues emitted by this validator.
        /// </summary>
        public const string Code = "SCATTER_DEPENDENCY_MISSING";

        /// <inheritdoc />
        public BodyClassFlags AppliesTo => BodyClassFlags.SolidSurface;

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(CoreCelestialBodyData body)
        {
            var order = new Dictionary<string, int>();
            var names = new Dictionary<string, string>();
            var position = 0;
            foreach ((VegetationPackagePro _, VegetationItemInfoPro item) in ScatterValidatorHelper.Items(body))
            {
                if (!string.IsNullOrEmpty(item.VegetationItemID) && !order.ContainsKey(item.VegetationItemID))
                {
                    order[item.VegetationItemID] = position;
                    names[item.VegetationItemID] = item.Name;
                }

                position++;
            }

            position = 0;
            foreach ((VegetationPackagePro package, VegetationItemInfoPro item) in ScatterValidatorHelper.Items(body))
            {
                int self = position++;
                if (!item.UseDependencyRules)
                    continue;

                string where = $"Item '{item.Name}' in package '{ScatterValidatorHelper.PackageLabel(package)}'";
                if (string.IsNullOrEmpty(item.DependencyVegetationID) ||
                    !order.TryGetValue(item.DependencyVegetationID, out int target))
                {
                    yield return new ValidationIssue(
                        Code,
                        ValidationSeverity.Error,
                        $"{where} depends on an item that is not on this body, so the rule never resolves.");
                    continue;
                }

                if (target < self)
                    continue;

                if (target == self)
                {
                    yield return new ValidationIssue(
                        Code,
                        ValidationSeverity.Error,
                        $"{where} depends on itself, which no ordering can satisfy. Point the rule at "
                        + "another item or turn dependency rules off.");
                    continue;
                }

                yield return new ValidationIssue(
                    Code,
                    ValidationSeverity.Error,
                    $"{where} depends on '{names[item.DependencyVegetationID]}', which spawns after it. "
                    + "Move the dependency earlier in the item list or the rule is silently ignored.");
            }
        }
    }
}
