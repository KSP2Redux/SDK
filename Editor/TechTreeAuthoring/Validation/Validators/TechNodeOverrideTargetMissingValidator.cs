using System.Collections.Generic;
using System.Linq;
using Ksp2UnityTools.Editor.Validation;

namespace Ksp2UnityTools.Editor.TechTreeAuthoring.Validation.Validators
{
    /// <summary>
    /// Warns on a patch's override whose node the reference tree no longer has, so the change does nothing.
    /// </summary>
    public class TechNodeOverrideTargetMissingValidator : ITechNodeValidator
    {
        /// <summary>Stable code emitted for an override whose node the reference no longer has.</summary>
        public const string Code = "TECHNODE_OVERRIDE_TARGET_MISSING";

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(TechNodeValidationContext context)
        {
            if (context.Tree.Tree is not TechTreePatchAsset patch
                || !patch.IsOverride(context.Id)
                || context.Tree.Reference.Any(node => node.Data?.ID == context.Id))
            {
                yield break;
            }

            yield return new ValidationIssue(
                Code,
                ValidationSeverity.Warning,
                $"The reference tree no longer has {context.Id}, so the patch's change to it does nothing.",
                new[]
                {
                    new ValidationFix("Revert", () => context.Tree.Change("Revert Tech Node",
                        () => TechTreeEditing.RevertOverride(patch, context.Id))),
                }
            );
        }
    }
}
