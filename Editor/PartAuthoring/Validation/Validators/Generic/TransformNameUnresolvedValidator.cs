using System.Collections.Generic;
using Ksp2UnityTools.Editor.Validation;
using Redux.Modules.Attributes;

namespace Ksp2UnityTools.Editor.PartAuthoring.Validation.Validators.Generic
{
    /// <summary>
    /// Errors when a <c>[TransformName]</c>-decorated field stores a leaf name that does not
    /// match any transform anywhere in the prefab hierarchy.
    /// </summary>
    /// <remarks>
    /// Leaf-name fields are resolved by recursive descendant search at runtime
    /// (FindModelTransform, FindChildRecursive, etc.) so any transform whose gameObject.name
    /// matches counts as resolved. Empty values are skipped because many fields are optional.
    /// </remarks>
    public sealed class TransformNameUnresolvedValidator : IPartValidator
    {
        /// <summary>Stable code emitted per unresolved transform leaf name.</summary>
        public const string Code = "TRANSFORM_NAME_UNRESOLVED";

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(PartValidationContext context)
        {
            if (context?.Part == null)
            {
                yield break;
            }
            var byName = context.TransformByName;
            foreach (var fieldRef in ModuleFieldEnumerator.EnumerateStringFieldsWithAttribute<TransformNameAttribute>(context.Modules))
            {
                if (string.IsNullOrEmpty(fieldRef.Value))
                {
                    continue;
                }
                if (byName.ContainsKey(fieldRef.Value))
                {
                    continue;
                }
                yield return new ValidationIssue(
                    Code,
                    ValidationSeverity.Error,
                    $"{fieldRef.DisplayPath} = '{fieldRef.Value}' does not match any transform name in the prefab.");
            }
        }
    }
}
