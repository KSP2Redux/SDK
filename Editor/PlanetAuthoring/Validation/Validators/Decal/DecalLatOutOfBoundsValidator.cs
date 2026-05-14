using System.Collections.Generic;
using KSP;
using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Validation.Validators.Decal
{
    /// <summary>
    /// Errors when a decal instance has latitude outside the <c>[-90, 90]</c> range.
    /// </summary>
    /// <remarks>
    /// Latitudes outside the valid range produce undefined surface projections. The fix clamps the value to the
    /// nearest pole. See <see cref="DecalProPoleValidator" /> for the in-range pole-proximity check.
    /// </remarks>
    public sealed class DecalLatOutOfBoundsValidator : IPlanetValidator
    {
        /// <summary>Stable code identifying issues emitted by this validator.</summary>
        public const string Code = "DECAL_LAT_OOB";

        /// <inheritdoc />
        public BodyClassFlags AppliesTo => BodyClassFlags.SolidSurface;

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(CoreCelestialBodyData body)
        {
            if (body == null) yield break;
            foreach (PQSDecalInstance inst in body.GetComponentsInChildren<PQSDecalInstance>(includeInactive: true))
            {
                if (inst == null) continue;
                float lat = inst.LatLong.x;
                if (lat >= -90f && lat <= 90f) continue;

                PQSDecalInstance captured = inst;
                yield return new ValidationIssue(
                    Code,
                    ValidationSeverity.Error,
                    $"Decal '{inst.gameObject.name}' has latitude {lat:0.###} outside [-90, 90].",
                    new[] { new ValidationFix("Clamp to nearest pole", () => Clamp(captured)) });
            }
        }

        private static void Clamp(PQSDecalInstance inst)
        {
            Undo.RecordObject(inst, "Clamp decal latitude");
            inst.LatLong = new Vector2(Mathf.Clamp(inst.LatLong.x, -90f, 90f), inst.LatLong.y);
            inst.UpdateDecalTransform();
            EditorUtility.SetDirty(inst);
        }
    }
}
