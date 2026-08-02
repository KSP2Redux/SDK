using System.Collections.Generic;
using AwesomeTechnologies.VegetationSystem;
using KSP;
using KSP.Rendering.Planets;
using Ksp2UnityTools.Editor.PlanetAuthoring.Scatter;
using Ksp2UnityTools.Editor.PlanetAuthoring.Tools;
using Ksp2UnityTools.Editor.Validation;
using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Validation.Validators.Scatter
{
    /// <summary>
    /// Errors when a scatter system sits outside its body's PQS hierarchy.
    /// </summary>
    /// <remarks>
    /// <c>CelestialBodyBehavior</c> finds the system with <c>GetComponentInChildren</c> on the loaded
    /// local-space object, so a system parented anywhere else is never located at runtime. The
    /// preview is handed the system directly, so such a scene still renders scatter in the editor and
    /// the failure surfaces only in the shipped game.
    ///
    /// Scans the scene for scatter systems, which is sound because an authoring scene holds exactly
    /// one body.
    /// </remarks>
    public sealed class ScatterSystemNotUnderPqsValidator : IPlanetValidator
    {
        /// <summary>
        /// Stable code identifying issues emitted by this validator.
        /// </summary>
        public const string Code = "SCATTER_NOT_UNDER_PQS";

        /// <inheritdoc />
        public BodyClassFlags AppliesTo => BodyClassFlags.SolidSurface;

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(CoreCelestialBodyData body)
        {
            PQS pqs = BodyResolver.FindPqs(body);
            if (pqs == null)
                yield break;

            foreach (VegetationSystemPro system in UnityEngine.Object.FindObjectsByType<VegetationSystemPro>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (system == null || system.gameObject.scene != pqs.gameObject.scene)
                    continue;

                if (system.GetComponentInParent<PQS>(true) == pqs)
                    continue;

                var fixes = new[]
                {
                    new ValidationFix("Move it under the PQS", () => Reparent(system, pqs)),
                };

                yield return new ValidationIssue(
                    Code,
                    ValidationSeverity.Error,
                    $"Scatter system on '{system.gameObject.name}' is not under the body's PQS, so the game "
                    + "will never find it. It can still render in preview, which makes this look fine until it ships.",
                    fixes);
            }
        }

        private static void Reparent(VegetationSystemPro system, PQS pqs)
        {
            if (system == null || pqs == null)
                return;

            // worldPositionStays false, because the scatter field's origin comes from this transform.
            // Preserving the world pose would drag the whole field off the terrain.
            Undo.SetTransformParent(system.transform, pqs.transform, false, "Move Scatter System Under PQS");
            system.transform.localPosition = Vector3.zero;
            system.transform.localRotation = Quaternion.identity;
            EditorUtility.SetDirty(system);
        }
    }
}
