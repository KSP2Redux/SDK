using System.Collections.Generic;
using Ksp2UnityTools.Editor.Validation;

namespace Ksp2UnityTools.Editor.PartAuthoring.Validation.Validators.Resources
{
    /// <summary>
    /// Errors when two entries in <c>PartData.resourceContainers</c> share the same name.
    /// </summary>
    /// <remarks>
    /// Module lookups against the part's container group resolve by name. Two rows with the same
    /// name produce a non-deterministic resolution that depends on iteration order, and the
    /// runtime overwrites one with the other when computing per-resource totals.
    /// </remarks>
    public sealed class ResourceContainerDuplicateNameValidator : IPartValidator
    {
        /// <summary>Stable code emitted per duplicate-name group.</summary>
        public const string Code = "RESCAP_DUPLICATE_NAME";

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(PartValidationContext context)
        {
            var containers = context?.Data?.resourceContainers;
            if (containers == null || containers.Count < 2)
            {
                yield break;
            }
            var counts = new Dictionary<string, int>();
            foreach (var c in containers)
            {
                if (string.IsNullOrEmpty(c.name))
                {
                    continue;
                }
                counts[c.name] = counts.TryGetValue(c.name, out int n) ? n + 1 : 1;
            }
            foreach (var kv in counts)
            {
                if (kv.Value < 2)
                {
                    continue;
                }
                yield return new ValidationIssue(
                    Code,
                    ValidationSeverity.Error,
                    $"Resource '{kv.Key}' appears in {kv.Value} containers on this part. Module lookups become non-deterministic.");
            }
        }
    }
}
