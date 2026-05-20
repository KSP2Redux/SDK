using System.Collections.Generic;
using KSP;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Validation.Validators.Decal
{
    /// <summary>
    /// Errors when a decal instance has no <c>PQSDecal</c> template assigned.
    /// </summary>
    /// <remarks>
    /// The instance references its template for textures, opacity, blend mode, and fade shape. A null template
    /// makes the instance contribute nothing at bake time and confuses the per-decal inspector.
    /// </remarks>
    public sealed class DecalTemplateMissingValidator : IPlanetValidator
    {
        /// <summary>Stable code identifying issues emitted by this validator.</summary>
        public const string Code = "DECAL_TEMPLATE_MISSING";

        /// <inheritdoc />
        public BodyClassFlags AppliesTo => BodyClassFlags.SolidSurface;

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(CoreCelestialBodyData body)
        {
            if (body == null) yield break;
            foreach (PQSDecalInstance inst in body.GetComponentsInChildren<PQSDecalInstance>(includeInactive: true))
            {
                if (inst == null || inst.PQSDecal != null) continue;
                yield return new ValidationIssue(
                    Code,
                    ValidationSeverity.Error,
                    $"Decal '{inst.gameObject.name}' has no PQSDecal template assigned.");
            }
        }
    }
}
