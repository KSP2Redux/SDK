using System.Collections.Generic;
using KSP;
using UnityEditor;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Validation.Validators.Body
{
    /// <summary>
    /// Warns when the home world has no atmosphere at all.
    /// </summary>
    /// <remarks>
    /// A no-atmosphere home world is legal but unusual. Kerbals need helmets on the surface and there is no aerodynamic flight or aerobraking. The companion <see cref="HomeworldOxygenValidator" /> covers the case where an atmosphere exists but lacks oxygen.
    /// </remarks>
    public sealed class HomeworldAtmosphereValidator : IPlanetValidator
    {
        /// <summary>Stable code identifying issues emitted by this validator.</summary>
        public const string Code = "HOMEWORLD_NO_ATMOSPHERE";

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(CoreCelestialBodyData body)
        {
            if (body?.Core?.data == null)
                yield break;
            var data = body.Core.data;
            if (!data.isHomeWorld || data.hasAtmosphere)
                yield break;

            var fixes = new[]
            {
                new ValidationFix("Enable Has Atmosphere", () => EnableAtmosphere(body)),
            };
            yield return new ValidationIssue(
                Code,
                ValidationSeverity.Warning,
                "Body is flagged Is Home World but Has Atmosphere is off. Kerbals on the surface will need helmets and there is no aerodynamic flight.",
                fixes);
        }

        private static void EnableAtmosphere(CoreCelestialBodyData body)
        {
            if (body?.Core?.data == null)
                return;
            Undo.RecordObject(body, "Enable Has Atmosphere");
            body.Core.data.hasAtmosphere = true;
            EditorUtility.SetDirty(body);
        }
    }
}
