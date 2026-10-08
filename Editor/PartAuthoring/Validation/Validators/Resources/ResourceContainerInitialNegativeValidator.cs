using System.Collections.Generic;
using KSP;
using Ksp2UnityTools.Editor.Validation;
using UnityEditor;

namespace Ksp2UnityTools.Editor.PartAuthoring.Validation.Validators.Resources
{
    /// <summary>
    /// Errors when a resource container's <c>initialUnits &lt; 0</c>.
    /// </summary>
    /// <remarks>
    /// Negative stored amounts are not a representable state for the resource graph. The Clamp
    /// fix sets <c>initialUnits = 0</c>.
    /// </remarks>
    public sealed class ResourceContainerInitialNegativeValidator : IPartValidator
    {
        /// <summary>Stable code emitted per offending container.</summary>
        public const string Code = "RESCAP_INITIAL_NEGATIVE";

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
                if (c.initialUnits >= 0.0)
                {
                    continue;
                }
                int capturedIndex = i;
                var fix = new ValidationFix(
                    "Clamp initial -> 0",
                    () =>
                    {
                        Undo.RecordObject(target, "Clamp resource initial to zero");
                        var cc = target.Data.resourceContainers[capturedIndex];
                        cc.initialUnits = 0.0;
                        EditorUtility.SetDirty(target);
                    });
                string label = string.IsNullOrEmpty(c.name) ? $"container {i}" : $"container '{c.name}'";
                yield return new ValidationIssue(
                    Code,
                    ValidationSeverity.Error,
                    $"Resource {label} initialUnits is {c.initialUnits:0.###}.",
                    new[] { fix });
            }
        }
    }
}
