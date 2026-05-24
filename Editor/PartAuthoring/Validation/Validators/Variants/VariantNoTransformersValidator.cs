using System.Collections.Generic;
using Ksp2UnityTools.Editor.Validation;
using VSwift.Modules.Data;

namespace Ksp2UnityTools.Editor.PartAuthoring.Validation.Validators.Variants
{
    /// <summary>
    /// Info-level note when a variant has no transformers attached.
    /// </summary>
    /// <remarks>
    /// An empty transformer list is sometimes intentional - it's the "off" sibling in a pair where the other variant turns something on. The note exists as a glance check, not as a defect signal, so it ships as Info with no auto-fix.
    /// </remarks>
    public sealed class VariantNoTransformersValidator : IPartValidator
    {
        /// <summary>Stable code emitted per variant with zero transformers.</summary>
        public const string Code = "VARIANT_NO_TRANSFORMERS";

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(PartValidationContext context)
        {
            Data_PartSwitch data = VariantValidationHelper.FindData(context);
            if (data?.VariantSets == null)
            {
                yield break;
            }
            foreach (var set in data.VariantSets)
            {
                if (set?.Variants == null)
                {
                    continue;
                }
                foreach (var variant in set.Variants)
                {
                    if (variant == null)
                    {
                        continue;
                    }
                    if (variant.Transformers != null && variant.Transformers.Count > 0)
                    {
                        continue;
                    }
                    yield return new ValidationIssue(
                        Code,
                        ValidationSeverity.Info,
                        $"Variant '{variant.VariantId}' in set '{set.VariantSetId}' has no transformers. (Intentional when paired with a transforming sibling; flagging for review.)");
                }
            }
        }
    }
}
