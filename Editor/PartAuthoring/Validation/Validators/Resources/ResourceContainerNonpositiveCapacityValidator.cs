using System.Collections.Generic;
using Ksp2UnityTools.Editor.Validation;

namespace Ksp2UnityTools.Editor.PartAuthoring.Validation.Validators.Resources
{
    /// <summary>
    /// Errors when a resource container's <c>capacityUnits &lt;= 0</c>.
    /// </summary>
    /// <remarks>
    /// Zero capacity makes the container's slot a no-op - the runtime treats it as unable to hold
    /// the resource. Negative capacity is undefined behaviour in the resource graph.
    /// </remarks>
    public sealed class ResourceContainerNonpositiveCapacityValidator : IPartValidator
    {
        /// <summary>Stable code emitted per offending container.</summary>
        public const string Code = "RESCAP_NONPOSITIVE_CAPACITY";

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(PartValidationContext context)
        {
            var containers = context?.Data?.resourceContainers;
            if (containers == null)
            {
                yield break;
            }
            for (int i = 0; i < containers.Count; i++)
            {
                var c = containers[i];
                if (c.capacityUnits > 0.0)
                {
                    continue;
                }
                string label = string.IsNullOrEmpty(c.name) ? $"container {i}" : $"container '{c.name}'";
                yield return new ValidationIssue(
                    Code,
                    ValidationSeverity.Error,
                    $"Resource {label} has capacityUnits = {c.capacityUnits:0.###}. Must be > 0.");
            }
        }
    }
}
