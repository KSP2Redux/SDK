using System.Collections.Generic;
using KSP.Modules;
using Ksp2UnityTools.Editor.Validation;

namespace Ksp2UnityTools.Editor.PartAuthoring.Validation.Validators.ControlSurface
{
    /// <summary>
    /// Errors when <c>CtrlTransformDir == CtrlTransformRotAxis</c>.
    /// </summary>
    /// <remarks>
    /// The control surface pivots in the same direction it produces lift. As the surface deflects
    /// the lift vector does not change, so the control surface contributes no torque even though
    /// the geometry rotates.
    /// </remarks>
    public sealed class ControlSurfaceAxisDegenerateValidator : IPartValidator
    {
        /// <summary>Stable code emitted when lift and pivot axes are the same.</summary>
        public const string Code = "CTRL_SURFACE_AXIS_DEGENERATE";

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(PartValidationContext context)
        {
            var modules = context?.ModuleDatas;
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
                if (ctrl.CtrlTransformDir != ctrl.CtrlTransformRotAxis)
                {
                    continue;
                }
                yield return new ValidationIssue(
                    Code,
                    ValidationSeverity.Error,
                    $"CtrlTransformDir and CtrlTransformRotAxis both equal {ctrl.CtrlTransformDir}. The surface pivots in its own lift direction - deflection produces no torque change.");
            }
        }
    }
}
