using System.Collections.Generic;
using KSP.Sim.Definitions;
using Ksp2UnityTools.Editor.Validation;

namespace Ksp2UnityTools.Editor.PartAuthoring.Validation.Validators.Core
{
    /// <summary>
    /// Errors when <see cref="PartData.partName" /> is null or whitespace.
    /// </summary>
    /// <remarks>
    /// The runtime keys parts by <c>partName</c> for save-file references, addressables, and OAB
    /// catalog entries. An empty name produces a part that cannot be loaded or referenced.
    /// </remarks>
    public sealed class PartNameEmptyValidator : IPartValidator
    {
        /// <summary>Stable code emitted when the part name is empty or whitespace.</summary>
        public const string Code = "PART_NAME_EMPTY";

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(PartValidationContext context)
        {
            PartData data = context?.Data;
            if (data == null)
            {
                yield break;
            }
            if (!string.IsNullOrWhiteSpace(data.partName))
            {
                yield break;
            }
            yield return new ValidationIssue(
                Code,
                ValidationSeverity.Error,
                "Part name is empty. Set a unique partName to identify this part in save files and the addressables catalog.");
        }
    }
}
