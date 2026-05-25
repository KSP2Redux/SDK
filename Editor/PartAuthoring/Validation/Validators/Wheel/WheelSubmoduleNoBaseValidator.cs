using System;
using System.Collections.Generic;
using KSP.Modules;
using Ksp2UnityTools.Editor.Validation;

namespace Ksp2UnityTools.Editor.PartAuthoring.Validation.Validators.Wheel
{
    /// <summary>
    /// Errors when a wheel submodule is present without a sibling <c>Data_WheelBase</c>.
    /// </summary>
    /// <remarks>
    /// <see cref="Module_WheelSubmodule.SetWheelBase" /> calls <c>GetComponent&lt;Module_WheelBase&gt;</c>
    /// and logs an error then returns early when the component is missing. The submodule never
    /// registers with the base and never receives <c>OnWheelInit</c>, so its runtime hooks
    /// silently fail to fire.
    /// </remarks>
    public sealed class WheelSubmoduleNoBaseValidator : IPartValidator
    {
        /// <summary>Stable code emitted per offending submodule.</summary>
        public const string Code = "WHEEL_SUBMODULE_NO_BASE";

        private static readonly Type[] SUBMODULE_TYPES = new[]
        {
            typeof(Data_WheelSuspension),
            typeof(Data_WheelMotor),
            typeof(Data_WheelSteering),
            typeof(Data_WheelBrakes),
            typeof(Data_WheelLock),
            typeof(Data_WheelDamage),
            typeof(Data_WheelBogey),
            typeof(Data_WheelMotorSteering),
        };

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(PartValidationContext context)
        {
            var modules = context?.ModuleDatas;
            if (modules == null)
            {
                yield break;
            }
            bool hasBase = false;
            var presentSubmodules = new List<Type>();
            foreach (var module in modules)
            {
                if (module == null)
                {
                    continue;
                }
                Type type = module.GetType();
                if (type == typeof(Data_WheelBase))
                {
                    hasBase = true;
                    continue;
                }
                foreach (var subType in SUBMODULE_TYPES)
                {
                    if (type == subType)
                    {
                        presentSubmodules.Add(type);
                        break;
                    }
                }
            }
            if (hasBase || presentSubmodules.Count == 0)
            {
                yield break;
            }
            foreach (var subType in presentSubmodules)
            {
                yield return new ValidationIssue(
                    Code,
                    ValidationSeverity.Error,
                    $"{subType.Name} requires a sibling Data_WheelBase. SetWheelBase logs an error and the submodule never receives OnWheelInit.");
            }
        }
    }
}
