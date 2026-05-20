using System.Collections.Generic;
using KSP;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Validation
{
    /// <summary>
    /// Contract for a single planet-authoring validator.
    /// </summary>
    /// <remarks>
    /// Implementations are auto-discovered by <see cref="PlanetValidatorRegistry" />. Concrete types must live in
    /// this assembly and have a public parameterless constructor. The runner filters validators per body by
    /// matching <see cref="AppliesTo" /> against <see cref="BodyClassClassifier.Classify" />.
    /// </remarks>
    public interface IPlanetValidator
    {
        /// <summary>
        /// Categorizes the validator for refresh-frequency purposes.
        /// </summary>
        /// <remarks>
        /// Defaults to <see cref="ValidatorCost.Cheap" />. Expensive validators must override.
        /// </remarks>
        ValidatorCost Cost => ValidatorCost.Cheap;

        /// <summary>
        /// Body classes the validator applies to.
        /// </summary>
        /// <remarks>
        /// The runner classifies each body via <see cref="BodyClassClassifier" /> and skips validators
        /// whose mask does not include the body's class. Defaults to <see cref="BodyClassFlags.All" />
        /// for validators that work on any body. PQS / biome / decal / science-region validators
        /// should narrow to <see cref="BodyClassFlags.SolidSurface" />. Star-specific checks should narrow
        /// to <see cref="BodyClassFlags.Star" />, and so on.
        /// </remarks>
        BodyClassFlags AppliesTo => BodyClassFlags.All;

        /// <summary>
        /// Runs the check against <paramref name="body" /> and returns any issues found.
        /// </summary>
        /// <remarks>
        /// Cheap validators are called on every refresh tick (~500ms) from open inspectors. Expensive
        /// validators run only when the user clicks "Run full validation" so heavy walks (texture
        /// pixels, large hash computes, big array scans) don't stall the inspector. Return an empty
        /// enumerable when nothing is wrong. Never return null.
        /// </remarks>
        /// <param name="body">The body to validate. May be a prefab asset or a scene instance. Implementations should null-check before walking deep into the hierarchy.</param>
        /// <returns>Issues produced by this validator. Empty when the check passes.</returns>
        IEnumerable<ValidationIssue> Validate(CoreCelestialBodyData body);
    }
}
