using System.Collections.Generic;
using Ksp2UnityTools.Editor.Validation;
using Redux.Modules.Attributes;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PartAuthoring.Validation.Validators.Generic
{
    /// <summary>
    /// Errors when a <c>[TransformPath]</c>-decorated field stores a slash-separated path that
    /// does not resolve via <c>transform.Find</c> against the prefab root.
    /// </summary>
    /// <remarks>
    /// Path-style fields are hierarchy-aware: the runtime calls <c>part.transform.Find(value)</c>
    /// which walks each slash-separated segment as a child name. Leaf-name fields belong on
    /// <see cref="TransformNameAttribute" /> and are checked by the sibling
    /// TransformNameUnresolvedValidator. Empty values are skipped because many path fields are
    /// optional (the runtime treats empty as "use the part root").
    /// </remarks>
    public sealed class TransformPathUnresolvedValidator : IPartValidator
    {
        /// <summary>Stable code emitted per unresolved transform path.</summary>
        public const string Code = "TRANSFORM_PATH_UNRESOLVED";

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(PartValidationContext context)
        {
            Transform root = context?.Prefab?.transform;
            if (root == null)
            {
                yield break;
            }
            foreach (var fieldRef in ModuleFieldEnumerator.EnumerateStringFieldsWithAttribute<TransformPathAttribute>(context.ModuleDatas))
            {
                if (string.IsNullOrEmpty(fieldRef.Value))
                {
                    continue;
                }
                if (root.Find(fieldRef.Value) != null)
                {
                    continue;
                }
                yield return new ValidationIssue(
                    Code,
                    ValidationSeverity.Error,
                    $"{fieldRef.DisplayPath} = '{fieldRef.Value}' does not resolve to a transform path under the prefab root.");
            }
        }
    }
}
