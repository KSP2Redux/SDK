using System.Collections.Generic;
using System.Linq;
using Ksp2UnityTools.Editor.Validation;

namespace Ksp2UnityTools.Editor.TechTreeAuthoring.Validation.Validators
{
    /// <summary>
    /// Checks a patch's tiers and start node against the reference tree's: notes when the reference has changed them
    /// since the patch took its baseline.
    /// </summary>
    /// <remarks>
    /// A tier or start node the patch changed and the reference changed too is a warning, since the patch overwrites
    /// the reference's change. One only the reference changed is a note, since the patch leaves it alone. Either way
    /// the fix rebases the patch onto the reference as it is now, keeping the patch's own changes.
    /// </remarks>
    public class TechTreeDataReferenceValidator : ITechTreeValidator
    {
        /// <summary>Stable code emitted when the reference's tiers or start node changed under a patch.</summary>
        public const string Code = "TECHTREE_DATA_REFERENCE_CHANGED";

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(TechTreeValidationContext context)
        {
            if (context.Tree is not TechTreePatchAsset patch || patch.TreeDataBaseline == null || context.ReferenceTreeData == null)
                yield break;

            ReferenceTreeData baseline = patch.TreeDataBaseline;
            ReferenceTreeData reference = context.ReferenceTreeData;
            List<int> movedTiers = TechTreePatchBake.GetChangedTiers(baseline.Tiers, reference.Tiers);
            bool isStartNodeMoved = !TechTreePatchBake.IsSameStartNode(baseline.StartNodeId, reference.StartNodeId);
            if (movedTiers.Count == 0 && !isStartNodeMoved)
                yield break;

            HashSet<int> patchTiers = new(TechTreePatchBake.GetChangedTiers(patch));
            List<int> conflictingTiers = movedTiers.Where(patchTiers.Contains).ToList();
            bool isStartNodeConflicting = isStartNodeMoved && TechTreePatchBake.ChangesStartNode(patch);
            var rebase = new ValidationFix("Rebase", () => context.Change("Rebase Tech Tree Data",
                () => TechTreePatchBake.RebaseTreeData(patch, reference)));
            yield return conflictingTiers.Count > 0 || isStartNodeConflicting
                ? new ValidationIssue(
                    Code,
                    ValidationSeverity.Warning,
                    $"The reference tree has changed {Describe(conflictingTiers, isStartNodeConflicting)}, which this patch changes too and overwrites.",
                    new[] { rebase }
                )
                : new ValidationIssue(
                    Code,
                    ValidationSeverity.Info,
                    $"The reference tree has changed {Describe(movedTiers, isStartNodeMoved)} since the patch took it, which the patch leaves alone.",
                    new[] { rebase }
                );
        }

        private static string Describe(List<int> tiers, bool isStartNode)
        {
            var parts = new List<string>();
            if (tiers.Count > 0)
            {
                parts.Add(tiers.Count == 1 ? $"the name of tier {tiers[0]}" : $"the names of tiers {string.Join(", ", tiers)}");
            }

            if (isStartNode)
            {
                parts.Add("the start node");
            }

            return string.Join(" and ", parts);
        }
    }
}
