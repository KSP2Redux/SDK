using System;
using System.Collections.Generic;
using System.Linq;
using Ksp2UnityTools.Editor.Validation;
using UnityEngine;

namespace Ksp2UnityTools.Editor.TechTreeAuthoring.Validation
{
    /// <summary>
    /// Runs every tech tree validator over a tree and collects what they find.
    /// </summary>
    /// <remarks>
    /// Tree validators run once, node validators once per node of the tree. A validator that throws is logged and
    /// skipped, so one broken check never hides the rest.
    /// </remarks>
    public static class TechTreeValidationReport
    {
        /// <summary>
        /// Runs the validators.
        /// </summary>
        /// <param name="context">The tree to check.</param>
        /// <param name="costFilter">Only run validators of this cost, or null to run every one.</param>
        /// <returns>The issues, errors first.</returns>
        public static List<TechTreeIssue> Run(TechTreeValidationContext context, ValidatorCost? costFilter = ValidatorCost.Cheap)
        {
            var issues = new List<TechTreeIssue>();
            if (context?.Tree == null)
                return issues;

            foreach (IValidator<TechTreeValidationContext> validator in Matching(ValidatorRegistry<TechTreeValidationContext>.Validators, costFilter))
            {
                issues.AddRange(Collect(validator, context).Select(issue => new TechTreeIssue(issue, null)));
            }

            List<IValidator<TechNodeValidationContext>> nodeValidators =
                Matching(ValidatorRegistry<TechNodeValidationContext>.Validators, costFilter).ToList();
            for (int i = 0; i < context.Tree.Nodes.Count; i++)
            {
                TechTreeNode node = context.Tree.Nodes[i];
                if (string.IsNullOrEmpty(node.Data?.ID))
                    continue;

                var nodeContext = new TechNodeValidationContext(context, node, i);
                foreach (IValidator<TechNodeValidationContext> validator in nodeValidators)
                {
                    issues.AddRange(Collect(validator, nodeContext).Select(issue => new TechTreeIssue(issue, node.Data.ID)));
                }
            }

            return issues.OrderByDescending(issue => issue.Issue.Severity).ToList();
        }

        private static IEnumerable<IValidator<T>> Matching<T>(IEnumerable<IValidator<T>> validators, ValidatorCost? costFilter) =>
            validators.Where(validator => !costFilter.HasValue || validator.Cost == costFilter.Value);

        private static IEnumerable<ValidationIssue> Collect<T>(IValidator<T> validator, T context)
        {
            try
            {
                return (validator.Validate(context) ?? Array.Empty<ValidationIssue>()).ToList();
            }
            catch (Exception e)
            {
                Debug.LogError($"[TechTreeValidationReport] Validator '{validator.GetType().FullName}' threw: {e}");
                return Array.Empty<ValidationIssue>();
            }
        }
    }
}
