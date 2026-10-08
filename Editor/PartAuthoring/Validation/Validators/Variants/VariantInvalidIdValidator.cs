using System.Collections.Generic;
using System.Text.RegularExpressions;
using Ksp2UnityTools.Editor.Validation;
using VSwift.Modules.Behaviours;
using VSwift.Modules.Data;
using VSwift.Modules.Variants;

namespace Ksp2UnityTools.Editor.PartAuthoring.Validation.Validators.Variants
{
    /// <summary>
    /// Errors when a <c>VariantSetId</c> or <c>VariantId</c> contains characters outside the safe identifier set <c>[A-Za-z0-9_]</c>.
    /// </summary>
    /// <remarks>
    /// Variant IDs flow through JSON keys, localization tags, and PAM dropdown bindings. Spaces, punctuation, and Unicode tend to break at least one of those.
    /// </remarks>
    public sealed class VariantInvalidIdValidator : IPartValidator
    {
        /// <summary>Stable code emitted per invalid identifier.</summary>
        public const string Code = "VARIANT_INVALID_ID";

        private static readonly Regex SAFE_PATTERN = new("^[A-Za-z0-9_]+$", RegexOptions.Compiled);
        private static readonly Regex UNSAFE_RUN = new("[^A-Za-z0-9_]+", RegexOptions.Compiled);

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(PartValidationContext context)
        {
            Data_PartSwitch data = VariantValidationHelper.FindData(context);
            if (data?.VariantSets == null)
            {
                yield break;
            }
            Module_PartSwitch module = VariantValidationHelper.FindModule(context);

            foreach (var set in data.VariantSets)
            {
                if (set == null)
                {
                    continue;
                }
                if (!string.IsNullOrEmpty(set.VariantSetId) && !SAFE_PATTERN.IsMatch(set.VariantSetId))
                {
                    string sluggified = Slugify(set.VariantSetId);
                    string oldId = set.VariantSetId;
                    VariantSet captured = set;
                    var fix = new ValidationFix(
                        $"Slugify -> '{sluggified}'",
                        () => VariantValidationHelper.RecordAndApply(module, "Slugify variant-set ID",
                            () => captured.VariantSetId = sluggified));
                    yield return new ValidationIssue(
                        Code,
                        ValidationSeverity.Error,
                        $"Variant set ID '{oldId}' has characters outside [A-Z a-z 0-9 _].",
                        new[] { fix });
                }
                if (set.Variants == null)
                {
                    continue;
                }
                foreach (var variant in set.Variants)
                {
                    if (variant == null || string.IsNullOrEmpty(variant.VariantId))
                    {
                        continue;
                    }
                    if (SAFE_PATTERN.IsMatch(variant.VariantId))
                    {
                        continue;
                    }
                    string sluggified = Slugify(variant.VariantId);
                    string oldId = variant.VariantId;
                    string setId = set.VariantSetId;
                    Variant capturedVariant = variant;
                    var fix = new ValidationFix(
                        $"Slugify -> '{sluggified}'",
                        () => VariantValidationHelper.RecordAndApply(module, "Slugify variant ID",
                            () => capturedVariant.VariantId = sluggified));
                    yield return new ValidationIssue(
                        Code,
                        ValidationSeverity.Error,
                        $"Variant ID '{oldId}' (in set '{setId}') has characters outside [A-Z a-z 0-9 _].",
                        new[] { fix });
                }
            }
        }

        private static string Slugify(string name)
        {
            string replaced = UNSAFE_RUN.Replace(name, "_");
            return replaced.Trim('_');
        }
    }
}
