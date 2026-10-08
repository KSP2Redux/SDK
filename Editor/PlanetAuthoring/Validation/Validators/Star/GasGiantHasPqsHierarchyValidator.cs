using System.Collections.Generic;
using KSP;
using KSP.Rendering.Planets;
using Ksp2UnityTools.Editor.PlanetAuthoring.Tools;
using Ksp2UnityTools.Editor.Validation;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Validation.Validators.Star
{
    /// <summary>
    /// Warns when a gas giant still carries a leftover <see cref="PQS" /> in its hierarchy.
    /// </summary>
    /// <remarks>
    /// Typically a leftover from reclassifying a solid body to a gas giant. The PQS will not render but its presence
    /// confuses other validators and the live preview boot path.
    /// </remarks>
    public sealed class GasGiantHasPqsHierarchyValidator : IPlanetValidator
    {
        /// <summary>Stable code identifying issues emitted by this validator.</summary>
        public const string Code = "GAS_GIANT_HAS_PQS_HIERARCHY";

        /// <inheritdoc />
        public BodyClassFlags AppliesTo => BodyClassFlags.GasGiant;

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(CoreCelestialBodyData body)
        {
            if (body == null) yield break;
            PQS pqs = BodyResolver.FindPqsIncludingAsset(body);
            if (pqs == null) yield break;

            yield return new ValidationIssue(
                Code,
                ValidationSeverity.Warning,
                $"Gas giant '{body.Core?.data?.bodyName}' has a PQS in its hierarchy. Remove the PQS prefab from the authoring scene or set Has Solid Surface back to true.");
        }
    }
}
