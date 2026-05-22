using System.Collections.Generic;
using KSP.Modules;
using Ksp2UnityTools.Editor.Validation;

namespace Ksp2UnityTools.Editor.PartAuthoring.Validation.Validators.ControlSurface
{
    /// <summary>
    /// Warns when both <c>ApplyLiftSurfaceForceAtBase</c> and
    /// <c>ApplyLiftSurfaceForceAtPivotMidpoint</c> are true.
    /// </summary>
    /// <remarks>
    /// Module_ControlSurface.MeshDataChanged uses if-elif ordering when picking the application
    /// point. The AtBase branch wins and AtPivotMidpoint is silently ignored. Setting both flags
    /// usually signals authoring confusion about which mode is in effect.
    /// </remarks>
    public sealed class ControlSurfaceForceModeBothValidator : IPartValidator
    {
        /// <summary>Stable code emitted when both flags are set.</summary>
        public const string Code = "CTRL_SURFACE_FORCE_MODE_BOTH";

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
                if (!(ctrl.ApplyLiftSurfaceForceAtBase && ctrl.ApplyLiftSurfaceForceAtPivotMidpoint))
                {
                    continue;
                }
                yield return new ValidationIssue(
                    Code,
                    ValidationSeverity.Warning,
                    "Both ApplyLiftSurfaceForceAtBase and ApplyLiftSurfaceForceAtPivotMidpoint are on. AtBase wins, AtPivotMidpoint is ignored.");
            }
        }
    }
}
