using System.Collections.Generic;
using KSP;
using Ksp2UnityTools.Editor.PlanetAuthoring.Tools;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Validation.Validators.Star
{
    /// <summary>
    /// Errors when a gas giant has no <c>assetKeyScaled</c> or the key does not resolve.
    /// </summary>
    /// <remarks>
    /// Gas giants render only as scaled-space objects. Without the scaled prefab nothing draws at any distance.
    /// </remarks>
    public sealed class GasGiantMissingScaledPrefabValidator : IPlanetValidator
    {
        /// <summary>Stable code identifying issues emitted by this validator.</summary>
        public const string Code = "GAS_GIANT_MISSING_SCALED_PREFAB";

        /// <inheritdoc />
        public BodyClassFlags AppliesTo => BodyClassFlags.GasGiant;

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(CoreCelestialBodyData body)
        {
            if (body?.Core?.data == null) yield break;
            string key = body.Core.data.assetKeyScaled;
            if (!string.IsNullOrEmpty(key) && AddressableKeyLookup.GetPrefab(key) != null) yield break;

            yield return new ValidationIssue(
                Code,
                ValidationSeverity.Error,
                string.IsNullOrEmpty(key)
                    ? $"Gas giant '{body.Core.data.bodyName}' has no assetKeyScaled. Gas giants render only via the scaled-space prefab."
                    : $"Gas giant '{body.Core.data.bodyName}' references assetKeyScaled '{key}' but no Addressables entry resolves. The body will never render.");
        }
    }
}
