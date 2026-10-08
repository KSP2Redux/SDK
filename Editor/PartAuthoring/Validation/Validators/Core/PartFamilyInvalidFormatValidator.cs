using System.Collections.Generic;
using System.Text.RegularExpressions;
using KSP.Sim.Definitions;
using Ksp2UnityTools.Editor.Validation;

namespace Ksp2UnityTools.Editor.PartAuthoring.Validation.Validators.Core
{
    /// <summary>
    /// Errors when <see cref="PartData.family" /> does not begin with the canonical
    /// four-digit-prefix-dash pattern (e.g. <c>"0100-Methalox"</c>).
    /// </summary>
    /// <remarks>
    /// KSP2's Parts Manager sorts the picker by the numeric prefix, so families without it
    /// land at the head of the picker out of order. No auto-fix: there is no safe way to guess
    /// the right four-digit prefix for an arbitrary string. Authors should use the autocomplete
    /// in the Identity section to pick a canonical family from <see cref="PartAuthoringChoiceCatalog" />.
    /// </remarks>
    public sealed class PartFamilyInvalidFormatValidator : IPartValidator
    {
        /// <summary>Stable code emitted when the family string fails the prefix check.</summary>
        public const string Code = "PART_FAMILY_INVALID_FORMAT";

        private static readonly Regex SortPrefixPattern = new("^\\d{4}-", RegexOptions.Compiled);

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(PartValidationContext context)
        {
            PartData data = context?.Data;
            if (data == null || string.IsNullOrEmpty(data.family))
            {
                yield break;
            }
            if (SortPrefixPattern.IsMatch(data.family))
            {
                yield break;
            }
            yield return new ValidationIssue(
                Code,
                ValidationSeverity.Error,
                $"Family '{data.family}' does not start with a four-digit sort prefix. Required format: '0000-Name' through '9999-Name'.");
        }
    }
}
