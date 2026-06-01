using System.Collections.Generic;
using KSP.Game.Missions.Definitions;
using Ksp2UnityTools.Editor.Validation;

namespace Ksp2UnityTools.Editor.MissionAuthoring.Validation.Validators
{
    /// <summary>
    /// Errors when <see cref="MissionData.ID" /> is null or whitespace.
    /// </summary>
    /// <remarks>
    /// The runtime keys missions by ID for save-file references, addressables, and the
    /// MissionGranter lookup. An empty ID produces a mission that cannot be loaded or referenced.
    /// </remarks>
    public sealed class MissionIdEmptyValidator : IMissionValidator
    {
        /// <summary>Stable code emitted when the mission ID is empty or whitespace.</summary>
        public const string Code = "MISSION_ID_EMPTY";

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(MissionValidationContext context)
        {
            MissionData data = context?.Data;
            if (data == null) yield break;
            if (!string.IsNullOrWhiteSpace(data.ID)) yield break;
            yield return new ValidationIssue(
                Code,
                ValidationSeverity.Error,
                "Mission ID is empty. Set a unique ID to identify this mission in save files and the addressables catalog.");
        }
    }
}
