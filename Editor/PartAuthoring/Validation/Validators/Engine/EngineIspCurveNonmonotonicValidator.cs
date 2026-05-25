using System.Collections.Generic;
using KSP.Modules;
using Ksp2UnityTools.Editor.Validation;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PartAuthoring.Validation.Validators.Engine
{
    /// <summary>
    /// Warns per engine mode when <c>atmosphereCurve</c> is not weakly decreasing in atmospheric
    /// pressure.
    /// </summary>
    /// <remarks>
    /// Real engines lose Isp as atmospheric pressure rises - vacuum optimisations underperform at
    /// sea level. A curve that increases with pressure produces a vacuum-bad, sea-level-good
    /// engine, which is almost always an authoring mistake. Warning, not Error, because a
    /// deliberately unusual engine (airbreathing-like) may want a non-monotonic curve.
    /// </remarks>
    public sealed class EngineIspCurveNonmonotonicValidator : IPartValidator
    {
        /// <summary>Stable code emitted per affected mode.</summary>
        public const string Code = "ENGINE_ISP_CURVE_NONMONOTONIC";

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
                    AnimationCurve curve = mode?.atmosphereCurve?.Curve;
                    if (curve == null || curve.keys == null || curve.keys.Length < 2)
                    {
                        continue;
                    }
                    Keyframe[] keys = curve.keys;
                    bool nonmonotonic = false;
                    for (int k = 1; k < keys.Length; k++)
                    {
                        if (keys[k].value > keys[k - 1].value)
                        {
                            nonmonotonic = true;
                            break;
                        }
                    }
                    if (!nonmonotonic)
                    {
                        continue;
                    }
                    string modeLabel = string.IsNullOrEmpty(mode.engineID) ? $"mode {i}" : $"mode '{mode.engineID}'";
                    yield return new ValidationIssue(
                        Code,
                        ValidationSeverity.Warning,
                        $"Engine {modeLabel} atmosphereCurve gains Isp as atm pressure increases. Real engines lose Isp at higher pressure - check the key order.");
                }
            }
        }
    }
}
