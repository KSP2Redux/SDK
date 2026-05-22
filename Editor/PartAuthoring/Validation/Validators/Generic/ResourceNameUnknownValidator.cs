using System;
using System.Collections.Generic;
using Ksp2UnityTools.Editor.PartAuthoring.Inspectors.Widgets;
using Ksp2UnityTools.Editor.Validation;
using Redux.Modules.Attributes;

namespace Ksp2UnityTools.Editor.PartAuthoring.Validation.Validators.Generic
{
    /// <summary>
    /// Warns when a <c>[ResourceName]</c>-decorated field references a name that is not in the
    /// project's addressables-loaded resource catalog.
    /// </summary>
    /// <remarks>
    /// Severity is Warning, not Error, because mods can extend the resource catalog at game-load
    /// time. Authors targeting a stock resource see the name flagged, but a deliberate Redux-only
    /// resource that the catalog has not picked up is still allowed. Empty values are skipped -
    /// the runtime treats empty as "no resource" and many propellant slots are intentionally blank.
    /// </remarks>
    public sealed class ResourceNameUnknownValidator : IPartValidator
    {
        /// <summary>Stable code emitted per unknown resource name.</summary>
        public const string Code = "RESOURCE_NAME_UNKNOWN";

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(PartValidationContext context)
        {
            if (context?.Part == null)
            {
                yield break;
            }
            var known = new HashSet<string>(ResourceNameCatalog.GetKnownResources(), StringComparer.Ordinal);
            foreach (var fieldRef in ModuleFieldEnumerator.EnumerateStringFieldsWithAttribute<ResourceNameAttribute>(context.Modules))
            {
                if (string.IsNullOrEmpty(fieldRef.Value))
                {
                    continue;
                }
                if (known.Contains(fieldRef.Value))
                {
                    continue;
                }
                yield return new ValidationIssue(
                    Code,
                    ValidationSeverity.Warning,
                    $"{fieldRef.DisplayPath} = '{fieldRef.Value}' is not a known resource. Mods can extend the catalog at runtime, but check the name for typos.");
            }
        }
    }
}
