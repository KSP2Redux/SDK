using System.Collections.Generic;
using System.Text;
using KSP;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Validation
{
    /// <summary>
    /// Builds and refreshes the in-inspector validation summary used by both the celestial body and PQS inspectors.
    /// </summary>
    /// <remarks>
    /// Call <see cref="Mount" /> once to attach a header label, the issues container, and the
    /// "Run full validation" footer to the slot. Call <see cref="Refresh" /> on the inspector's
    /// repaint tick to rebuild the cheap-validator issues. Expensive validators only run when
    /// the user clicks the footer button; their last result persists alongside cheap output.
    /// </remarks>
    public static class ValidationSectionBuilder
    {
        private const string HeaderClassClean = "validation-header--clean";
        private const string HeaderClassIssues = "validation-header--issues";
        private const string IssueRowClass = "validation-issue-row";
        private const string IssueRowSeverityPrefix = "validation-issue-row--";
        private const string IssueLabelClass = "validation-issue-label";
        private const string IssueFixesClass = "validation-issue-fixes";
        private const string IssueFixButtonClass = "validation-issue-fix";

        // Last expensive run per body (keyed by entity ID). Persists across cheap ticks so the
        // user can run expensive once and see the merged result until they re-run.
        private static readonly Dictionary<EntityId, IReadOnlyList<ValidationIssue>> _expensiveCache = new();

        /// <summary>
        /// Mounts the validation header, issues container, and "Run full validation" footer into <paramref name="slot" />.
        /// </summary>
        /// <param name="slot">The container element that receives the mounted validation widgets.</param>
        /// <returns>The handle used to refresh the mounted section.</returns>
        public static Handle Mount(VisualElement slot)
        {
            slot.Clear();
            var header = new Label();
            header.AddToClassList("validation-header");
            slot.Add(header);
            var issues = new VisualElement();
            issues.AddToClassList("validation-issues");
            slot.Add(issues);
            var footer = new VisualElement();
            footer.AddToClassList("validation-footer");
            var runFullButton = new Button { text = "Run full validation" };
            runFullButton.tooltip = "Runs the expensive validators (texture pixel scans, content-hash drift checks, full byte-array walks) once. Cheap validators continue to refresh on every tick.";
            footer.Add(runFullButton);
            var fullStatus = new Label("Expensive checks: not run yet");
            fullStatus.AddToClassList("validation-footer-status");
            footer.Add(fullStatus);
            slot.Add(footer);
            return new Handle(header, issues, runFullButton, fullStatus);
        }

        /// <summary>
        /// Re-runs only the cheap validators against <paramref name="body" /> and rewrites the section.
        /// </summary>
        /// <remarks>
        /// Expensive results from the last full-validation click are merged in.
        /// </remarks>
        /// <param name="handle">The handle returned by <see cref="Mount" />.</param>
        /// <param name="body">The body to validate. May be null.</param>
        public static void Refresh(Handle handle, CoreCelestialBodyData body)
        {
            // Wire the button to the current body on every refresh so the lambda capture stays
            // fresh when the inspector switches targets.
            CoreCelestialBodyData captured = body;
            Handle capturedHandle = handle;
            handle.RunFullButton.clickable = new Clickable(() => RunFullValidation(capturedHandle, captured));

            PlanetValidationReport cheap = PlanetValidationReport.Run(body, ValidatorCost.Cheap);
            IReadOnlyList<ValidationIssue> expensive = GetCachedExpensive(body);
            string fingerprint = ComputeFingerprint(cheap.Issues, expensive);
            if (string.Equals(fingerprint, handle.Issues.userData as string))
                return;
            handle.Issues.userData = fingerprint;
            Render(handle, cheap.Issues, expensive);
        }

        private static void RunFullValidation(Handle handle, CoreCelestialBodyData body)
        {
            if (body == null) return;
            PlanetValidationReport expensive;
            try
            {
                expensive = PlanetValidationReport.Run(
                    body,
                    ValidatorCost.Expensive,
                    progress: (frac, name) => EditorUtility.DisplayProgressBar(
                        "Running full validation",
                        $"Running {Humanize(name)}...",
                        frac));
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            _expensiveCache[body.GetEntityId()] = expensive.Issues;
            handle.FullStatus.text = $"Expensive checks: ran, {expensive.Issues.Count} issue(s)";
            // Force a re-render now: clear the fingerprint so Refresh below produces a new one.
            handle.Issues.userData = null;
            Refresh(handle, body);
        }

        // Strip the "Validator" suffix and convert from CamelCase to "Camel Case" so the progress
        // bar shows something readable instead of "ScienceRegionUnmappedPixelsValidator".
        private static string Humanize(string typeName)
        {
            if (string.IsNullOrEmpty(typeName)) return typeName;
            const string Suffix = "Validator";
            if (typeName.EndsWith(Suffix)) typeName = typeName.Substring(0, typeName.Length - Suffix.Length);
            var sb = new StringBuilder(typeName.Length + 4);
            for (int i = 0; i < typeName.Length; i++)
            {
                if (i > 0 && char.IsUpper(typeName[i]) && !char.IsUpper(typeName[i - 1]))
                    sb.Append(' ');
                sb.Append(typeName[i]);
            }
            return sb.ToString();
        }

        private static IReadOnlyList<ValidationIssue> GetCachedExpensive(CoreCelestialBodyData body)
        {
            if (body == null) return System.Array.Empty<ValidationIssue>();
            return _expensiveCache.TryGetValue(body.GetEntityId(), out IReadOnlyList<ValidationIssue> cached)
                ? cached
                : System.Array.Empty<ValidationIssue>();
        }

        private static void Render(Handle handle, IReadOnlyList<ValidationIssue> cheap, IReadOnlyList<ValidationIssue> expensive)
        {
            handle.Issues.Clear();
            handle.Header.RemoveFromClassList(HeaderClassClean);
            handle.Header.RemoveFromClassList(HeaderClassIssues);

            int total = cheap.Count + expensive.Count;
            if (total == 0)
            {
                handle.Header.text = "No issues found.";
                handle.Header.AddToClassList(HeaderClassClean);
                return;
            }

            handle.Header.text = total == 1
                ? "Validation (1 issue):"
                : "Validation (" + total + " issues):";
            handle.Header.AddToClassList(HeaderClassIssues);

            foreach (ValidationIssue issue in cheap)
                handle.Issues.Add(BuildIssueRow(issue));
            foreach (ValidationIssue issue in expensive)
                handle.Issues.Add(BuildIssueRow(issue));
        }

        private static string ComputeFingerprint(IReadOnlyList<ValidationIssue> cheap, IReadOnlyList<ValidationIssue> expensive)
        {
            if (cheap.Count == 0 && expensive.Count == 0)
                return "clean";
            var sb = new StringBuilder((cheap.Count + expensive.Count) * 32);
            AppendIssues(sb, cheap);
            sb.Append("||");
            AppendIssues(sb, expensive);
            return sb.ToString();
        }

        private static void AppendIssues(StringBuilder sb, IReadOnlyList<ValidationIssue> issues)
        {
            foreach (ValidationIssue issue in issues)
            {
                sb.Append((int)issue.Severity).Append('|');
                sb.Append(issue.Code).Append('|');
                sb.Append(issue.Message).Append('|');
                if (issue.Fixes != null)
                {
                    for (int i = 0; i < issue.Fixes.Count; i++)
                        sb.Append(issue.Fixes[i].Label).Append(',');
                }
                sb.Append(';');
            }
        }

        private static VisualElement BuildIssueRow(ValidationIssue issue)
        {
            var row = new VisualElement();
            row.AddToClassList(IssueRowClass);
            row.AddToClassList(IssueRowSeverityPrefix + issue.Severity.ToString().ToLowerInvariant());

            var label = new Label(issue.Message);
            label.AddToClassList(IssueLabelClass);
            row.Add(label);

            if (issue.Fixes.Count > 0)
            {
                var fixes = new VisualElement();
                fixes.AddToClassList(IssueFixesClass);
                foreach (ValidationFix fix in issue.Fixes)
                {
                    var button = new Button(fix.Apply) { text = fix.Label };
                    button.AddToClassList(IssueFixButtonClass);
                    fixes.Add(button);
                }
                row.Add(fixes);
            }

            return row;
        }

        /// <summary>
        /// Mounted-section handle returned by <see cref="Mount" /> and consumed by <see cref="Refresh" />.
        /// </summary>
        public readonly struct Handle
        {
            internal Handle(Label header, VisualElement issues, Button runFullButton, Label fullStatus)
            {
                Header = header;
                Issues = issues;
                RunFullButton = runFullButton;
                FullStatus = fullStatus;
            }

            internal Label Header { get; }
            internal VisualElement Issues { get; }
            internal Button RunFullButton { get; }
            internal Label FullStatus { get; }

            /// <summary>True when this handle was returned from a successful mount.</summary>
            public bool IsValid => Header != null && Issues != null && RunFullButton != null;
        }
    }
}
