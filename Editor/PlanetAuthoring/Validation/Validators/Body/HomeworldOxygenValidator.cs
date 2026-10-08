using System.Collections.Generic;
using KSP;
using UnityEditor;
using Ksp2UnityTools.Editor.Validation;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Validation.Validators.Body
{
    /// <summary>
    /// Warns when the home world has an atmosphere without breathable oxygen.
    /// </summary>
    /// <remarks>
    /// Kerbals on the home-world surface need oxygen to operate without helmets. Off is legal but is more often a missed checkbox than a deliberate choice.
    /// </remarks>
    public sealed class HomeworldOxygenValidator : IPlanetValidator
    {
        /// <summary>Stable code identifying issues emitted by this validator.</summary>
        public const string Code = "ATMO_NO_OXYGEN_ON_HOMEWORLD";

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(CoreCelestialBodyData body)
        {
            if (body?.Core?.data == null)
                yield break;
            var data = body.Core.data;
            if (!data.isHomeWorld || !data.hasAtmosphere || data.atmosphereContainsOxygen)
                yield break;

            var fixes = new[]
            {
                new ValidationFix("Enable Contains Oxygen", () => EnableOxygen(body)),
            };
            yield return new ValidationIssue(
                Code,
                ValidationSeverity.Warning,
                "Body is flagged Is Home World but Contains Oxygen is off. Kerbals on the surface will need helmets.",
                fixes);
        }

        private static void EnableOxygen(CoreCelestialBodyData body)
        {
            if (body?.Core?.data == null)
                return;
            Undo.RecordObject(body, "Enable Contains Oxygen");
            body.Core.data.atmosphereContainsOxygen = true;
            EditorUtility.SetDirty(body);
        }
    }
}
