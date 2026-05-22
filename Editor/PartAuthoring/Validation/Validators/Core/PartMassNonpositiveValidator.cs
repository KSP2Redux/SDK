using System.Collections.Generic;
using KSP.Sim.Definitions;
using Ksp2UnityTools.Editor.Validation;

namespace Ksp2UnityTools.Editor.PartAuthoring.Validation.Validators.Core
{
    /// <summary>
    /// Errors when <see cref="PartData.mass" /> is zero or negative.
    /// </summary>
    /// <remarks>
    /// Zero or negative mass produces a part the physics engine treats as massless, breaking
    /// staging mass calculations, dV math, joint behaviour, and any downstream TWR-style
    /// computations. The runtime does not guard against the case.
    /// </remarks>
    public sealed class PartMassNonpositiveValidator : IPartValidator
    {
        /// <summary>Stable code emitted when <c>mass &lt;= 0</c>.</summary>
        public const string Code = "PART_MASS_NONPOSITIVE";

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(PartValidationContext context)
        {
            PartData data = context?.Data;
            if (data == null)
            {
                yield break;
            }
            if (data.mass > 0.0)
            {
                yield break;
            }
            yield return new ValidationIssue(
                Code,
                ValidationSeverity.Error,
                $"Part mass is {data.mass:0.###}. Must be > 0.");
        }
    }
}
