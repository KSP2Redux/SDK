using System.Collections.Generic;
using KSP.Modules;
using Ksp2UnityTools.Editor.Validation;

namespace Ksp2UnityTools.Editor.PartAuthoring.Validation.Validators.ControlSurface
{
    /// <summary>
    /// Warns when <c>CtrlSurfaceMinimumLockAngleForControl &gt; CtrlSurfaceRange</c>.
    /// </summary>
    /// <remarks>
    /// Deploy angle can never exceed the lock threshold, so control input stays locked at neutral
    /// and the surface ignores the player's input axis.
    /// </remarks>
    public sealed class ControlSurfaceMinLockGtRangeValidator : IPartValidator
    {
        /// <summary>Stable code emitted when the lock threshold exceeds the deploy range.</summary>
        public const string Code = "CTRL_SURFACE_MIN_LOCK_GT_RANGE";

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(PartValidationContext context)
        {
            var modules = context?.Modules;
            if (modules == null)
            {
                yield break;
            }
            foreach (var module in modules)
            {
                if (module is not Data_ControlSurface ctrl)
                {
                    continue;
                }
                if (ctrl.CtrlSurfaceMinimumLockAngleForControl <= ctrl.CtrlSurfaceRange)
                {
                    continue;
                }
                yield return new ValidationIssue(
                    Code,
                    ValidationSeverity.Warning,
                    $"CtrlSurfaceMinimumLockAngleForControl ({ctrl.CtrlSurfaceMinimumLockAngleForControl:0.##}°) > CtrlSurfaceRange ({ctrl.CtrlSurfaceRange:0.##}°). The surface stays locked at neutral.");
            }
        }
    }
}
