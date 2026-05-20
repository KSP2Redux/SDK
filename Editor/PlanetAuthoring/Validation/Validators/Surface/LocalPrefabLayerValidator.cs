using System.Collections.Generic;
using KSP;
using KSP.Rendering.Planets;
using Ksp2UnityTools.Editor.PlanetAuthoring.Tools;
using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Validation.Validators.Surface
{
    /// <summary>
    /// Errors when the Local prefab root (PQS GameObject) is not on the
    /// <see cref="PlanetAuthoringLayers.Local" /> layer.
    /// </summary>
    /// <remarks>
    /// The local-space camera and terrain physics filter on this layer. A PQS on any other layer
    /// will not render in close-up view and its colliders will not interact with vessels or kerbals.
    /// </remarks>
    public sealed class LocalPrefabLayerValidator : IPlanetValidator
    {
        /// <summary>Stable code identifying issues emitted by this validator.</summary>
        public const string Code = "LOCAL_PREFAB_LAYER";

        /// <inheritdoc />
        public BodyClassFlags AppliesTo => BodyClassFlags.SolidSurface;

        /// <summary>Stable code emitted when the project itself is missing the Local layer.</summary>
        public const string CodeLayerMissing = "LOCAL_PREFAB_LAYER_PROJECT_MISSING";

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(CoreCelestialBodyData body)
        {
            if (body == null) yield break;
            int expected = LayerMask.NameToLayer(PlanetAuthoringLayers.Local);
            if (expected < 0)
            {
                yield return new ValidationIssue(
                    CodeLayerMissing,
                    ValidationSeverity.Error,
                    $"Project layer '{PlanetAuthoringLayers.Local}' is missing. The SDK import normally stamps it - re-import the SDK package to restore the layer.");
                yield break;
            }

            PQS pqs = BodyResolver.FindPqsIncludingAsset(body);
            if (pqs == null) yield break;
            GameObject go = pqs.gameObject;
            if (go.layer == expected) yield break;

            string actualName = LayerMask.LayerToName(go.layer);
            string bodyName = body.Core?.data?.bodyName ?? body.gameObject.name;
            GameObject captured = go;
            yield return new ValidationIssue(
                Code,
                ValidationSeverity.Error,
                $"PQS GameObject for body '{bodyName}' is on layer '{actualName}' instead of '{PlanetAuthoringLayers.Local}'. Local-space cameras and terrain colliders target this layer.",
                new[] { new ValidationFix($"Assign {PlanetAuthoringLayers.Local} layer", () => AssignExpectedLayer(captured, expected)) });
        }

        private static void AssignExpectedLayer(GameObject go, int layer)
        {
            Undo.RecordObject(go, $"Assign {PlanetAuthoringLayers.Local} layer");
            go.layer = layer;
            EditorUtility.SetDirty(go);
        }
    }
}
