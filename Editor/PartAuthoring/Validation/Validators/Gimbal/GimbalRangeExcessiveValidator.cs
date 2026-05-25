using System.Collections.Generic;
using KSP.Modules;
using Ksp2UnityTools.Editor.Validation;

namespace Ksp2UnityTools.Editor.PartAuthoring.Validation.Validators.Gimbal
{
    /// <summary>
    /// Warns when <c>gimbalRange</c> exceeds 45 degrees.
    /// </summary>
    /// <remarks>
    /// Stock engines gimbal in the 0.5 to 10 degree range. Anything over 45 produces a gimbal so
    /// wide that the geometry visibly clips and the thrust vector goes nearly perpendicular to the
    /// vessel axis. Almost certainly an authoring typo (e.g. mistakenly entered the gimbal range
    /// in radians, or off by a decimal).
    /// </remarks>
    public sealed class GimbalRangeExcessiveValidator : IPartValidator
    {
        private const float EXCESSIVE_DEG = 45f;

        /// <summary>Stable code emitted for an oversized gimbal range.</summary>
        public const string Code = "GIMBAL_RANGE_EXCESSIVE";

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
                if (module is not Data_Gimbal gimbal)
                {
                    continue;
                }
                if (gimbal.gimbalRange <= EXCESSIVE_DEG)
                {
                    continue;
                }
                yield return new ValidationIssue(
                    Code,
                    ValidationSeverity.Warning,
                    $"gimbalRange is {gimbal.gimbalRange:0.##}° (> {EXCESSIVE_DEG:0}°). Stock engines stay under 10°. Check for a units mistake.");
            }
        }
    }
}
