using System.Collections.Generic;
using KSP;
using UnityEditor;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Validation.Validators
{
    /// <summary>
    /// Errors when a star body has zero luminosity.
    /// </summary>
    /// <remarks>
    /// Solar flux at every body in the system is computed from the star's luminosity. A value of 0 leaves the system in darkness.
    /// </remarks>
    public sealed class StarLuminosityValidator : IPlanetValidator
    {
        /// <summary>Stable code identifying issues emitted by this validator.</summary>
        public const string Code = "STAR_NO_LUMINOSITY";

        private const double DefaultLuminosity = 3.06E+24;

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(CoreCelestialBodyData body)
        {
            if (body?.Core?.data == null)
                yield break;
            var data = body.Core.data;
            if (!data.isStar || data.StarLuminosity != 0)
                yield break;

            var fixes = new[]
            {
                new ValidationFix("Set to default (3.06e24 W)", () => SetDefaultLuminosity(body)),
            };
            yield return new ValidationIssue(
                Code,
                ValidationSeverity.Error,
                "Body is flagged Is Star but Star Luminosity is 0. The star will emit no light.",
                fixes);
        }

        private static void SetDefaultLuminosity(CoreCelestialBodyData body)
        {
            if (body?.Core?.data == null)
                return;
            Undo.RecordObject(body, "Set Star Luminosity");
            body.Core.data.StarLuminosity = DefaultLuminosity;
            EditorUtility.SetDirty(body);
        }
    }
}
