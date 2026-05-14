using System.Collections.Generic;
using KSP;
using KSP.Rendering.Planets;
using Ksp2UnityTools.Editor.PlanetAuthoring.Tools;
using UnityEditor;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Validation.Validators
{
    /// <summary>
    /// Errors when the scaled-to-local blend window is wider than the transition distance.
    /// </summary>
    /// <remarks>
    /// The blend should fit inside the transition zone. When blend exceeds transition the math has no purely-scaled or purely-local region and the renderer flickers between the two.
    /// </remarks>
    public sealed class ScaledTransitionGapValidator : IPlanetValidator
    {
        /// <summary>Stable code identifying issues emitted by this validator.</summary>
        public const string Code = "SCALED_TRANSITION_GAP";

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(CoreCelestialBodyData body)
        {
            if (body?.Core?.data == null || !body.Core.data.hasSolidSurface)
                yield break;
            var pqs = BodyResolver.FindPqsIncludingAsset(body);
            if (pqs == null || pqs.data == null || pqs.data.heightMapInfo == null)
                yield break;
            var info = pqs.data.heightMapInfo;
            if (info.scaledToLocalTransition >= info.scaledToLocalBlend)
                yield break;

            PQSData pqsData = pqs.data;
            var fixes = new[]
            {
                new ValidationFix("Swap values", () => SwapValues(pqsData)),
            };
            string message = $"Scaled-to-Local Transition ({info.scaledToLocalTransition:0.#} m) is less than Scaled-to-Local Blend ({info.scaledToLocalBlend:0.#} m). The blend window is wider than the transition zone.";
            yield return new ValidationIssue(Code, ValidationSeverity.Error, message, fixes);
        }

        private static void SwapValues(PQSData pqsData)
        {
            if (pqsData == null || pqsData.heightMapInfo == null)
                return;
            Undo.RecordObject(pqsData, "Swap scaled-to-local values");
            (pqsData.heightMapInfo.scaledToLocalTransition, pqsData.heightMapInfo.scaledToLocalBlend) =
                (pqsData.heightMapInfo.scaledToLocalBlend, pqsData.heightMapInfo.scaledToLocalTransition);
            EditorUtility.SetDirty(pqsData);
            AssetDatabase.SaveAssetIfDirty(pqsData);
        }
    }
}
