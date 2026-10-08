using System.Collections.Generic;
using KSP.Modules;
using Ksp2UnityTools.Editor.Validation;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PartAuthoring.Validation.Validators.Engine
{
    /// <summary>
    /// Warns per engine mode when <c>atmosphereCurve</c> has a key with <c>time &gt; 30</c>.
    /// </summary>
    /// <remarks>
    /// The atmosphere curve is indexed by atmospheric pressure in atmospheres. The thickest stock
    /// atmosphere (Eve) caps near 5 atm. Keys beyond ~10 atm are inert. A key at 30+ is almost
    /// always a units mistake (e.g. someone wrote kPa instead of atm).
    /// </remarks>
    public sealed class EngineAtmCurveKeyOutOfRangeValidator : IPartValidator
    {
        private const float ATM_PRESSURE_THRESHOLD = 30f;

        /// <summary>Stable code emitted per offending key.</summary>
        public const string Code = "ENGINE_ATMCURVE_KEY_OUT_OF_RANGE";

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
                    if (curve == null)
                    {
                        continue;
                    }
                    Keyframe[] keys = curve.keys;
                    if (keys == null)
                    {
                        continue;
                    }
                    string modeLabel = string.IsNullOrEmpty(mode.engineID) ? $"mode {i}" : $"mode '{mode.engineID}'";
                    foreach (var key in keys)
                    {
                        if (key.time <= ATM_PRESSURE_THRESHOLD)
                        {
                            continue;
                        }
                        yield return new ValidationIssue(
                            Code,
                            ValidationSeverity.Warning,
                            $"Engine {modeLabel} atmosphereCurve has a key at {key.time:0.##} atm. No body reaches that pressure - the X axis is atm, not kPa.");
                    }
                }
            }
        }
    }
}
