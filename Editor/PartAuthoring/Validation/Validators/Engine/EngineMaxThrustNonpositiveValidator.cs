using System.Collections.Generic;
using KSP.Modules;
using Ksp2UnityTools.Editor.Validation;

namespace Ksp2UnityTools.Editor.PartAuthoring.Validation.Validators.Engine
{
    /// <summary>
    /// Errors per engine mode when <c>maxThrust &lt;= 0</c>.
    /// </summary>
    /// <remarks>
    /// Module_Engine's thrust math divides by <c>maxThrust</c> and uses it as the upper bound of
    /// throttle scaling. Zero or negative produces no thrust output regardless of throttle.
    /// </remarks>
    public sealed class EngineMaxThrustNonpositiveValidator : IPartValidator
    {
        /// <summary>Stable code emitted per affected mode.</summary>
        public const string Code = "ENGINE_MAX_THRUST_NONPOSITIVE";

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
                if (module is not Data_Engine engine || engine.engineModes == null)
                {
                    continue;
                }
                for (int i = 0; i < engine.engineModes.Length; i++)
                {
                    Data_Engine.EngineMode mode = engine.engineModes[i];
                    if (mode == null || mode.maxThrust > 0f)
                    {
                        continue;
                    }
                    string modeLabel = string.IsNullOrEmpty(mode.engineID) ? $"mode {i}" : $"mode '{mode.engineID}'";
                    yield return new ValidationIssue(
                        Code,
                        ValidationSeverity.Error,
                        $"Engine {modeLabel} maxThrust is {mode.maxThrust:0.###} kN. Must be > 0.");
                }
            }
        }
    }
}
