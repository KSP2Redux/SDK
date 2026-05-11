using System.Collections.Generic;
using KSP;
using KSP.Game.Science;
using Ksp2UnityTools.Editor.PlanetAuthoring.Science;
using Ksp2UnityTools.Editor.ScriptableObjects;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Validation.Validators
{
    /// <summary>
    /// Flags discoverables with a blank or whitespace ScienceRegionId.
    /// </summary>
    /// <remarks>
    /// An empty Id never matches a region row at runtime so the discoverable is effectively
    /// invisible. PlaceDiscoverableTool generates unique Ids on creation. An empty Id usually
    /// means the artist cleared the field during editing.
    /// </remarks>
    public sealed class DiscoverableEmptyIdValidator : IPlanetValidator
    {
        /// <summary>Stable code identifying issues emitted by this validator.</summary>
        public const string Code = "DISCOVERABLE_EMPTY_ID";

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(CoreCelestialBodyData body)
        {
            string bodyName = body?.Data?.bodyName;
            if (string.IsNullOrEmpty(bodyName)) yield break;
            ScienceRegionData data = ScienceRegionAssetLocator.FindForBody(bodyName);
            if (data?.discoverables == null) yield break;

            int empty = 0;
            for (int i = 0; i < data.discoverables.Count; i++)
                if (string.IsNullOrWhiteSpace(data.discoverables[i]?.ScienceRegionId))
                    empty++;
            if (empty == 0) yield break;

            yield return new ValidationIssue(
                Code,
                ValidationSeverity.Warning,
                $"{empty} discoverable(s) on '{bodyName}' have an empty ScienceRegionId. " +
                $"Open the Science Region inspector's Discoverables section and rename them.");
        }
    }
}
