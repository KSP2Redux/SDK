using System.Collections.Generic;
using KSP;
using UnityEditor;
using UnityEngine;
using Ksp2UnityTools.Editor.Validation;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Validation.Validators.Body
{
    /// <summary>
    /// Warns when a body in the authoring scene has a non-identity rotation but no preview session
    /// is active, which means a previous framing rotation leaked past the session's restore.
    /// </summary>
    /// <remarks>
    /// SceneViewFraming rotates the body to bring the chosen lat/lon under the SceneView camera,
    /// and PlanetAuthoringSession.End restores the snapshot. A domain reload mid-preview, a crash
    /// before End, or applying overrides while the session was still rotating the body can all
    /// leave a non-identity rotation override behind. Surfacing it here gives the artist a
    /// one-click reset.
    /// </remarks>
    public sealed class StaleBodyRotationValidator : IPlanetValidator
    {
        /// <summary>Stable code identifying issues emitted by this validator.</summary>
        public const string Code = "STALE_BODY_ROTATION";

        // Anything below ~0.05 deg of total rotation can be floating-point noise from previous
        // edits and is ignored. Anything above is a real authored or leaked rotation.
        private const float ThresholdDegrees = 0.05f;

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(CoreCelestialBodyData body)
        {
            if (body == null)
                yield break;
            // Only flag while there's no live preview. A running session is legitimately rotating
            // the body, so the rotation isn't "stale" yet.
            if (PlanetAuthoringSession.Active != null)
                yield break;

            Quaternion rotation = body.transform.localRotation;
            float angle = Quaternion.Angle(rotation, Quaternion.identity);
            if (angle < ThresholdDegrees)
                yield break;

            var fixes = new[]
            {
                new ValidationFix("Reset rotation to identity", () => ResetRotation(body)),
            };
            yield return new ValidationIssue(
                Code,
                ValidationSeverity.Warning,
                $"Body transform has a non-identity rotation ({angle:0.0} deg from identity) and no preview session is active. Framing leaks like this happen if Unity reloaded mid-preview. Resetting clears the leak.",
                fixes);
        }

        private static void ResetRotation(CoreCelestialBodyData body)
        {
            if (body == null) return;
            Undo.RecordObject(body.transform, "Reset body rotation");
            if (PrefabUtility.IsPartOfPrefabInstance(body.transform))
            {
                var so = new SerializedObject(body.transform);
                SerializedProperty prop = so.FindProperty("m_LocalRotation");
                if (prop != null)
                    PrefabUtility.RevertPropertyOverride(prop, InteractionMode.AutomatedAction);
            }
            else
            {
                body.transform.localRotation = Quaternion.identity;
            }
            EditorUtility.SetDirty(body.transform);
        }
    }
}
