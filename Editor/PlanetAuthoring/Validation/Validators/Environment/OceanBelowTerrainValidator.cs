using System.Collections.Generic;
using KSP;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Validation.Validators.Environment
{
    /// <summary>
    /// Warns when the ocean surface sits below the lowest terrain.
    /// </summary>
    /// <remarks>
    /// An ocean entirely below terrain renders nothing visible, so the ocean flag is effectively wasted.
    /// </remarks>
    public sealed class OceanBelowTerrainValidator : IPlanetValidator
    {
        /// <summary>Stable code identifying issues emitted by this validator.</summary>
        public const string Code = "OCEAN_BELOW_TERRAIN";

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(CoreCelestialBodyData body)
        {
            if (body?.Core?.data == null)
                yield break;
            var data = body.Core.data;
            if (!data.hasOcean)
                yield break;
            if (data.oceanAltitude >= data.MinTerrainHeight)
                yield break;

            string message = $"Ocean Altitude ({data.oceanAltitude:0.#} m) sits below Min Terrain Height ({data.MinTerrainHeight:0.#} m). The ocean will be hidden by terrain.";
            yield return new ValidationIssue(Code, ValidationSeverity.Warning, message);
        }
    }
}
