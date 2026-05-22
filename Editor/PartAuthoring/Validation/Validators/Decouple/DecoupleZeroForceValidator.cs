using System.Collections.Generic;
using KSP.Modules;
using Ksp2UnityTools.Editor.Validation;

namespace Ksp2UnityTools.Editor.PartAuthoring.Validation.Validators.Decouple
{
    /// <summary>
    /// Warns when <c>ejectionForce == 0</c>.
    /// </summary>
    /// <remarks>
    /// Zero impulse means the decoupler still detaches the parts but applies no separation force.
    /// Stages will drift apart through residual physics rather than being pushed clear. Often
    /// intentional for very lightweight decouplers but worth surfacing.
    /// </remarks>
    public sealed class DecoupleZeroForceValidator : IPartValidator
    {
        /// <summary>Stable code emitted when ejection force is exactly zero.</summary>
        public const string Code = "DECOUPLE_ZERO_FORCE";

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
                if (module is not Data_Decouple decouple)
                {
                    continue;
                }
                if (decouple.ejectionForce != 0f)
                {
                    continue;
                }
                yield return new ValidationIssue(
                    Code,
                    ValidationSeverity.Warning,
                    "Decoupler ejectionForce is 0. Parts detach but receive no separation impulse.");
            }
        }
    }
}
