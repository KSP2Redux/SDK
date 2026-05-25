using System.Collections.Generic;
using KSP;
using KSP.Modules;
using Ksp2UnityTools.Editor.Validation;
using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PartAuthoring.Validation.Validators.Wheel
{
    /// <summary>
    /// Warns when <c>Data_WheelSuspension.targetPosition</c> is outside <c>[0, 1]</c>.
    /// </summary>
    /// <remarks>
    /// Module_WheelSuspension writes <c>targetPosition</c> straight to
    /// <c>WheelCollider.suspensionAnchor</c>. Unity clamps internally, so the wheel still works,
    /// but the authored value no longer reflects the rest position. The Clamp fix snaps the value
    /// to the valid range.
    /// </remarks>
    public sealed class WheelSuspensionTargetOutOfRangeValidator : IPartValidator
    {
        /// <summary>Stable code emitted when targetPosition is out of range.</summary>
        public const string Code = "WHEEL_SUSPENSION_TARGET_OUT_OF_RANGE";

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
                if (module is not Data_WheelSuspension suspension)
                {
                    continue;
                }
                if (suspension.targetPosition >= 0f && suspension.targetPosition <= 1f)
                {
                    continue;
                }
                Data_WheelSuspension captured = suspension;
                float clamped = Mathf.Clamp01(suspension.targetPosition);
                var fix = new ValidationFix(
                    $"Clamp targetPosition -> {clamped:0.###}",
                    () =>
                    {
                        Undo.RecordObject(target, "Clamp wheel suspension target");
                        captured.targetPosition = clamped;
                        EditorUtility.SetDirty(target);
                    });
                yield return new ValidationIssue(
                    Code,
                    ValidationSeverity.Warning,
                    $"targetPosition is {suspension.targetPosition:0.###}. WheelCollider.suspensionAnchor requires [0, 1] - Unity clamps but the authored value loses meaning.",
                    new[] { fix });
            }
        }
    }
}
