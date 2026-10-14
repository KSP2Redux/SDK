using System.Collections.Generic;
using System.Linq;
using Ksp2UnityTools.Editor.Validation;

namespace Ksp2UnityTools.Editor.TechTreeAuthoring.Validation.Validators
{
    /// <summary>
    /// Checks a patch's override against the reference node it changes: warns when the node has left the reference,
    /// and when the reference has changed since the override was taken.
    /// </summary>
    /// <remarks>
    /// A field the patch changed and the reference changed too is a warning, since the patch overwrites the
    /// reference's change. A field only the reference changed is a note, since the patch leaves it alone. Either way
    /// the fix rebases the override onto the reference as it is now, keeping the patch's own changes.
    /// </remarks>
    public class TechNodeOverrideReferenceValidator : ITechNodeValidator
    {
        /// <summary>Stable code emitted for an override whose node the reference no longer has.</summary>
        public const string TARGET_MISSING_CODE = "TECHNODE_OVERRIDE_TARGET_MISSING";

        /// <summary>Stable code emitted for an override the reference has changed under.</summary>
        public const string REFERENCE_CHANGED_CODE = "TECHNODE_OVERRIDE_REFERENCE_CHANGED";

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(TechNodeValidationContext context)
        {
            if (context.Tree.Tree is not TechTreePatchAsset patch)
                yield break;

            TechTreeNode baseline = patch.FindBaseline(context.Id);
            if (baseline == null)
                yield break;

            TechTreeNode reference = context.Tree.Reference.FirstOrDefault(node => node.Data?.ID == context.Id);
            if (reference == null)
            {
                yield return new ValidationIssue(
                    TARGET_MISSING_CODE,
                    ValidationSeverity.Warning,
                    $"The reference tree no longer has {context.Id}, so the patch's change to it does nothing.",
                    new[]
                    {
                        new ValidationFix("Revert", () => context.Tree.Change("Revert Tech Node",
                            () => TechTreeEditing.RevertOverride(patch, context.Id))),
                    }
                );
                yield break;
            }

            List<string> moved = TechTreePatchBake.GetChangedFields(baseline, reference);
            if (moved.Count == 0)
                yield break;

            List<string> conflicts = TechTreePatchBake.GetConflictingFields(baseline, context.Node, reference);
            TechTreeNode node = context.Node;
            var rebase = new ValidationFix("Rebase", () => context.Tree.Change("Rebase Tech Node",
                () => TechTreePatchBake.Rebase(baseline, node, reference)));
            yield return conflicts.Count > 0
                ? new ValidationIssue(
                    REFERENCE_CHANGED_CODE,
                    ValidationSeverity.Warning,
                    $"The reference tree has changed {string.Join(", ", conflicts)} of {context.Name}, which this patch changes too and overwrites.",
                    new[] { rebase }
                )
                : new ValidationIssue(
                    REFERENCE_CHANGED_CODE,
                    ValidationSeverity.Info,
                    $"The reference tree has changed {string.Join(", ", moved)} of {context.Name} since the patch took it, which the patch leaves alone.",
                    new[] { rebase }
                );
        }
    }
}
