using System.Collections.Generic;
using Ksp2UnityTools.Editor.Validation;
using VSwift.Modules.Data;
using VSwift.Modules.Behaviours;

namespace Ksp2UnityTools.Editor.PartAuthoring.Validation.Validators.Variants
{
    /// <summary>
    /// Errors when two <c>VariantSet</c> entries share the same <c>VariantSetId</c>. The runtime indexes variant sets by ID, so duplicates collapse non-deterministically.
    /// </summary>
    public sealed class VariantSetDupIdValidator : IPartValidator
    {
        /// <summary>Stable code emitted per duplicate-id group.</summary>
        public const string Code = "VARIANT_SET_DUP_ID";

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(PartValidationContext context)
        {
            Data_PartSwitch data = VariantValidationHelper.FindData(context);
            if (data?.VariantSets == null || data.VariantSets.Count < 2)
            {
                yield break;
            }

            var counts = new Dictionary<string, int>();
            foreach (var set in data.VariantSets)
            {
                if (set == null || string.IsNullOrEmpty(set.VariantSetId))
                {
                    continue;
                }
                counts[set.VariantSetId] = counts.TryGetValue(set.VariantSetId, out int n) ? n + 1 : 1;
            }

            Module_PartSwitch module = VariantValidationHelper.FindModule(context);

            foreach (var kv in counts)
            {
                if (kv.Value < 2)
                {
                    continue;
                }
                string duplicateId = kv.Key;
                var fix = new ValidationFix(
                    $"Rename later sets -> '{duplicateId}_2', '{duplicateId}_3', ...",
                    () => RenameDuplicates(module, data, duplicateId));
                yield return new ValidationIssue(
                    Code,
                    ValidationSeverity.Error,
                    $"Variant set ID '{duplicateId}' appears on {kv.Value} sets on this part.",
                    new[] { fix });
            }
        }

        private static void RenameDuplicates(Module_PartSwitch module, Data_PartSwitch data, string duplicateId)
        {
            if (data?.VariantSets == null)
            {
                return;
            }
            VariantValidationHelper.RecordAndApply(module, "Rename duplicate variant-set IDs", () =>
            {
                var taken = new HashSet<string>();
                foreach (var set in data.VariantSets)
                {
                    if (set != null && !string.IsNullOrEmpty(set.VariantSetId))
                    {
                        taken.Add(set.VariantSetId);
                    }
                }
                bool firstSeen = false;
                foreach (var set in data.VariantSets)
                {
                    if (set == null || set.VariantSetId != duplicateId)
                    {
                        continue;
                    }
                    if (!firstSeen)
                    {
                        firstSeen = true;
                        continue;
                    }
                    taken.Remove(set.VariantSetId);
                    string fresh = VariantValidationHelper.MakeUniqueId(duplicateId, taken);
                    set.VariantSetId = fresh;
                    taken.Add(fresh);
                }
            });
        }
    }
}
