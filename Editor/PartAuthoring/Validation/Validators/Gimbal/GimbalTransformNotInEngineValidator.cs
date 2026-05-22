using System;
using System.Collections.Generic;
using KSP;
using KSP.Modules;
using Ksp2UnityTools.Editor.Validation;
using UnityEditor;

namespace Ksp2UnityTools.Editor.PartAuthoring.Validation.Validators.Gimbal
{
    /// <summary>
    /// Warns when a gimbal's <c>gimbalTransformName</c> does not appear in any engine mode's
    /// thrust-transform list.
    /// </summary>
    /// <remarks>
    /// The gimbal rotates the named transform every fixed step. If that transform is not also a
    /// thrust transform on the engine, gimbaling moves a non-emitter and produces no torque
    /// change. The Copy fix copies the first thrust transform name from the first engine mode.
    /// </remarks>
    public sealed class GimbalTransformNotInEngineValidator : IPartValidator
    {
        /// <summary>Stable code emitted when the gimbal points at a non-thrust transform.</summary>
        public const string Code = "GIMBAL_TRANSFORM_NOT_IN_ENGINE";

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(PartValidationContext context)
        {
            CorePartData target = context?.Part;
            var modules = context?.Modules;
            if (modules == null || target == null)
            {
                yield break;
            }
            Data_Gimbal gimbal = null;
            Data_Engine engine = null;
            foreach (var module in modules)
            {
                if (module is Data_Gimbal g) gimbal = g;
                else if (module is Data_Engine e) engine = e;
            }
            if (gimbal == null || engine == null || string.IsNullOrEmpty(gimbal.gimbalTransformName))
            {
                yield break;
            }

            var thrustNames = new HashSet<string>(StringComparer.Ordinal);
            if (engine.engineModes != null)
            {
                foreach (var mode in engine.engineModes)
                {
                    if (mode == null) continue;
                    if (!string.IsNullOrEmpty(mode.thrustVectorTransformName))
                    {
                        thrustNames.Add(mode.thrustVectorTransformName);
                    }
                    if (mode.ThrustTransformNamesMultipliers == null) continue;
                    foreach (var ttg in mode.ThrustTransformNamesMultipliers)
                    {
                        if (ttg != null && !string.IsNullOrEmpty(ttg.ThrustTransformName))
                        {
                            thrustNames.Add(ttg.ThrustTransformName);
                        }
                    }
                }
            }
            if (thrustNames.Contains(gimbal.gimbalTransformName))
            {
                yield break;
            }

            string suggestion = FirstThrustName(engine);
            ValidationFix[] fixes = null;
            if (!string.IsNullOrEmpty(suggestion))
            {
                Data_Gimbal capturedGimbal = gimbal;
                string capturedSuggestion = suggestion;
                fixes = new[]
                {
                    new ValidationFix(
                        $"Copy Engine.thrustTransform -> Gimbal.gimbalTransformName ('{capturedSuggestion}')",
                        () =>
                        {
                            Undo.RecordObject(target, "Copy engine thrust transform to gimbal");
                            capturedGimbal.gimbalTransformName = capturedSuggestion;
                            EditorUtility.SetDirty(target);
                        })
                };
            }

            yield return new ValidationIssue(
                Code,
                ValidationSeverity.Warning,
                $"Gimbal.gimbalTransformName = '{gimbal.gimbalTransformName}' is not a thrust transform on any engine mode. Gimbaling has no effect on thrust direction.",
                fixes);
        }

        private static string FirstThrustName(Data_Engine engine)
        {
            if (engine.engineModes == null)
            {
                return null;
            }
            foreach (var mode in engine.engineModes)
            {
                if (mode == null) continue;
                if (mode.ThrustTransformNamesMultipliers != null)
                {
                    foreach (var ttg in mode.ThrustTransformNamesMultipliers)
                    {
                        if (ttg != null && !string.IsNullOrEmpty(ttg.ThrustTransformName))
                        {
                            return ttg.ThrustTransformName;
                        }
                    }
                }
                if (!string.IsNullOrEmpty(mode.thrustVectorTransformName))
                {
                    return mode.thrustVectorTransformName;
                }
            }
            return null;
        }
    }
}
