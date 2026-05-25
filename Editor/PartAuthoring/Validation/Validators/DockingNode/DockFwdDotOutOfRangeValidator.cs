using System.Collections.Generic;
using KSP;
using KSP.Modules;
using Ksp2UnityTools.Editor.Validation;
using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PartAuthoring.Validation.Validators.DockingNode
{
    /// <summary>
    /// Errors when <c>AcquireMinFwdDot</c> is outside the valid dot-product range [-1, 1].
    /// </summary>
    /// <remarks>
    /// <c>AcquireMinFwdDot</c> is a cosine-similarity threshold against the docking ports' forward
    /// vectors. Values must lie within [-1, 1] because that is the full range of the dot product
    /// of two unit vectors. Outside that range the comparison either never triggers (&gt; 1) or
    /// always triggers (&lt; -1).
    /// </remarks>
    public sealed class DockFwdDotOutOfRangeValidator : IPartValidator
    {
        /// <summary>Stable code emitted when the dot threshold is out of range.</summary>
        public const string Code = "DOCK_FWDDOT_OUT_OF_RANGE";

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(PartValidationContext context)
        {
            CorePartData target = context?.Part;
            var modules = context?.ModuleDatas;
            if (modules == null || target == null)
            {
                yield break;
            }
            foreach (var module in modules)
            {
                if (module is not Data_DockingNode dock)
                {
                    continue;
                }
                if (dock.AcquireMinFwdDot >= -1f && dock.AcquireMinFwdDot <= 1f)
                {
                    continue;
                }
                Data_DockingNode captured = dock;
                float clamped = Mathf.Clamp(dock.AcquireMinFwdDot, -1f, 1f);
                var fix = new ValidationFix(
                    $"Clamp AcquireMinFwdDot to {clamped:0.###}",
                    () =>
                    {
                        Undo.RecordObject(target, "Clamp docking node fwd dot");
                        captured.AcquireMinFwdDot = clamped;
                        EditorUtility.SetDirty(target);
                    });
                yield return new ValidationIssue(
                    Code,
                    ValidationSeverity.Error,
                    $"DockingNode AcquireMinFwdDot = {dock.AcquireMinFwdDot:0.###}. Must lie in [-1, 1] (dot product of two unit vectors).",
                    new[] { fix });
            }
        }
    }
}
