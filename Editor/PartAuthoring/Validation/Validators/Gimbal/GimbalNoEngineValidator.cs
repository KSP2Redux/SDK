using System.Collections.Generic;
using KSP.Modules;
using Ksp2UnityTools.Editor.Validation;

namespace Ksp2UnityTools.Editor.PartAuthoring.Validation.Validators.Gimbal
{
    /// <summary>
    /// Errors when a part has a Gimbal module but no Engine module.
    /// </summary>
    /// <remarks>
    /// A gimbal rotates a transform but produces no thrust on its own. Without an engine module
    /// gimbaling has no effect on vessel attitude. Runtime does not gate on this, but the
    /// authoring intent is broken.
    /// </remarks>
    public sealed class GimbalNoEngineValidator : IPartValidator
    {
        /// <summary>Stable code emitted when a gimbal is present without an engine.</summary>
        public const string Code = "GIMBAL_NO_ENGINE";

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(PartValidationContext context)
        {
            var modules = context?.Modules;
            if (modules == null)
            {
                yield break;
            }
            bool hasGimbal = false;
            bool hasEngine = false;
            foreach (var module in modules)
            {
                if (module is Data_Gimbal) hasGimbal = true;
                if (module is Data_Engine) hasEngine = true;
            }
            if (hasGimbal && !hasEngine)
            {
                yield return new ValidationIssue(
                    Code,
                    ValidationSeverity.Error,
                    "Gimbal module on a part with no Engine. The gimbal rotates a transform that produces no thrust.");
            }
        }
    }
}
