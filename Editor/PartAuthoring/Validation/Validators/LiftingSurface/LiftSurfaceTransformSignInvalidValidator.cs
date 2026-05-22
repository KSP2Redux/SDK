using System.Collections.Generic;
using KSP.Modules;
using Ksp2UnityTools.Editor.Validation;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PartAuthoring.Validation.Validators.LiftingSurface
{
    /// <summary>
    /// Warns when <c>transformSign</c> is not approximately +1 or -1.
    /// </summary>
    /// <remarks>
    /// The lifting surface multiplies its computed lift vector by <c>transformSign</c>. The field
    /// is documented as a +/-1 flip toggle to handle mirrored geometry. Non-unit values silently
    /// scale the lift magnitude away from the curve-defined value.
    /// </remarks>
    public sealed class LiftSurfaceTransformSignInvalidValidator : IPartValidator
    {
        private const float TOLERANCE = 1e-3f;

        /// <summary>Stable code emitted when transformSign is not unit-magnitude.</summary>
        public const string Code = "LIFT_SURFACE_TRANSFORM_SIGN_INVALID";

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
                if (module is not Data_LiftingSurface lift)
                {
                    continue;
                }
                if (Mathf.Abs(Mathf.Abs(lift.transformSign) - 1f) <= TOLERANCE)
                {
                    continue;
                }
                yield return new ValidationIssue(
                    Code,
                    ValidationSeverity.Warning,
                    $"transformSign is {lift.transformSign:0.###}. Expected +1 or -1 - other values silently scale the lift vector.");
            }
        }
    }
}
