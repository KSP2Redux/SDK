using System.Collections.Generic;
using KSP;
using UnityEditor;
using UnityEngine;
using Ksp2UnityTools.Editor.Validation;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Validation.Validators.Decal
{
    /// <summary>
    /// Warns when a decal instance has longitude outside the <c>[-180, 180]</c> range.
    /// </summary>
    /// <remarks>
    /// Longitudes outside the canonical range render but the inspector and tools assume the canonical range. The
    /// fix wraps the value into <c>[-180, 180]</c> using modular arithmetic.
    /// </remarks>
    public sealed class DecalLongitudeOutOfBoundsValidator : IPlanetValidator
    {
        /// <summary>Stable code identifying issues emitted by this validator.</summary>
        public const string Code = "DECAL_LON_NORM";

        /// <inheritdoc />
        public BodyClassFlags AppliesTo => BodyClassFlags.SolidSurface;

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(CoreCelestialBodyData body)
        {
            if (body == null) yield break;
            foreach (PQSDecalInstance inst in body.GetComponentsInChildren<PQSDecalInstance>(includeInactive: true))
            {
                if (inst == null) continue;
                float lon = inst.LatLong.y;
                if (lon >= -180f && lon <= 180f) continue;

                PQSDecalInstance captured = inst;
                yield return new ValidationIssue(
                    Code,
                    ValidationSeverity.Warning,
                    $"Decal '{inst.gameObject.name}' has longitude {lon:0.###} outside [-180, 180].",
                    new[] { new ValidationFix("Wrap to canonical range", () => Wrap(captured)) });
            }
        }

        private static void Wrap(PQSDecalInstance inst)
        {
            Undo.RecordObject(inst, "Wrap decal longitude");
            float lon = inst.LatLong.y;
            lon = ((lon + 180f) % 360f + 360f) % 360f - 180f;
            inst.LatLong = new Vector2(inst.LatLong.x, lon);
            inst.UpdateDecalTransform();
            EditorUtility.SetDirty(inst);
        }
    }
}
