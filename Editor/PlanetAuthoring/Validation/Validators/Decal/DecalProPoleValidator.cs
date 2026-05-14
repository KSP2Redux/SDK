using System.Collections.Generic;
using KSP;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Validation.Validators.Decal
{
    /// <summary>
    /// Warns when a decal sits within 0.1 degree of a pole.
    /// </summary>
    /// <remarks>
    /// The equirectangular pole projection severely distorts decal footprints above 89.9 degrees of latitude. A
    /// decal at the pole spreads across the entire top row of pixels. See <see cref="DecalLatOutOfBoundsValidator" />
    /// for the out-of-range latitude check.
    /// </remarks>
    public sealed class DecalProPoleValidator : IPlanetValidator
    {
        /// <summary>Stable code identifying issues emitted by this validator.</summary>
        public const string Code = "DECAL_PRO_POLE";

        private const float ProPoleThreshold = 89.9f;

        /// <inheritdoc />
        public BodyClassFlags AppliesTo => BodyClassFlags.SolidSurface;

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(CoreCelestialBodyData body)
        {
            if (body == null) yield break;
            foreach (PQSDecalInstance inst in body.GetComponentsInChildren<PQSDecalInstance>(includeInactive: true))
            {
                if (inst == null) continue;
                float absLat = Mathf.Abs(inst.LatLong.x);
                if (absLat < ProPoleThreshold) continue;

                yield return new ValidationIssue(
                    Code,
                    ValidationSeverity.Warning,
                    $"Decal '{inst.gameObject.name}' at latitude {inst.LatLong.x:0.###} is within 0.1 degree of a pole. The footprint will distort along the entire top row.");
            }
        }
    }
}
