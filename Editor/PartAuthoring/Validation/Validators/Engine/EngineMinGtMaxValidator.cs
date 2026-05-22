using System.Collections.Generic;
using KSP;
using KSP.Modules;
using Ksp2UnityTools.Editor.Validation;
using UnityEditor;

namespace Ksp2UnityTools.Editor.PartAuthoring.Validation.Validators.Engine
{
    /// <summary>
    /// Errors per engine mode when <c>minThrust &gt; maxThrust</c>.
    /// </summary>
    /// <remarks>
    /// Inverted range degenerates the throttle scaling and produces undefined thrust at every
    /// throttle setting. The Swap fix exchanges the two values.
    /// </remarks>
    public sealed class EngineMinGtMaxValidator : IPartValidator
    {
        /// <summary>Stable code emitted per affected mode.</summary>
        public const string Code = "ENGINE_MIN_GT_MAX";

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(PartValidationContext context)
        {
            CorePartData target = context?.Part;
            var modules = context?.Modules;
            if (modules == null || target == null)
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
                    if (mode == null || mode.minThrust <= mode.maxThrust)
                    {
                        continue;
                    }
                    int capturedIndex = i;
                    Data_Engine capturedEngine = engine;
                    var fix = new ValidationFix(
                        $"Swap minThrust / maxThrust ({mode.minThrust:0.###} / {mode.maxThrust:0.###})",
                        () =>
                        {
                            Undo.RecordObject(target, "Swap engine min/max thrust");
                            (capturedEngine.engineModes[capturedIndex].minThrust, capturedEngine.engineModes[capturedIndex].maxThrust) =
                                (capturedEngine.engineModes[capturedIndex].maxThrust, capturedEngine.engineModes[capturedIndex].minThrust);
                            EditorUtility.SetDirty(target);
                        });
                    string modeLabel = string.IsNullOrEmpty(mode.engineID) ? $"mode {i}" : $"mode '{mode.engineID}'";
                    yield return new ValidationIssue(
                        Code,
                        ValidationSeverity.Error,
                        $"Engine {modeLabel} minThrust ({mode.minThrust:0.###}) > maxThrust ({mode.maxThrust:0.###}).",
                        new[] { fix });
                }
            }
        }
    }
}
