using System.Collections.Generic;
using KSP;
using Ksp2UnityTools.Editor.PlanetAuthoring.Tools;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Validation.Validators.Star
{
    /// <summary>
    /// Errors when a star has no <c>assetKeyScaled</c> or the key does not resolve.
    /// </summary>
    /// <remarks>
    /// Stars render entirely as scaled-space objects. Without the scaled prefab the star never appears in either
    /// the orbital view or the system-wide light setup.
    /// </remarks>
    public sealed class StarMissingScaledPrefabValidator : IPlanetValidator
    {
        /// <summary>Stable code identifying issues emitted by this validator.</summary>
        public const string Code = "STAR_MISSING_SCALED_PREFAB";

        /// <inheritdoc />
        public BodyClassFlags AppliesTo => BodyClassFlags.Star;

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
                    ? $"Star '{body.Core.data.bodyName}' has no assetKeyScaled. Stars render only via the scaled-space prefab. Without a key the star will never appear."
                    : $"Star '{body.Core.data.bodyName}' references assetKeyScaled '{key}' but no Addressables entry resolves. The star will never render.");
        }
    }
}
