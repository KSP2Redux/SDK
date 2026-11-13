using System.Collections.Generic;
using System.IO;
using Ksp2UnityTools.Editor.TechTreeAuthoring;
using Ksp2UnityTools.Editor.Validation;
using UnityEditor;

namespace Ksp2UnityTools.Editor.PartAuthoring.Validation.Validators.Core
{
    /// <summary>
    /// Checks picker category, icon presence and local tech-tree membership for visible authored parts.
    /// </summary>
    public class PartPickerDataValidator : IPartValidator
    {
        /// <inheritdoc />
        public ValidatorCost Cost => ValidatorCost.Expensive;

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(PartValidationContext context)
        {
            if (context?.Part == null || context.Data == null || context.Data.partHideMode.ToString().StartsWith("Hidden"))
                yield break;

            string partName = context.Data.partName;
            if (context.Data.category.ToString() == "none")
            {
                yield return new ValidationIssue("PART_PICKER_CATEGORY_MISSING", ValidationSeverity.Warning,
                    $"Part '{partName}' category is none. Expected an authored picker category or an explicitly hidden part.");
            }
            string prefabPath = PartPathResolver.ResolvePrefabPath(context.Part);
            if (!string.IsNullOrEmpty(prefabPath) && !string.IsNullOrEmpty(partName))
            {
                string icon = Path.Combine(Path.GetDirectoryName(prefabPath) ?? "", $"{partName}_icon.png");
                if (!File.Exists(icon))
                {
                    yield return new ValidationIssue("PART_PICKER_ICON_MISSING", ValidationSeverity.Warning,
                        $"Part '{partName}' has no baked icon at '{icon}'. Expected the canonical icon file. Check any externally supplied icon.");
                }
            }
            if (!context.IsProjectScanAvailable || string.IsNullOrEmpty(partName))
                yield break;

            string[] trees = AssetDatabase.FindAssets("t:TechTreeAsset");
            if (trees.Length == 0)
                yield break;
            foreach (string guid in trees)
            {
                TechTreeAsset tree = AssetDatabase.LoadAssetAtPath<TechTreeAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (tree == null || tree.Nodes == null)
                    continue;
                foreach (TechTreeNode node in tree.Nodes)
                {
                    if (node?.Data?.UnlockedPartsIDs == null)
                        continue;
                    foreach (string unlocked in node.Data.UnlockedPartsIDs)
                    {
                        if (unlocked == partName)
                            yield break;
                    }
                }
            }
            yield return new ValidationIssue("PART_PICKER_TECH_NODE_MISSING", ValidationSeverity.Warning,
                $"Part '{partName}' occurs in 0 local authored tech nodes. Expected at least 1 node or a hidden part. Check external trees and runtime patches.");
        }
    }
}
