using System.Collections.Generic;
using System.IO;
using Ksp2UnityTools.Editor.PartAuthoring.Validation.Validators;
using Ksp2UnityTools.Editor.Validation;

namespace Ksp2UnityTools.Editor.PartAuthoring.Validation.Validators.Core
{
    /// <summary>
    /// Info when the part's baked icon PNG is older than the prefab itself.
    /// </summary>
    /// <remarks>
    /// PartIconBaker writes <c>{prefabDir}/{partName}_icon.png</c>. If the prefab's modification
    /// time is newer than the icon's, the icon may not reflect the current geometry. Info
    /// severity because most edits do not actually change the rendered silhouette - the warning
    /// is a "you may want to re-bake" nudge, not a runtime contract violation.
    /// </remarks>
    public sealed class PartIconOutOfDateValidator : IPartValidator
    {
        /// <summary>Stable code emitted when the icon mtime is older than the prefab mtime.</summary>
        public const string Code = "PART_ICON_OUT_OF_DATE";

        /// <inheritdoc />
        public ValidatorCost Cost => ValidatorCost.Expensive;

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(PartValidationContext context)
        {
            if (context?.Part == null)
            {
                yield break;
            }
            string prefabPath = PartPathResolver.ResolvePrefabPath(context.Part);
            if (string.IsNullOrEmpty(prefabPath) || !File.Exists(prefabPath))
            {
                yield break;
            }
            string partName = context.Data?.partName;
            if (string.IsNullOrEmpty(partName))
            {
                yield break;
            }
            string prefabDir = Path.GetDirectoryName(prefabPath);
            if (string.IsNullOrEmpty(prefabDir))
            {
                yield break;
            }
            string iconPath = Path.Combine(prefabDir, $"{partName}_icon.png").Replace('\\', '/');
            if (!File.Exists(iconPath))
            {
                yield break;
            }
            System.DateTime prefabMtime = File.GetLastWriteTimeUtc(prefabPath);
            System.DateTime iconMtime = File.GetLastWriteTimeUtc(iconPath);
            if (iconMtime >= prefabMtime)
            {
                yield break;
            }
            yield return new ValidationIssue(
                Code,
                ValidationSeverity.Info,
                $"Icon was baked {(prefabMtime - iconMtime).TotalMinutes:0} min before the most recent prefab edit. Consider re-baking via Quick Tools > Bake Icon.");
        }
    }
}
