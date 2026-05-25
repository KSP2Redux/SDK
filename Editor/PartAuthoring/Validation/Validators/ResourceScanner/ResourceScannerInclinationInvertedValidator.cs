using System.Collections.Generic;
using Ksp2UnityTools.Editor.Validation;
using Redux.Modules;

namespace Ksp2UnityTools.Editor.PartAuthoring.Validation.Validators.ResourceScanner
{
    /// <summary>
    /// Errors when <c>minInclination &gt; maxInclination</c>.
    /// </summary>
    /// <remarks>
    /// Inverted bounds produce a window that no inclination satisfies. The scanner can never
    /// engage on any orbit.
    /// </remarks>
    public sealed class ResourceScannerInclinationInvertedValidator : IPartValidator
    {
        /// <summary>Stable code emitted when the inclination window is inverted.</summary>
        public const string Code = "RESOURCE_SCANNER_INCLINATION_INVERTED";

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
                if (scanner.minInclination <= scanner.maxInclination)
                {
                    continue;
                }
                yield return new ValidationIssue(
                    Code,
                    ValidationSeverity.Error,
                    $"minInclination ({scanner.minInclination:0.##}°) > maxInclination ({scanner.maxInclination:0.##}°). No inclination satisfies the window.");
            }
        }
    }
}
