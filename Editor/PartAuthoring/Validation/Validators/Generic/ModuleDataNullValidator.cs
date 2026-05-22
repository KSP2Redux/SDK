using System.Collections.Generic;
using Ksp2UnityTools.Editor.Validation;

namespace Ksp2UnityTools.Editor.PartAuthoring.Validation.Validators.Generic
{
    /// <summary>
    /// Errors when a slot in <c>PartCore.modules</c> holds a null <c>ModuleData</c>.
    /// </summary>
    /// <remarks>
    /// Usually the result of a serialization round-trip after removing a C# module type. The
    /// runtime walks the list and dereferences each entry to enumerate Data fields, so a null
    /// entry produces NullReferenceException at module init.
    /// </remarks>
    public sealed class ModuleDataNullValidator : IPartValidator
    {
        /// <summary>Stable code emitted per null slot.</summary>
        public const string Code = "MODULE_DATA_NULL";

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
                if (modules[i] != null)
                {
                    continue;
                }
                yield return new ValidationIssue(
                    Code,
                    ValidationSeverity.Error,
                    $"Module slot {i} is null. Remove the empty entry from the modules list.");
            }
        }
    }
}
