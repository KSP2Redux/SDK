using System.Collections.Generic;
using KSP;
using KSP.Sim.Definitions;
using Ksp2UnityTools.Editor.Validation;
using UnityEditor;

namespace Ksp2UnityTools.Editor.PartAuthoring.Validation.Validators.Core
{
    /// <summary>
    /// Warns when <see cref="PartData.partName" /> contains capital letters.
    /// </summary>
    /// <remarks>
    /// Lowercase part names are recommended rather than required. Addressable keys are
    /// case-sensitive but Windows file names are not, so a name whose case drifts from its files
    /// and addresses still finds the files while the game misses the keys. The fix lowercases the
    /// name, which the inspector then offers to carry over to the files and addresses.
    /// </remarks>
    public class PartNameNotLowercaseValidator : IPartValidator
    {
        /// <summary>Stable code emitted when the part name has capital letters.</summary>
        public const string Code = "PART_NAME_NOT_LOWERCASE";

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(PartValidationContext context)
        {
            PartData data = context?.Data;
            if (data == null || string.IsNullOrWhiteSpace(data.partName))
                yield break;

            string lowercase = data.partName.ToLowerInvariant();
            if (lowercase == data.partName)
                yield break;

            CorePartData target = context.Part;
            var fix = new ValidationFix(
                $"Lowercase -> '{lowercase}'",
                () =>
                {
                    if (target == null)
                        return;
                    Undo.RecordObject(target, "Lowercase part name");
                    target.Data.partName = lowercase;
                    EditorUtility.SetDirty(target);
                });

            yield return new ValidationIssue(
                Code,
                ValidationSeverity.Warning,
                $"Part name '{data.partName}' has capital letters. Lowercase names are recommended, since addressable keys are case-sensitive and Windows file names are not.",
                new[] { fix });
        }
    }
}
