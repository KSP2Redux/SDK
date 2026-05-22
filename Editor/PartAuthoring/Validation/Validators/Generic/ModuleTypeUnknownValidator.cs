using System;
using System.Collections.Generic;
using KSP.Sim.Definitions;
using Ksp2UnityTools.Editor.Validation;

namespace Ksp2UnityTools.Editor.PartAuthoring.Validation.Validators.Generic
{
    /// <summary>
    /// Errors when a <c>ModuleData</c>'s <see cref="ModuleData.ModuleType" /> resolves to null.
    /// </summary>
    /// <remarks>
    /// Triggered when a Data_* class survives a serialization round-trip but its companion
    /// Module_* type was removed from the codebase. The module is unable to instantiate its
    /// runtime behaviour wrapper and silently contributes no functionality.
    /// </remarks>
    public sealed class ModuleTypeUnknownValidator : IPartValidator
    {
        /// <summary>Stable code emitted per unresolved module type.</summary>
        public const string Code = "MODULE_TYPE_UNKNOWN";

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(PartValidationContext context)
        {
            var modules = context?.Modules;
            if (modules == null)
            {
                yield break;
            }
            for (int i = 0; i < modules.Count; i++)
            {
                ModuleData module = modules[i];
                if (module == null)
                {
                    continue;
                }
                Type moduleType = null;
                try
                {
                    moduleType = module.ModuleType;
                }
                catch
                {
                    // ModuleType getters that throw are treated the same as null.
                }
                if (moduleType != null)
                {
                    continue;
                }
                yield return new ValidationIssue(
                    Code,
                    ValidationSeverity.Error,
                    $"Module slot {i} ({module.GetType().Name}) has no resolvable runtime type. The companion Module_* class is likely missing.");
            }
        }
    }
}
