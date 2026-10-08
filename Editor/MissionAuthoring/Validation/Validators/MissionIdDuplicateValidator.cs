using System;
using System.Collections.Generic;
using Ksp2UnityTools.Editor.Validation;
using UnityEditor;

namespace Ksp2UnityTools.Editor.MissionAuthoring.Validation.Validators
{
    /// <summary>
    /// Errors when another Mission asset in the project shares this mission's
    /// <c>MissionData.ID</c>.
    /// </summary>
    /// <remarks>
    /// Mission ID is the addressables key and the save-file reference. Two missions with the same
    /// ID resolve non-deterministically at load time. Expensive-scope walks every Mission asset
    /// via <see cref="AssetDatabase.FindAssets" />. Gated on
    /// <see cref="MissionValidationContext.IsProjectScanAvailable" /> so SDK mode without a full
    /// project stays quiet.
    /// </remarks>
    public sealed class MissionIdDuplicateValidator : IMissionValidator
    {
        /// <summary>Stable code emitted per duplicate-ID group.</summary>
        public const string Code = "MISSION_ID_DUPLICATE";

        /// <inheritdoc />
        public ValidatorCost Cost => ValidatorCost.Expensive;

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(MissionValidationContext context)
        {
            if (context?.Mission == null || !context.IsProjectScanAvailable) yield break;
            string ownId = context.Data?.ID;
            if (string.IsNullOrWhiteSpace(ownId)) yield break;

            string selfPath = AssetDatabase.GetAssetPath(context.Mission);
            var matches = new List<string>();
            CollectProjectMatches(selfPath, ownId, matches);

            if (matches.Count == 0) yield break;

            string list = matches.Count <= 3
                ? string.Join(", ", matches)
                : $"{matches[0]}, {matches[1]}, ... ({matches.Count} total)";
            yield return new ValidationIssue(
                Code,
                ValidationSeverity.Error,
                $"Mission ID '{ownId}' is shared with: {list}. Addressable keys collide at load.");
        }

        private static void CollectProjectMatches(string selfPath, string ownId, List<string> matches)
        {
            string[] guids = AssetDatabase.FindAssets("t:Mission");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path)) continue;
                if (string.Equals(path, selfPath, StringComparison.OrdinalIgnoreCase)) continue;
                var other = AssetDatabase.LoadAssetAtPath<Mission>(path);
                if (other == null || other.missionData == null) continue;
                if (string.Equals(other.missionData.ID, ownId, StringComparison.Ordinal))
                {
                    matches.Add(path);
                }
            }
        }
    }
}
