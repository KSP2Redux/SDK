using System.Collections.Generic;
using KSP;
using Ksp2UnityTools.Editor.PlanetAuthoring.Tools;
using UnityEngine;
using Ksp2UnityTools.Editor.Validation;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Validation.Validators.Star
{
    /// <summary>
    /// Warns when a star's scaled prefab contains no directional <see cref="Light" /> component.
    /// </summary>
    /// <remarks>
    /// SunLightData picks the brightest directional Light reachable from the star's hierarchy. A scaled prefab
    /// without one either ends up unlit or, worse, picks an unrelated light in the scene. Point or spot lights
    /// don't satisfy the runtime contract and are treated as "no light".
    /// </remarks>
    public sealed class StarNoLightSourceValidator : IPlanetValidator
    {
        /// <summary>Stable code identifying issues emitted by this validator.</summary>
        public const string Code = "STAR_NO_LIGHT_SOURCE";

        /// <inheritdoc />
        public BodyClassFlags AppliesTo => BodyClassFlags.Star;

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(CoreCelestialBodyData body)
        {
            if (body?.Core?.data == null) yield break;
            string key = body.Core.data.assetKeyScaled;
            if (string.IsNullOrEmpty(key)) yield break;
            GameObject scaled = AddressableKeyLookup.GetPrefab(key);
            if (scaled == null) yield break;
            foreach (Light light in scaled.GetComponentsInChildren<Light>(includeInactive: true))
            {
                if (light.type == LightType.Directional) yield break;
            }

            yield return new ValidationIssue(
                Code,
                ValidationSeverity.Warning,
                $"Star '{body.Core.data.bodyName}' has no directional Light in its scaled prefab '{scaled.name}'. SunLightData may pick a wrong light or none at runtime.");
        }
    }
}
