using System.Collections.Generic;
using Ksp2UnityTools.Editor.Validation;
using VSwift.Modules.Behaviours;
using VSwift.Modules.Data;
using VSwift.Modules.Variants;

namespace Ksp2UnityTools.Editor.PartAuthoring.Validation.Validators.Variants
{
    /// <summary>
    /// Errors when two <c>Variant</c> entries within the same <c>VariantSet</c> share the same <c>VariantId</c>.
    /// </summary>
    public sealed class VariantDupIdValidator : IPartValidator
    {
        /// <summary>Stable code emitted per duplicate-id group within a set.</summary>
        public const string Code = "VARIANT_DUP_ID";

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
                if (set?.Variants == null || set.Variants.Count < 2)
                {
                    continue;
                }
                var counts = new Dictionary<string, int>();
                foreach (var variant in set.Variants)
                {
                    if (variant == null || string.IsNullOrEmpty(variant.VariantId))
                    {
                        continue;
                    }
                    counts[variant.VariantId] = counts.TryGetValue(variant.VariantId, out int n) ? n + 1 : 1;
                }
                foreach (var kv in counts)
                {
                    if (kv.Value < 2)
                    {
                        continue;
                    }
                    string setId = set.VariantSetId;
                    string dupId = kv.Key;
                    VariantSet capturedSet = set;
                    var fix = new ValidationFix(
                        $"Rename later variants -> '{dupId}_2', '{dupId}_3', ...",
                        () => RenameDuplicates(module, capturedSet, dupId));
                    yield return new ValidationIssue(
                        Code,
                        ValidationSeverity.Error,
                        $"Variant ID '{dupId}' appears on {kv.Value} variants in set '{setId}'.",
                        new[] { fix });
                }
            }
        }

        private static void RenameDuplicates(Module_PartSwitch module, VariantSet set, string duplicateId)
        {
            if (set?.Variants == null)
            {
                return;
            }
            VariantValidationHelper.RecordAndApply(module, "Rename duplicate variant IDs", () =>
            {
                var taken = new HashSet<string>();
                foreach (var v in set.Variants)
                {
                    if (v != null && !string.IsNullOrEmpty(v.VariantId))
                    {
                        taken.Add(v.VariantId);
                    }
                }
                bool firstSeen = false;
                foreach (var v in set.Variants)
                {
                    if (v == null || v.VariantId != duplicateId)
                    {
                        continue;
                    }
                    if (!firstSeen)
                    {
                        firstSeen = true;
                        continue;
                    }
                    taken.Remove(v.VariantId);
                    string fresh = VariantValidationHelper.MakeUniqueId(duplicateId, taken);
                    v.VariantId = fresh;
                    taken.Add(fresh);
                }
            });
        }
    }
}
