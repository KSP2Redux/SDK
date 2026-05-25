using System.Collections.Generic;
using KSP;
using KSP.Modules;
using Ksp2UnityTools.Editor.Validation;
using UnityEditor;

namespace Ksp2UnityTools.Editor.PartAuthoring.Validation.Validators.DockingNode
{
    /// <summary>
    /// Errors when <c>CaptureRange &gt; AcquireRange</c>.
    /// </summary>
    /// <remarks>
    /// AcquireRange is the outer distance at which two ports begin pulling toward each other.
    /// CaptureRange is the inner distance at which the magnetic lock fires. If capture is wider
    /// than acquire the ports never reach the acquisition phase before locking, so the lock fires
    /// without alignment. The Swap fix exchanges the two values.
    /// </remarks>
    public sealed class DockCaptureGtAcquireValidator : IPartValidator
    {
        /// <summary>Stable code emitted when CaptureRange exceeds AcquireRange.</summary>
        public const string Code = "DOCK_CAPTURE_GT_ACQUIRE";

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
                if (dock.CaptureRange <= dock.AcquireRange)
                {
                    continue;
                }
                Data_DockingNode captured = dock;
                var fix = new ValidationFix(
                    $"Swap CaptureRange / AcquireRange ({dock.CaptureRange:0.###} / {dock.AcquireRange:0.###})",
                    () =>
                    {
                        Undo.RecordObject(target, "Swap docking node capture/acquire range");
                        (captured.CaptureRange, captured.AcquireRange) = (captured.AcquireRange, captured.CaptureRange);
                        EditorUtility.SetDirty(target);
                    });
                yield return new ValidationIssue(
                    Code,
                    ValidationSeverity.Error,
                    $"DockingNode CaptureRange ({dock.CaptureRange:0.###}) > AcquireRange ({dock.AcquireRange:0.###}). Capture fires before acquisition aligns the ports.",
                    new[] { fix });
            }
        }
    }
}
