using System.Collections.Generic;
using KSP;
using Ksp2UnityTools.Editor.Validation;
using UnityEditor;

namespace Ksp2UnityTools.Editor.PartAuthoring.Validation.Validators.Resources
{
    /// <summary>
    /// Errors when a resource container's <c>initialUnits &gt; capacityUnits</c>.
    /// </summary>
    /// <remarks>
    /// The runtime clamps the stored amount to capacity but the authored values diverge from the
    /// runtime state. UI and JSON exports reflect the inflated initial value while the part
    /// actually holds capacity. The Clamp fix sets <c>initialUnits = capacityUnits</c>.
    /// </remarks>
    public sealed class ResourceContainerInitialOverCapacityValidator : IPartValidator
    {
        /// <summary>Stable code emitted per offending container.</summary>
        public const string Code = "RESCAP_INITIAL_OVER_CAPACITY";

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(PartValidationContext context)
        {
            CorePartData target = context?.Part;
            var containers = context?.Data?.resourceContainers;
            if (containers == null || target == null)
            {
                yield break;
            }
            for (int i = 0; i < containers.Count; i++)
            {
                var c = containers[i];
                if (c.initialUnits <= c.capacityUnits)
                {
                    continue;
                }
                int capturedIndex = i;
                var fix = new ValidationFix(
                    $"Clamp initial -> {c.capacityUnits:0.###}",
                    () =>
                    {
                        Undo.RecordObject(target, "Clamp resource initial to capacity");
                        var cc = target.Data.resourceContainers[capturedIndex];
                        cc.initialUnits = cc.capacityUnits;
                        EditorUtility.SetDirty(target);
                    });
                string label = string.IsNullOrEmpty(c.name) ? $"container {i}" : $"container '{c.name}'";
                yield return new ValidationIssue(
                    Code,
                    ValidationSeverity.Error,
                    $"Resource {label} initialUnits ({c.initialUnits:0.###}) > capacityUnits ({c.capacityUnits:0.###}).",
                    new[] { fix });
            }
        }
    }
}
