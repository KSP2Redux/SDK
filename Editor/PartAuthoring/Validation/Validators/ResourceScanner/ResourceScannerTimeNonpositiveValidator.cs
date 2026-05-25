using System.Collections.Generic;
using Ksp2UnityTools.Editor.Validation;
using Redux.Modules;

namespace Ksp2UnityTools.Editor.PartAuthoring.Validation.Validators.ResourceScanner
{
    /// <summary>
    /// Warns when <c>TimeToComplete &lt;= 0</c>.
    /// </summary>
    /// <remarks>
    /// Time required for the scanner to finish a scan. Zero or negative either completes instantly
    /// or never completes depending on how the runtime compares progress against the duration -
    /// both shapes are authoring mistakes.
    /// </remarks>
    public sealed class ResourceScannerTimeNonpositiveValidator : IPartValidator
    {
        /// <summary>Stable code emitted when TimeToComplete is non-positive.</summary>
        public const string Code = "RESOURCE_SCANNER_TIME_NONPOSITIVE";

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
                if (module is not Data_ResourceScanner scanner)
                {
                    continue;
                }
                if (scanner.TimeToComplete > 0f)
                {
                    continue;
                }
                yield return new ValidationIssue(
                    Code,
                    ValidationSeverity.Warning,
                    $"TimeToComplete is {scanner.TimeToComplete:0.###}. Scanner either finishes instantly or never finishes.");
            }
        }
    }
}
