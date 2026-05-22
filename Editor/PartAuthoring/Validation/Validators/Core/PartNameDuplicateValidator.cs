using System;
using System.Collections.Generic;
using KSP;
using Ksp2UnityTools.Editor.Validation;
using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PartAuthoring.Validation.Validators.Core
{
    /// <summary>
    /// Errors when another part in the project asset database shares this part's
    /// <see cref="KSP.Sim.Definitions.PartData.partName" />.
    /// </summary>
    /// <remarks>
    /// PartName is the addressables key, the save-file reference, and the OAB catalog ID. Two
    /// parts with the same name resolve non-deterministically at load time. Full-scope: walks
    /// every prefab in the project asset database. Gated on
    /// <see cref="PartValidationContext.IsProjectScanAvailable" /> so SDK mode without a full
    /// Redux project stays quiet.
    /// </remarks>
    public sealed class PartNameDuplicateValidator : IPartValidator
    {
        /// <summary>Stable code emitted per duplicate-name group.</summary>
        public const string Code = "PART_NAME_DUPLICATE";

        /// <inheritdoc />
        public ValidatorCost Cost => ValidatorCost.Expensive;

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(PartValidationContext context)
        {
            if (context?.Part == null || !context.IsProjectScanAvailable)
            {
                yield break;
            }
            string ownName = context.Data?.partName;
            if (string.IsNullOrWhiteSpace(ownName))
            {
                yield break;
            }

            var matches = new List<string>();
            string selfPath = PartPathResolver.ResolvePrefabPath(context.Part);
            CollectProjectMatches(selfPath, ownName, matches);

            if (matches.Count == 0)
            {
                yield break;
            }
            string list = matches.Count <= 3
                ? string.Join(", ", matches)
                : $"{matches[0]}, {matches[1]}, ... ({matches.Count} total)";
            yield return new ValidationIssue(
                Code,
                ValidationSeverity.Error,
                $"partName '{ownName}' is shared with: {list}. Addressable keys collide at load.");
        }

        private static void CollectProjectMatches(string selfPath, string ownName, List<string> matches)
        {
            string[] guids = AssetDatabase.FindAssets("t:GameObject");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path))
                {
                    continue;
                }
                if (string.Equals(path, selfPath, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    continue;
                }
                CorePartData other = prefab.GetComponent<CorePartData>();
                if (other == null)
                {
                    continue;
                }
                string otherName = other.Data?.partName;
                if (string.Equals(otherName, ownName, StringComparison.Ordinal))
                {
                    matches.Add(path);
                }
            }
        }

    }
}
