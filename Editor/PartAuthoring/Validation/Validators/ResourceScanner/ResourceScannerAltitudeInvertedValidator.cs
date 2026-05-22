using System.Collections.Generic;
using Ksp2UnityTools.Editor.Validation;
using Redux.Modules;

namespace Ksp2UnityTools.Editor.PartAuthoring.Validation.Validators.ResourceScanner
{
    /// <summary>
    /// Errors when <c>minAltitude &gt; maxAltitude</c> and both are positive (not the -1 sentinel).
    /// </summary>
    /// <remarks>
    /// The runtime tests whether the vessel altitude falls within the configured window. An
    /// inverted range produces a window that no altitude satisfies - the scanner can never engage.
    /// The -1 sentinel value disables the bound entirely; only inverted-and-both-set rows fail.
    /// </remarks>
    public sealed class ResourceScannerAltitudeInvertedValidator : IPartValidator
    {
        /// <summary>Stable code emitted when the altitude window is inverted.</summary>
        public const string Code = "RESOURCE_SCANNER_ALTITUDE_INVERTED";

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(PartValidationContext context)
        {
            var modules = context?.Modules;
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
                if (scanner.minAltitude == -1.0 || scanner.maxAltitude == -1.0)
                {
                    continue;
                }
                if (scanner.minAltitude <= scanner.maxAltitude)
                {
                    continue;
                }
                yield return new ValidationIssue(
                    Code,
                    ValidationSeverity.Error,
                    $"minAltitude ({scanner.minAltitude:0.#}) > maxAltitude ({scanner.maxAltitude:0.#}). No altitude satisfies the window.");
            }
        }
    }
}
