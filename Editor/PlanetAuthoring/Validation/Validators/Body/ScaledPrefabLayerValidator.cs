using System.Collections.Generic;
using KSP;
using Ksp2UnityTools.Editor.PlanetAuthoring.Tools;
using UnityEditor;
using UnityEngine;
using Ksp2UnityTools.Editor.Validation;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Validation.Validators.Body
{
    /// <summary>
    /// Errors when the Scaled prefab root is not on the <see cref="PlanetAuthoringLayers.Scaled" /> layer.
    /// </summary>
    /// <remarks>
    /// The scaled-space camera renders only objects on this layer. A body on any other layer will not
    /// appear in scaled space (orbit view, map, far-distance render).
    /// </remarks>
    public sealed class ScaledPrefabLayerValidator : IPlanetValidator
    {
        /// <summary>Stable code identifying issues emitted by this validator.</summary>
        public const string Code = "SCALED_PREFAB_LAYER";

        /// <summary>Stable code emitted when the project itself is missing the Scaled layer.</summary>
        public const string CodeLayerMissing = "SCALED_PREFAB_LAYER_PROJECT_MISSING";

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(CoreCelestialBodyData body)
        {
            if (body == null) yield break;
            int expected = LayerMask.NameToLayer(PlanetAuthoringLayers.Scaled);
            if (expected < 0)
            {
                yield return new ValidationIssue(
                    CodeLayerMissing,
                    ValidationSeverity.Error,
                    $"Project layer '{PlanetAuthoringLayers.Scaled}' is missing. The SDK import normally stamps it - re-import the SDK package to restore the layer.");
                yield break;
            }

            GameObject go = body.gameObject;
            if (go.layer == expected) yield break;

            string actualName = LayerMask.LayerToName(go.layer);
            string bodyName = body.Core?.data?.bodyName ?? go.name;
            GameObject captured = go;
            yield return new ValidationIssue(
                Code,
                ValidationSeverity.Error,
                $"Body '{bodyName}' Scaled prefab root is on layer '{actualName}' instead of '{PlanetAuthoringLayers.Scaled}'. The scaled-space camera renders only objects on this layer.",
                new[] { new ValidationFix($"Assign {PlanetAuthoringLayers.Scaled} layer", () => AssignExpectedLayer(captured, expected)) });
        }

        private static void AssignExpectedLayer(GameObject go, int layer)
        {
            Undo.RecordObject(go, $"Assign {PlanetAuthoringLayers.Scaled} layer");
            go.layer = layer;
            EditorUtility.SetDirty(go);
        }
    }
}
