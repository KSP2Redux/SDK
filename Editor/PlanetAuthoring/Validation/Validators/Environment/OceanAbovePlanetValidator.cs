using System.Collections.Generic;
using KSP;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Validation.Validators.Environment
{
    /// <summary>
    /// Warns when the ocean surface sits above the top of the atmosphere.
    /// </summary>
    /// <remarks>
    /// A liquid surface in vacuum is a physical contradiction and indicates that one of the two altitudes is mistuned.
    /// </remarks>
    public sealed class OceanAbovePlanetValidator : IPlanetValidator
    {
        /// <summary>Stable code identifying issues emitted by this validator.</summary>
        public const string Code = "OCEAN_ABOVE_PLANET";

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(CoreCelestialBodyData body)
        {
            if (body?.Core?.data == null)
                yield break;
            var data = body.Core.data;
            if (!data.hasOcean || !data.hasAtmosphere)
                yield break;
            if (data.oceanAltitude <= data.atmosphereDepth)
                yield break;

            string message = $"Ocean Altitude ({data.oceanAltitude:0.#} m) is above Atmosphere Depth ({data.atmosphereDepth:0.#} m). The ocean surface is outside the atmosphere.";
            yield return new ValidationIssue(Code, ValidationSeverity.Warning, message);
        }
    }
}
