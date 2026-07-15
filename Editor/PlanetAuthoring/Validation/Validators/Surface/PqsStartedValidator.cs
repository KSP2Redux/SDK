using System.Collections.Generic;
using KSP;
using KSP.Rendering.Planets;
using Ksp2UnityTools.Editor.PlanetAuthoring.Tools;
using Ksp2UnityTools.Editor.Validation;
using UnityEditor;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Validation.Validators.Surface
{
    /// <summary>
    /// Errors when the body's PQS is marked as started in the prefab.
    /// </summary>
    /// <remarks>
    /// The PQS should only be started at runtime, if it is started in the prefab this completely breaks the PQS system
    /// and can end up making the planet disappear alongside other planets and map view prefabs.
    /// </remarks>
    public sealed class PqsStartedValidator : IPlanetValidator
    {
        /// <summary>
        /// Stable code identifying issues emitted by this validator.
        /// </summary>
        public const string Code = "PQS_STARTED";
        /// <inheritdoc />
        public BodyClassFlags AppliesTo => BodyClassFlags.SolidSurface;


        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(CoreCelestialBodyData body)
        {
            if (body?.Core?.data == null) yield break;
            var pqs = BodyResolver.FindPqsIncludingAsset(body);
            if (pqs.isStarted)
            {
                yield return new ValidationIssue(Code, ValidationSeverity.Error,
                    $"PQS on '{body.Core.data.bodyName}' has `isStarted` set to true, click the fix button to set it to false.",
                    new[] { new ValidationFix("Set `isStarted` to false", () => DisableIsStarted(pqs))});
            }
        }

        private static void DisableIsStarted( PQS pqs)
        {
            pqs.isStarted = false;
            EditorUtility.SetDirty(pqs);
            AssetDatabase.SaveAssetIfDirty(pqs);
        }
    }
}