using System.Collections.Generic;
using KSP;
using Ksp2UnityTools.Editor.Validation;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Validation.Validators.Environment
{
    /// <summary>
    /// Warns when no terrain dips below sea level, so the ocean is hidden everywhere.
    /// </summary>
    /// <remarks>
    /// The ocean surface sits at the body radius, and Min Terrain Height is measured from that same
    /// sea level, so a non-negative minimum leaves no water to see. Raising Ocean Altitude lowers the
    /// terrain base and is the usual fix. A body whose terrain range was never computed, with both
    /// bounds still zero, is skipped rather than reported.
    /// </remarks>
    public class OceanBelowTerrainValidator : IPlanetValidator
    {
        /// <summary>Stable code identifying issues emitted by this validator.</summary>
        public const string Code = "OCEAN_BELOW_TERRAIN";

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(CoreCelestialBodyData body)
        {
            if (body == null || body.Core?.data == null)
                yield break;

            var data = body.Core.data;
            if (!data.hasOcean)
                yield break;
            if (data.MinTerrainHeight == 0.0 && data.MaxTerrainHeight == 0.0)
                yield break;
            if (data.MinTerrainHeight < 0.0)
                yield break;

            string message = $"Min Terrain Height ({data.MinTerrainHeight:0.#} m) is at or above sea level, so no terrain dips below the ocean and it is never visible. Raise Ocean Altitude to lower the terrain base, or turn off Has Ocean.";
            yield return new ValidationIssue(Code, ValidationSeverity.Warning, message);
        }
    }
}
