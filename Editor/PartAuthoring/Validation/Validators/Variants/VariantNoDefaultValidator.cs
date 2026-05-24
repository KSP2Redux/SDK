using System.Collections.Generic;
using Ksp2UnityTools.Editor.Validation;
using VSwift.Modules.Behaviours;
using VSwift.Modules.Data;

namespace Ksp2UnityTools.Editor.PartAuthoring.Validation.Validators.Variants
{
    /// <summary>
    /// Warns when a <c>VariantSet</c>'s entry in <c>DefaultActiveVariants</c> is missing or references an unknown variant.
    /// </summary>
    /// <remarks>
    /// The runtime falls back to <c>Variants[0]</c> when the default is unresolvable. Authoring the default explicitly makes the intent visible and stops the implicit-first behaviour from masking renames or reorders.
    /// </remarks>
    public sealed class VariantNoDefaultValidator : IPartValidator
    {
        /// <summary>Stable code emitted per set without a resolvable default.</summary>
        public const string Code = "VARIANT_NO_DEFAULT";

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(PartValidationContext context)
        {
            Data_PartSwitch data = VariantValidationHelper.FindData(context);
            if (data?.VariantSets == null)
            {
                yield break;
            }
            Module_PartSwitch module = VariantValidationHelper.FindModule(context);

            for (int i = 0; i < data.VariantSets.Count; i++)
            {
                var set = data.VariantSets[i];
                if (set?.Variants == null || set.Variants.Count == 0)
                {
                    continue;
                }
                string defaultId = (data.DefaultActiveVariants != null && i < data.DefaultActiveVariants.Count)
                    ? data.DefaultActiveVariants[i]
                    : null;
                bool resolved = !string.IsNullOrEmpty(defaultId) && set.Variants.Exists(v => v != null && v.VariantId == defaultId);
                if (resolved)
                {
                    continue;
                }

                string firstId = set.Variants[0]?.VariantId ?? string.Empty;
                int capturedIndex = i;
                string setId = set.VariantSetId;
                var fix = new ValidationFix(
                    $"Default to '{firstId}'",
                    () => VariantValidationHelper.RecordAndApply(module, "Set default variant", () =>
                    {
                        while (data.DefaultActiveVariants.Count <= capturedIndex)
                        {
                            data.DefaultActiveVariants.Add(string.Empty);
                        }
                        data.DefaultActiveVariants[capturedIndex] = firstId;
                    }));
                yield return new ValidationIssue(
                    Code,
                    ValidationSeverity.Warning,
                    string.IsNullOrEmpty(defaultId)
                        ? $"Variant set '{setId}' has no default variant. Runtime falls back to '{firstId}'."
                        : $"Variant set '{setId}' default '{defaultId}' is not a variant in the set. Runtime falls back to '{firstId}'.",
                    new[] { fix });
            }
        }
    }
}
