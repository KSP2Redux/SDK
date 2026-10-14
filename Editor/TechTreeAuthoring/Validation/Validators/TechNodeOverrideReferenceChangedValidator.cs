using System.Collections.Generic;
using System.Linq;
using Ksp2UnityTools.Editor.Validation;

namespace Ksp2UnityTools.Editor.TechTreeAuthoring.Validation.Validators
{
    /// <summary>
    /// Checks a patch's override against the reference node it changes, for changes the reference made since the
    /// override was taken.
    /// </summary>
    /// <remarks>
    /// A field the patch changed and the reference changed too is a warning, since the patch overwrites the
    /// reference's change. A field only the reference changed is a note, since the patch leaves it alone. Either way
    /// the fix rebases the override onto the reference as it is now, keeping the patch's own changes.
    /// </remarks>
    public class TechNodeOverrideReferenceChangedValidator : ITechNodeValidator
    {
        /// <summary>Stable code emitted for an override the reference has changed under.</summary>
        public const string Code = "TECHNODE_OVERRIDE_REFERENCE_CHANGED";

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(TechNodeValidationContext context)
        {
            if (context.Tree.Tree is not TechTreePatchAsset patch)
                yield break;

            TechTreeNode baseline = patch.FindBaseline(context.Id);
            TechTreeNode reference = context.Tree.Reference.FirstOrDefault(node => node.Data?.ID == context.Id);
            if (baseline == null || reference == null)
                yield break;

            List<string> moved = TechTreePatchBake.GetChangedFields(baseline, reference);
            if (moved.Count == 0)
                yield break;

            List<string> conflicts = TechTreePatchBake.GetConflictingFields(baseline, context.Node, reference);
            TechTreeNode node = context.Node;
            var rebase = new ValidationFix("Rebase", () => context.Tree.Change("Rebase Tech Node",
                () => TechTreePatchBake.Rebase(baseline, node, reference)));
            yield return conflicts.Count > 0
                ? new ValidationIssue(
                    Code,
                    ValidationSeverity.Warning,
                    $"The reference tree has changed {string.Join(", ", conflicts)} of {context.Name}, which this patch changes too and overwrites.",
                    new[] { rebase }
                )
                : new ValidationIssue(
                    Code,
                    ValidationSeverity.Info,
                    $"The reference tree has changed {string.Join(", ", moved)} of {context.Name} since the patch took it, which the patch leaves alone.",
                    new[] { rebase }
                );
        }
    }
}
