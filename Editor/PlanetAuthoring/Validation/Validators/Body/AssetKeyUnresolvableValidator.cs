using System.Collections.Generic;
using KSP;
using Ksp2UnityTools.Editor.PlanetAuthoring.Tools;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Validation.Validators.Body
{
    /// <summary>
    /// Warns when <c>assetKeyScaled</c> or <c>assetKeySimulation</c> does not resolve to any Addressables entry.
    /// </summary>
    /// <remarks>
    /// Unresolved keys load nothing at runtime, leaving the body with no scaled-space or no local-space rendering.
    /// One stable code per key so the artist sees which one is missing. Stars and gas giants skip the scaled-key
    /// check here because <c>STAR_MISSING_SCALED_PREFAB</c> / <c>GAS_GIANT_MISSING_SCALED_PREFAB</c> already cover
    /// it as an Error rather than a generic Warning.
    /// </remarks>
    public sealed class AssetKeyUnresolvableValidator : IPlanetValidator
    {
        /// <summary>Stable code emitted when <c>assetKeyScaled</c> does not resolve.</summary>
        public const string CodeScaled = "ASSETKEY_SCALED_UNRESOLVABLE";

        /// <summary>Stable code emitted when <c>assetKeySimulation</c> does not resolve.</summary>
        public const string CodeSimulation = "ASSETKEY_SIMULATION_UNRESOLVABLE";

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(CoreCelestialBodyData body)
        {
            if (body?.Core?.data == null) yield break;
            var data = body.Core.data;
            BodyClassFlags cls = BodyClassClassifier.Classify(body);
            bool starOrGasGiant = (cls & (BodyClassFlags.Star | BodyClassFlags.GasGiant)) != 0;

            if (!starOrGasGiant
                && !string.IsNullOrEmpty(data.assetKeyScaled)
                && AddressableKeyLookup.GetPrefab(data.assetKeyScaled) == null)
            {
                yield return new ValidationIssue(
                    CodeScaled,
                    ValidationSeverity.Warning,
                    $"assetKeyScaled '{data.assetKeyScaled}' does not resolve to any Addressables entry. The body will not render in scaled-space view.");
            }

            // Stars and gas giants don't ship a local-space prefab so the simulation key may legitimately
            // be empty for them. Only flag when set-but-unresolvable.
            if (!string.IsNullOrEmpty(data.assetKeySimulation) && AddressableKeyLookup.GetPrefab(data.assetKeySimulation) == null)
            {
                yield return new ValidationIssue(
                    CodeSimulation,
                    ValidationSeverity.Warning,
                    $"assetKeySimulation '{data.assetKeySimulation}' does not resolve to any Addressables entry. The body will not load its local-space prefab at runtime.");
            }
        }
    }
}
