using System.Collections.Generic;
using KSP.Modules;
using Ksp2UnityTools.Editor.Validation;

namespace Ksp2UnityTools.Editor.PartAuthoring.Validation.Validators.Engine
{
    /// <summary>
    /// Errors per engine mode when both thrust-resolution paths are empty.
    /// </summary>
    /// <remarks>
    /// Module_Engine resolves thrust transforms via two paths. First it tries the per-multiplier
    /// list <c>ThrustTransformNamesMultipliers</c>. When that array is null or empty it falls
    /// back to the single name <c>thrustVectorTransformName</c> and calls FindModelTransforms on
    /// it. If both are empty the engine finds zero thrust transforms and produces no force.
    /// </remarks>
    public sealed class EngineThrustPathEmptyValidator : IPartValidator
    {
        /// <summary>Stable code emitted per affected mode.</summary>
        public const string Code = "ENGINE_THRUST_PATH_EMPTY";

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
                if (module is not Data_Engine engine || engine.engineModes == null)
                {
                    continue;
                }
                for (int i = 0; i < engine.engineModes.Length; i++)
                {
                    Data_Engine.EngineMode mode = engine.engineModes[i];
                    if (mode == null)
                    {
                        continue;
                    }
                    bool groupEmpty = mode.ThrustTransformNamesMultipliers == null || mode.ThrustTransformNamesMultipliers.Length == 0;
                    bool nameEmpty = string.IsNullOrEmpty(mode.thrustVectorTransformName);
                    if (!groupEmpty || !nameEmpty)
                    {
                        continue;
                    }
                    string modeLabel = string.IsNullOrEmpty(mode.engineID) ? $"mode {i}" : $"mode '{mode.engineID}'";
                    yield return new ValidationIssue(
                        Code,
                        ValidationSeverity.Error,
                        $"Engine {modeLabel} has no thrust transforms. Populate ThrustTransformNamesMultipliers or set thrustVectorTransformName.");
                }
            }
        }
    }
}
