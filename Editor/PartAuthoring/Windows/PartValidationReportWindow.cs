using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using KSP;
using Ksp2UnityTools.Editor.PartAuthoring.Validation;
using Ksp2UnityTools.Editor.Validation;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PartAuthoring.Windows
{
    /// <summary>
    /// Validation Report dashboard window: pick a part, run cheap or full validation, browse
    /// grouped findings, apply fixes, export to markdown.
    /// </summary>
    /// <remarks>
    /// Mirrors the planet-side ValidationReportWindow. Expensive results are shared via
    /// <see cref="PartValidationExpensiveCache" /> so a single full run also lights up the
    /// inspector chip on <see cref="Inspectors.CorePartDataEditor" />.
    /// </remarks>
    public class PartValidationReportWindow : EditorWindow
    {
        private const string UXML_PATH = "/Assets/Windows/PartAuthoring/Windows/PartValidationReportWindow.uxml";
        private const string USS_PATH = "/Assets/Windows/PartAuthoring/Windows/PartValidationReportWindow.uss";

        private const string FILTER_PREFS_KEY = "Ksp2UnityTools.PartValidationReport.SeverityFilters";
        private const int FILTER_ERROR_BIT = 1 << 0;
        private const int FILTER_WARNING_BIT = 1 << 1;
        private const int FILTER_INFO_BIT = 1 << 2;
        private const int FILTER_DEFAULT = FILTER_ERROR_BIT | FILTER_WARNING_BIT | FILTER_INFO_BIT;

        private ObjectField _partField;
        private Button _useActiveButton;
        private Button _runQuickButton;
        private Button _runFullButton;
        private Button _applyAllFixesButton;
        private Button _exportButton;
        private Toggle _showErrorsToggle;
        private Toggle _showWarningsToggle;
        private Toggle _showInfoToggle;
        private Label _summaryLabel;
        private Label _lastRunLabel;
        private VisualElement _issuesContainer;
        private VisualElement _emptyState;

        private CorePartData _part;
        private IReadOnlyList<ValidationIssue> _cheapIssues = Array.Empty<ValidationIssue>();
        private DateTime _lastRunUtc;
        private bool _hasRun;

        /// <summary>
        /// Opens the window, optionally targeting <paramref name="part" />.
        /// </summary>
        /// <param name="part">Part to focus the report on. Null leaves the previous target in place.</param>
        public static void Open(CorePartData part)
        {
            var window = GetWindow<PartValidationReportWindow>();
            window.titleContent = new GUIContent("Part Validation Report");
            window.minSize = new Vector2(420f, 360f);
            if (part != null)
            {
                window.SetPart(part);
            }
        }

        /// <summary>Opens the window from the main menu.</summary>
        [MenuItem(PartAuthoringWindows.MENU_ROOT + "Validation Report", priority = PartAuthoringWindows.PRIORITY_VALIDATION_REPORT)]
        public static void ShowWindow() => Open(null);

        private void CreateGUI()
        {
            var root = rootVisualElement;
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(SDKConfiguration.BasePath + UXML_PATH);
            if (tree == null)
            {
                root.Add(new Label("Failed to load PartValidationReportWindow.uxml"));
                return;
            }
            tree.CloneTree(root);
            Ksp2UnityToolsStyles.Apply(root, USS_PATH);

            _partField = root.Q<ObjectField>("part-field");
            _useActiveButton = root.Q<Button>("use-active-button");
            _runQuickButton = root.Q<Button>("run-quick-button");
            _runFullButton = root.Q<Button>("run-full-button");
            _applyAllFixesButton = root.Q<Button>("apply-all-fixes-button");
            _exportButton = root.Q<Button>("export-button");
            _showErrorsToggle = root.Q<Toggle>("show-errors-toggle");
            _showWarningsToggle = root.Q<Toggle>("show-warnings-toggle");
            _showInfoToggle = root.Q<Toggle>("show-info-toggle");
            _summaryLabel = root.Q<Label>("summary-label");
            _lastRunLabel = root.Q<Label>("last-run-label");
            _issuesContainer = root.Q<VisualElement>("issues-container");
            _emptyState = root.Q<VisualElement>("empty-state");

            _partField.objectType = typeof(CorePartData);
            _partField.allowSceneObjects = true;
            _partField.RegisterValueChangedCallback(evt => SetPart(evt.newValue as CorePartData));

            _useActiveButton.clicked += UseActivePart;
            _runQuickButton.clicked += () => Run(ValidatorCost.Cheap);
            // Null cost filter runs both cheap + expensive and writes the expensive results into
            // PartValidationExpensiveCache. The cache's Changed event then routes back through
            // OnExpensiveCacheChanged to refresh this window's UI (and the inspector chip).
            _runFullButton.clicked += () => Run(null);
            _applyAllFixesButton.clicked += ApplyAllAutoFixes;
            _exportButton.clicked += ExportReport;

            int filters = EditorPrefs.GetInt(FILTER_PREFS_KEY, FILTER_DEFAULT);
            _showErrorsToggle.SetValueWithoutNotify((filters & FILTER_ERROR_BIT) != 0);
            _showWarningsToggle.SetValueWithoutNotify((filters & FILTER_WARNING_BIT) != 0);
            _showInfoToggle.SetValueWithoutNotify((filters & FILTER_INFO_BIT) != 0);
            _showErrorsToggle.RegisterValueChangedCallback(_ => OnFiltersChanged());
            _showWarningsToggle.RegisterValueChangedCallback(_ => OnFiltersChanged());
            _showInfoToggle.RegisterValueChangedCallback(_ => OnFiltersChanged());

            PartValidationExpensiveCache.Changed += OnExpensiveCacheChanged;

            CorePartData active = ActivePartTracker.Current;
            if (active != null)
            {
                SetPart(active);
            }
            else
            {
                Render();
            }
        }

        private void OnDisable()
        {
            PartValidationExpensiveCache.Changed -= OnExpensiveCacheChanged;
        }

        private void OnExpensiveCacheChanged(CorePartData part)
        {
            if (_part == null) return;
            if (part != null && part != _part) return;
            Render();
        }

        private void SetPart(CorePartData part)
        {
            if (_part == part) return;
            _part = part;
            _cheapIssues = Array.Empty<ValidationIssue>();
            _hasRun = false;
            if (_partField != null)
            {
                _partField.SetValueWithoutNotify(part);
            }
            if (part != null)
            {
                Run(ValidatorCost.Cheap);
            }
            else
            {
                Render();
            }
        }

        private void UseActivePart()
        {
            CorePartData active = ActivePartTracker.Current;
            if (active == null)
            {
                EditorUtility.DisplayDialog(
                    "Part Validation Report",
                    "No active part. Select a CorePartData prefab in the Project window or open one in the prefab stage.",
                    "OK");
                return;
            }
            // Always reflect the click in the picker, even when the same part is already loaded.
            _partField?.SetValueWithoutNotify(active);
            if (_part == active)
            {
                Run(ValidatorCost.Cheap);
                return;
            }
            SetPart(active);
        }

        private void Run(ValidatorCost? costFilter)
        {
            if (_part == null)
            {
                Render();
                return;
            }

            if (costFilter == ValidatorCost.Cheap)
            {
                PartValidationReport cheap = PartValidationReport.Run(new PartValidationContext(_part), ValidatorCost.Cheap);
                _cheapIssues = cheap.Issues;
                _hasRun = true;
                _lastRunUtc = DateTime.UtcNow;
                Render();
                return;
            }

            PartValidationReport cheapPart = PartValidationReport.Run(new PartValidationContext(_part), ValidatorCost.Cheap);
            _cheapIssues = cheapPart.Issues;
            PartValidationReport expensive;
            try
            {
                expensive = PartValidationReport.Run(
                    new PartValidationContext(_part),
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
            PartValidationExpensiveCache.Set(_part, expensive.Issues);
            _hasRun = true;
            _lastRunUtc = DateTime.UtcNow;
        }

        private void OnFiltersChanged()
        {
            int filters = 0;
            if (_showErrorsToggle.value) filters |= FILTER_ERROR_BIT;
            if (_showWarningsToggle.value) filters |= FILTER_WARNING_BIT;
            if (_showInfoToggle.value) filters |= FILTER_INFO_BIT;
            EditorPrefs.SetInt(FILTER_PREFS_KEY, filters);
            Render();
        }

        private void Render()
        {
            if (_issuesContainer == null) return;
            _issuesContainer.Clear();

            bool hasPart = _part != null;
            _runQuickButton.SetEnabled(hasPart);
            _runFullButton.SetEnabled(hasPart);
            _exportButton.SetEnabled(hasPart && _hasRun);
            _applyAllFixesButton.SetEnabled(hasPart && _hasRun && CountAvailableFixes() > 0);

            if (!hasPart)
            {
                _summaryLabel.text = "Pick a part to validate.";
                _lastRunLabel.text = string.Empty;
                _emptyState.style.display = DisplayStyle.Flex;
                _issuesContainer.style.display = DisplayStyle.None;
                return;
            }

            IReadOnlyList<ValidationIssue> expensive = PartValidationExpensiveCache.Get(_part);
            int errors = 0, warnings = 0, info = 0;
            CountBySeverity(_cheapIssues, ref errors, ref warnings, ref info);
            CountBySeverity(expensive, ref errors, ref warnings, ref info);
            int total = errors + warnings + info;

            _summaryLabel.text = total == 0
                ? "No issues found."
                : $"{errors} error{Plural(errors)}, {warnings} warning{Plural(warnings)}, {info} info";
            string expensiveStatus = PartValidationExpensiveCache.HasRunFor(_part)
                ? $"ran, {expensive.Count} issue{Plural(expensive.Count)}"
                : "not run";
            _lastRunLabel.text = !_hasRun
                ? string.Empty
                : $"Last run: {_lastRunUtc.ToLocalTime():HH:mm:ss}  -  Expensive: {expensiveStatus}";

            if (total == 0)
            {
                _emptyState.style.display = DisplayStyle.Flex;
                _issuesContainer.style.display = DisplayStyle.None;
                return;
            }
            _emptyState.style.display = DisplayStyle.None;
            _issuesContainer.style.display = DisplayStyle.Flex;

            bool showErrors = _showErrorsToggle.value;
            bool showWarnings = _showWarningsToggle.value;
            bool showInfo = _showInfoToggle.value;

            // After any fix runs, re-run cheap and re-render so the row disappears without the
            // user having to click Run Quick. Expensive issues are left as-is.
            Action onFixApplied = () => Run(ValidatorCost.Cheap);

            if (showErrors && errors > 0)
                AddGroup(ValidationSeverity.Error, $"Errors ({errors})", _cheapIssues, expensive, onFixApplied);
            if (showWarnings && warnings > 0)
                AddGroup(ValidationSeverity.Warning, $"Warnings ({warnings})", _cheapIssues, expensive, onFixApplied);
            if (showInfo && info > 0)
                AddGroup(ValidationSeverity.Info, $"Info ({info})", _cheapIssues, expensive, onFixApplied);
        }

        private void AddGroup(ValidationSeverity severity, string header, IReadOnlyList<ValidationIssue> cheap, IReadOnlyList<ValidationIssue> expensive, Action onFixApplied)
        {
            var group = new VisualElement();
            group.AddToClassList("validation-report-group");
            group.AddToClassList("validation-report-group--" + severity.ToString().ToLowerInvariant());

            var headerLabel = new Label(header);
            headerLabel.AddToClassList("validation-report-group-header");
            group.Add(headerLabel);

            AddIssuesOfSeverity(group, cheap, severity, onFixApplied);
            AddIssuesOfSeverity(group, expensive, severity, onFixApplied);
            _issuesContainer.Add(group);
        }

        private static void AddIssuesOfSeverity(VisualElement parent, IReadOnlyList<ValidationIssue> issues, ValidationSeverity severity, Action onFixApplied)
        {
            foreach (ValidationIssue issue in issues)
            {
                if (issue.Severity != severity) continue;
                parent.Add(BuildIssueRow(issue, onFixApplied));
            }
        }

        private static VisualElement BuildIssueRow(ValidationIssue issue, Action onFixApplied)
        {
            var row = new VisualElement();
            row.AddToClassList("validation-issue-row");
            row.AddToClassList("validation-issue-row--" + issue.Severity.ToString().ToLowerInvariant());

            var code = new Label(issue.Code);
            code.AddToClassList("validation-report-issue-code");
            row.Add(code);

            var message = new Label(issue.Message);
            message.AddToClassList("validation-issue-label");
            row.Add(message);

            if (issue.Fixes.Count > 0)
            {
                var fixes = new VisualElement();
                fixes.AddToClassList("validation-issue-fixes");
                foreach (ValidationFix fix in issue.Fixes)
                {
                    ValidationFix captured = fix;
                    var button = new Button(() =>
                    {
                        captured.Apply?.Invoke();
                        onFixApplied?.Invoke();
                    }) { text = fix.Label };
                    button.AddToClassList("validation-issue-fix");
                    fixes.Add(button);
                }
                row.Add(fixes);
            }

            return row;
        }

        private static void CountBySeverity(IReadOnlyList<ValidationIssue> issues, ref int errors, ref int warnings, ref int info)
        {
            foreach (ValidationIssue issue in issues)
            {
                switch (issue.Severity)
                {
                    case ValidationSeverity.Error: errors++; break;
                    case ValidationSeverity.Warning: warnings++; break;
                    case ValidationSeverity.Info: info++; break;
                }
            }
        }

        private static string Plural(int n) => n == 1 ? string.Empty : "s";

        private static string Humanize(string typeName)
        {
            if (string.IsNullOrEmpty(typeName)) return typeName;
            const string Suffix = "Validator";
            if (typeName.EndsWith(Suffix)) typeName = typeName.Substring(0, typeName.Length - Suffix.Length);
            var sb = new StringBuilder(typeName.Length + 4);
            for (int i = 0; i < typeName.Length; i++)
            {
                if (i > 0 && char.IsUpper(typeName[i]) && !char.IsUpper(typeName[i - 1]))
                {
                    sb.Append(' ');
                }
                sb.Append(typeName[i]);
            }
            return sb.ToString();
        }

        private int CountAvailableFixes()
        {
            int n = 0;
            foreach (var issue in _cheapIssues)
            {
                if (issue.Fixes.Count > 0)
                {
                    n++;
                }
            }
            foreach (var issue in PartValidationExpensiveCache.Get(_part))
            {
                if (issue.Fixes.Count > 0)
                {
                    n++;
                }
            }
            return n;
        }

        private void ApplyAllAutoFixes()
        {
            if (_part == null || !_hasRun)
            {
                return;
            }
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Apply all auto-fixes");
            int applied = 0;
            int skipped = 0;
            foreach (var issue in _cheapIssues)
            {
                applied += ApplyFirstFixSafely(issue, ref skipped);
            }
            foreach (var issue in PartValidationExpensiveCache.Get(_part))
            {
                applied += ApplyFirstFixSafely(issue, ref skipped);
            }
            Undo.CollapseUndoOperations(undoGroup);
            Run(ValidatorCost.Cheap);
            Debug.Log($"[PartValidationReportWindow] Applied {applied} fix{Plural(applied)}{(skipped > 0 ? $", {skipped} skipped" : string.Empty)}.");
        }

        private static int ApplyFirstFixSafely(ValidationIssue issue, ref int skipped)
        {
            if (issue.Fixes.Count == 0)
            {
                return 0;
            }
            try
            {
                issue.Fixes[0].Apply?.Invoke();
                return 1;
            }
            catch (Exception e)
            {
                Debug.LogError($"[PartValidationReportWindow] Fix for {issue.Code} threw: {e.Message}");
                skipped++;
                return 0;
            }
        }

        private void ExportReport()
        {
            if (_part == null || !_hasRun) return;
            string partName = _part.Data?.partName;
            if (string.IsNullOrEmpty(partName))
            {
                partName = _part.gameObject != null ? _part.gameObject.name : "part";
            }

            string defaultName = $"validation-{partName}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.md";
            string path = EditorUtility.SaveFilePanel("Export validation report", string.Empty, defaultName, "md");
            if (string.IsNullOrEmpty(path)) return;

            IReadOnlyList<ValidationIssue> expensive = PartValidationExpensiveCache.Get(_part);
            string contents = BuildExportText(partName, _cheapIssues, expensive, _lastRunUtc);
            File.WriteAllText(path, contents);
            EditorUtility.RevealInFinder(path);
        }

        private static string BuildExportText(string partName, IReadOnlyList<ValidationIssue> cheap, IReadOnlyList<ValidationIssue> expensive, DateTime runUtc)
        {
            var sb = new StringBuilder();
            sb.Append("# Validation report - ").AppendLine(partName);
            sb.Append("Generated: ").Append(runUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")).AppendLine();
            sb.AppendLine();
            AppendExportGroup(sb, ValidationSeverity.Error, "Errors", cheap, expensive);
            AppendExportGroup(sb, ValidationSeverity.Warning, "Warnings", cheap, expensive);
            AppendExportGroup(sb, ValidationSeverity.Info, "Info", cheap, expensive);
            return sb.ToString();
        }

        private static void AppendExportGroup(StringBuilder sb, ValidationSeverity severity, string header, IReadOnlyList<ValidationIssue> cheap, IReadOnlyList<ValidationIssue> expensive)
        {
            int total = CountSeverity(cheap, severity) + CountSeverity(expensive, severity);
            sb.Append("## ").Append(header).Append(" (").Append(total).AppendLine(")");
            if (total == 0)
            {
                sb.AppendLine("None.");
                sb.AppendLine();
                return;
            }
            AppendIssues(sb, cheap, severity);
            AppendIssues(sb, expensive, severity);
            sb.AppendLine();
        }

        private static int CountSeverity(IReadOnlyList<ValidationIssue> issues, ValidationSeverity severity)
        {
            int n = 0;
            foreach (ValidationIssue issue in issues)
            {
                if (issue.Severity == severity) n++;
            }
            return n;
        }

        private static void AppendIssues(StringBuilder sb, IReadOnlyList<ValidationIssue> issues, ValidationSeverity severity)
        {
            foreach (ValidationIssue issue in issues)
            {
                if (issue.Severity != severity) continue;
                sb.Append("- **").Append(issue.Code).Append("**: ").AppendLine(issue.Message);
            }
        }
    }
}
