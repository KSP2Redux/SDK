using System.Collections.Generic;
using System.Text.RegularExpressions;
using KSP;
using KSP.Sim.Definitions;
using Ksp2UnityTools.Editor.Validation;
using UnityEditor;

namespace Ksp2UnityTools.Editor.PartAuthoring.Validation.Validators.Core
{
    /// <summary>
    /// Errors when <see cref="PartData.partName" /> contains characters outside the safe set.
    /// </summary>
    /// <remarks>
    /// Part names appear in addressable keys, save-file references, and JSON sidecars. Characters
    /// outside <c>[a-z0-9_.]</c> cause platform-specific tooling friction (URL encoding, path
    /// escaping, MoonSharp identifier collisions). The slugify fix lowercases, replaces unsafe
    /// runs with single underscores, and trims trailing punctuation.
    /// </remarks>
    public sealed class PartNameInvalidCharsValidator : IPartValidator
    {
        /// <summary>Stable code emitted for any character outside the safe set.</summary>
        public const string Code = "PART_NAME_INVALID_CHARS";

        private static readonly Regex SAFE_PATTERN = new("^[a-z0-9_.\\-]+$", RegexOptions.Compiled);
        private static readonly Regex UNSAFE_RUN = new("[^a-z0-9_.\\-]+", RegexOptions.Compiled);

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(PartValidationContext context)
        {
            PartData data = context?.Data;
            if (data == null || string.IsNullOrWhiteSpace(data.partName))
            {
                yield break;
            }
            if (SAFE_PATTERN.IsMatch(data.partName))
            {
                yield break;
            }

            string slugified = Slugify(data.partName);
            CorePartData target = context.Part;
            var fix = new ValidationFix(
                $"Slugify -> '{slugified}'",
                () =>
                {
                    if (target == null) return;
                    Undo.RecordObject(target, "Slugify part name");
                    target.Data.partName = slugified;
                    EditorUtility.SetDirty(target);
                });

            yield return new ValidationIssue(
                Code,
                ValidationSeverity.Error,
                $"Part name '{data.partName}' has characters outside [a-z 0-9 _ . -]. Used in addressable keys and save files.",
                new[] { fix });
        }

        private static string Slugify(string name)
        {
            string lowered = name.ToLowerInvariant();
            string replaced = UNSAFE_RUN.Replace(lowered, "_");
            return replaced.Trim('_', '.');
        }
    }
}
