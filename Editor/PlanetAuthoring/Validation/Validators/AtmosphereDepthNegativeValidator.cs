using System.Collections.Generic;
using KSP;
using UnityEditor;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Validation.Validators
{
    /// <summary>
    /// Errors when atmosphere depth is negative.
    /// </summary>
    /// <remarks>
    /// A negative depth is physically meaningless and the runtime atmosphere math will produce NaN or invert the gradient.
    /// </remarks>
    public sealed class AtmosphereDepthNegativeValidator : IPlanetValidator
    {
        /// <summary>Stable code identifying issues emitted by this validator.</summary>
        public const string Code = "ATMO_DEPTH_NEGATIVE";

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(CoreCelestialBodyData body)
        {
            if (body?.Core?.data == null)
                yield break;
            var data = body.Core.data;
            if (!data.hasAtmosphere || data.atmosphereDepth >= 0)
                yield break;

            var fixes = new[]
            {
                new ValidationFix("Clamp to 0", () => ClampToZero(body)),
            };
            yield return new ValidationIssue(
                Code,
                ValidationSeverity.Error,
                $"Atmosphere Depth ({data.atmosphereDepth:0.#} m) is negative.",
                fixes);
        }

        private static void ClampToZero(CoreCelestialBodyData body)
        {
            if (body?.Core?.data == null)
                return;
            Undo.RecordObject(body, "Clamp Atmosphere Depth");
            body.Core.data.atmosphereDepth = 0;
            EditorUtility.SetDirty(body);
        }
    }
}
