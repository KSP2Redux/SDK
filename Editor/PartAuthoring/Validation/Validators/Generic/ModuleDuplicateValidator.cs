using System;
using System.Collections.Generic;
using KSP.Sim.Definitions;
using Ksp2UnityTools.Editor.Validation;

namespace Ksp2UnityTools.Editor.PartAuthoring.Validation.Validators.Generic
{
    /// <summary>
    /// Errors when two or more <c>ModuleData</c> entries share the same concrete C# type.
    /// </summary>
    /// <remarks>
    /// Modules are treated as singletons across the entire stock part catalog (0 of 391 part files
    /// contain a duplicate). The runtime indexes <c>DataModules</c> by type, so a duplicate
    /// overwrites its sibling on init and the patch system has no way to disambiguate which
    /// instance a patch targets. Disallow outright.
    /// </remarks>
    public sealed class ModuleDuplicateValidator : IPartValidator
    {
        /// <summary>Stable code emitted per duplicate-type group.</summary>
        public const string Code = "MODULE_DUPLICATE";

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(PartValidationContext context)
        {
            var modules = context?.ModuleDatas;
            if (modules == null || modules.Count < 2)
            {
                yield break;
            }
            var counts = new Dictionary<Type, int>();
            foreach (ModuleData module in modules)
            {
                if (module == null)
                {
                    continue;
                }
                Type type = module.GetType();
                counts[type] = counts.TryGetValue(type, out int n) ? n + 1 : 1;
            }
            foreach (var kv in counts)
            {
                if (kv.Value < 2)
                {
                    continue;
                }
                yield return new ValidationIssue(
                    Code,
                    ValidationSeverity.Error,
                    $"Module type {kv.Key.Name} appears {kv.Value} times. Modules are singletons. The runtime keeps only the last entry and patches cannot disambiguate.");
            }
        }
    }
}
