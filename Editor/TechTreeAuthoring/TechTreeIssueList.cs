using System;
using System.Collections.Generic;
using System.Linq;
using Ksp2UnityTools.Editor.TechTreeAuthoring.Validation;
using Ksp2UnityTools.Editor.Validation;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.TechTreeAuthoring
{
    /// <summary>
    /// The issues a tree's validation found, one row each, where clicking a row goes to its node and a fix button
    /// applies the fix.
    /// </summary>
    public class TechTreeIssueList : VisualElement
    {
        /// <summary>
        /// Raised with a node's ID when the author clicks an issue about that node.
        /// </summary>
        public event Action<string> OnNodeRequested;

        /// <summary>
        /// Raised after a fix is applied.
        /// </summary>
        public event Action OnFixApplied;

        /// <summary>
        /// Creates an empty list.
        /// </summary>
        public TechTreeIssueList()
        {
            AddToClassList("tech-tree-issues");
        }

        /// <summary>
        /// Shows a set of issues.
        /// </summary>
        /// <param name="issues">The issues, in the order to show them.</param>
        public void Show(IReadOnlyList<TechTreeIssue> issues)
        {
            Clear();
            var header = new Label(GetSummary(issues));
            header.AddToClassList("validation-header");
            header.AddToClassList(issues.Count == 0 ? "validation-header--clean" : "validation-header--issues");
            Add(header);

            foreach (TechTreeIssue issue in issues)
            {
                Add(BuildRow(issue));
            }
        }

        /// <summary>
        /// Gets a one-line count of issues by severity.
        /// </summary>
        /// <param name="issues">The issues.</param>
        /// <returns>The count, or that nothing was found.</returns>
        public static string GetSummary(IReadOnlyCollection<TechTreeIssue> issues)
        {
            if (issues.Count == 0)
                return "No problems found";

            int errors = issues.Count(issue => issue.Issue.Severity == ValidationSeverity.Error);
            int warnings = issues.Count(issue => issue.Issue.Severity == ValidationSeverity.Warning);
            int info = issues.Count - errors - warnings;
            var parts = new List<string>();
            AddCount(parts, errors, "error");
            AddCount(parts, warnings, "warning");
            AddCount(parts, info, "note");
            return string.Join(", ", parts);
        }

        /// <summary>
        /// Gets the worst severity among some issues.
        /// </summary>
        /// <param name="issues">The issues.</param>
        /// <returns>The worst severity, or null when there are none.</returns>
        public static ValidationSeverity? GetWorst(IEnumerable<TechTreeIssue> issues) =>
            issues.Select(issue => (ValidationSeverity?)issue.Issue.Severity).DefaultIfEmpty(null).Max();

        private VisualElement BuildRow(TechTreeIssue issue)
        {
            var row = new VisualElement();
            row.AddToClassList("validation-issue-row");
            row.AddToClassList("validation-issue-row--" + issue.Issue.Severity.ToString().ToLowerInvariant());
            if (issue.NodeId != null)
            {
                row.AddToClassList("tech-tree-issues__row--linked");
                row.RegisterCallback<ClickEvent>(_ => OnNodeRequested?.Invoke(issue.NodeId));
            }

            var message = new Label(issue.Issue.Message);
            message.AddToClassList("validation-issue-label");
            row.Add(message);

            if (issue.Issue.Fixes.Count == 0)
                return row;

            var fixes = new VisualElement();
            fixes.AddToClassList("validation-issue-fixes");
            foreach (ValidationFix fix in issue.Issue.Fixes)
            {
                var button = new Button(() =>
                {
                    fix.Apply?.Invoke();
                    OnFixApplied?.Invoke();
                }) { text = fix.Label };
                button.AddToClassList("validation-issue-fix");

                // The row's own click goes to the node, a fix button only fixes
                button.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());
                fixes.Add(button);
            }

            row.Add(fixes);
            return row;
        }

        private static void AddCount(List<string> parts, int count, string noun)
        {
            if (count > 0)
            {
                parts.Add(count == 1 ? $"1 {noun}" : $"{count} {noun}s");
            }
        }
    }
}
