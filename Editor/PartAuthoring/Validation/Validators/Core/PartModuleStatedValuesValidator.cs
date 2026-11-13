using System;
using System.Collections.Generic;
using System.Reflection;
using Ksp2UnityTools.Editor.Validation;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PartAuthoring.Validation.Validators.Core
{
    /// <summary>
    /// Checks thermal, generator and transmitter authored values without invoking runtime modules.
    /// </summary>
    public class PartModuleStatedValuesValidator : IPartValidator
    {
        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(PartValidationContext context)
        {
            if (context?.Part == null)
                yield break;
            foreach (var module in context.ModuleDatas)
            {
                string[] positiveFields = module.GetType().Name switch
                {
                    "Data_Heatshield" => new[] { "AblationTempThreshold", "AblationMaximumOverThreshold", "AblationFluxReference", "AblationFluxExponent", "PyrolysisLossFactor" },
                    "Data_Cooler" => new[] { "defaultFluxRemoved" },
                    "Data_Transmitter" => new[] { "CommunicationRange", "DataTransmissionInterval", "DataPacketSize" },
                    _ => Array.Empty<string>()
                };
                foreach (string field in positiveFields)
                {
                    if (!TryNumber(module, field, out double value))
                        continue;
                    if (!double.IsNaN(value) && !double.IsInfinity(value) && value > 0)
                        continue;
                    yield return new ValidationIssue("PART_MODULE_STATED_VALUE_INVALID", ValidationSeverity.Warning,
                        $"{module.GetType().Name}.{field} is {value:G6}. Expected a finite value greater than 0. Check intentional disabled capabilities.");
                }
                if (module.GetType().Name == "Data_Heatshield")
                {
                    if (Read(module, "ShieldingDirection") is Vector3 direction &&
                        (!PartGeometry.IsFinite(direction) || direction.sqrMagnitude == 0f))
                    {
                        yield return new ValidationIssue("PART_HEATSHIELD_DIRECTION_INVALID", ValidationSeverity.Warning,
                            $"Heat shield ShieldingDirection is {direction}. Expected a finite nonzero shielding axis.");
                    }
                    if (TryNumber(module, "AblationTempThreshold", out double threshold) && threshold >= context.Data.maxTemp)
                    {
                        yield return new ValidationIssue("PART_HEATSHIELD_ABLATION_TEMPERATURE", ValidationSeverity.Warning,
                            $"Heat shield AblationTempThreshold is {threshold:G6} K against part maxTemp {context.Data.maxTemp:G6} K. Expected ablation to begin below part destruction temperature.");
                    }
                }
                if (module.GetType().Name == "Data_ModuleGenerator")
                {
                    object setting = Read(module, "ResourceSetting");
                    string resource = Read(setting, "ResourceName") as string;
                    bool hasRate = TryNumber(setting, "Rate", out double rate);
                    if (string.IsNullOrWhiteSpace(resource) || !hasRate || rate <= 0 || double.IsNaN(rate) || double.IsInfinity(rate))
                    {
                        yield return new ValidationIssue("PART_GENERATOR_OUTPUT_INVALID", ValidationSeverity.Warning,
                            $"Generator ResourceSetting resource '{resource}' rate {rate:G6}. Expected a named output resource and finite rate greater than 0.");
                    }
                    if (Read(module, "UseDecay") is true && TryNumber(module, "DecayTime", out double lifetime) &&
                        (lifetime <= 0 || double.IsNaN(lifetime) || double.IsInfinity(lifetime)))
                    {
                        yield return new ValidationIssue("PART_GENERATOR_DECAY_INVALID", ValidationSeverity.Warning,
                            $"Generator DecayTime is {lifetime:G6} s with UseDecay enabled. Expected finite lifetime greater than 0 s.");
                    }
                    if (Read(module, "AutoShutdown") is true && TryNumber(module, "SafeOperationTemperature", out double safe) &&
                        TryNumber(module, "AutoShutdownTemperature", out double shutdown) && safe >= shutdown)
                    {
                        yield return new ValidationIssue("PART_GENERATOR_TEMPERATURE_ORDER", ValidationSeverity.Warning,
                            $"Generator SafeOperationTemperature {safe:G6} K is not below AutoShutdownTemperature {shutdown:G6} K. Expected safe temperature below shutdown.");
                    }
                }
            }
        }

        // Generated ECS data exposes some declarations as properties, others as fields.
        private static object Read(object owner, string name) => owner?.GetType().GetField(name)?.GetValue(owner) ??
            owner?.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public)?.GetValue(owner);

        private static bool TryNumber(object owner, string field, out double value)
        {
            object raw = Read(owner, field);
            value = raw is float or double or int ? Convert.ToDouble(raw) : 0;
            return raw is float or double or int;
        }
    }
}
