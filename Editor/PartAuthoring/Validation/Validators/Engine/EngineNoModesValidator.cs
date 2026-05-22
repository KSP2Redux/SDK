using System.Collections.Generic;
using KSP.Modules;
using Ksp2UnityTools.Editor.Validation;

namespace Ksp2UnityTools.Editor.PartAuthoring.Validation.Validators.Engine
{
    /// <summary>
    /// Errors when a <see cref="Data_Engine" /> has no entries in <c>engineModes</c>.
    /// </summary>
    /// <remarks>
    /// Module_Engine returns early at module init when <c>engineModes == null</c> or
    /// <c>engineModes.Length == 0</c>. The engine then produces no thrust, no PAM actions, and
    /// silently contributes nothing to the vessel.
    /// </remarks>
    public sealed class EngineNoModesValidator : IPartValidator
    {
        /// <summary>Stable code emitted when the engine has zero modes.</summary>
        public const string Code = "ENGINE_NO_MODES";

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
                if (module is not Data_Engine engine)
                {
                    continue;
                }
                if (engine.engineModes != null && engine.engineModes.Length > 0)
                {
                    continue;
                }
                yield return new ValidationIssue(
                    Code,
                    ValidationSeverity.Error,
                    "Engine has no modes. Add at least one entry to engineModes.");
            }
        }
    }
}
