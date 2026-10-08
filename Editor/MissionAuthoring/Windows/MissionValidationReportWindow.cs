using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Ksp2UnityTools.Editor.MissionAuthoring.Validation;
using Ksp2UnityTools.Editor.Validation;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.MissionAuthoring.Windows
{
    /// <summary>
    /// Validation Report dashboard window: pick a mission, run cheap or full validation, browse
    /// grouped findings, apply fixes, export to markdown.
    /// </summary>
    /// <remarks>
    /// Mirrors PartValidationReportWindow. Expensive results are shared via
    /// <see cref="MissionValidationExpensiveCache" /> so a single full run also lights up the
    /// header chip in <see cref="MissionEditorWindow" />.
    /// </remarks>
    public class MissionValidationReportWindow : EditorWindow
    {
        private const string UXML_PATH = "/Assets/Windows/MissionAuthoring/MissionValidationReportWindow.uxml";
        private const string USS_PATH = "/Assets/Windows/MissionAuthoring/MissionValidationReportWindow.uss";

        private const string FILTER_PREFS_KEY = "Ksp2UnityTools.MissionValidationReport.SeverityFilters";
        private const int FILTER_ERROR_BIT = 1 << 0;
        private const int FILTER_WARNING_BIT = 1 << 1;
        private const int FILTER_INFO_BIT = 1 << 2;
        private const int FILTER_DEFAULT = FILTER_ERROR_BIT | FILTER_WARNING_BIT | FILTER_INFO_BIT;

        private ObjectField _missionField;
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

        private Mission _mission;
        private IReadOnlyList<ValidationIssue> _cheapIssues = Array.Empty<ValidationIssue>();
        private DateTime _lastRunUtc;
        private bool _hasRun;

        /// <summary>
        /// Opens the window, optionally targeting <paramref name="mission" />.
        /// </summary>
        /// <param name="mission">The mission to validate, or null to open the window with no target.</param>
        public static void Open(Mission mission)
        {
            var window = GetWindow<MissionValidationReportWindow>(utility: true, title: "Mission Validation Report");
            window.minSize = new Vector2(420f, 360f);
            if (mission != null)
            {
                window.SetMission(mission);
            }
        }

        /// <summary>Opens the window from the main menu.</summary>
        [MenuItem("Modding/Mission Authoring/Validation Report")]
        public static void ShowWindow() => Open(null);

        private void CreateGUI()
        {
            var root = rootVisualElement;
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(SDKConfiguration.BasePath + UXML_PATH);
            if (tree == null)
            {
                root.Add(new Label("Failed to load MissionValidationReportWindow.uxml"));
                return;
            }
            tree.CloneTree(root);
            Ksp2UnityToolsStyles.Apply(root, USS_PATH);

            _missionField = root.Q<ObjectField>("mission-field");
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

            _missionField.objectType = typeof(Mission);
            _missionField.allowSceneObjects = false;
            _missionField.RegisterValueChangedCallback(evt => SetMission(evt.newValue as Mission));

            _useActiveButton.clicked += UseActiveMission;
            _runQuickButton.clicked += () => Run(ValidatorCost.Cheap);
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

            MissionValidationExpensiveCache.Changed += OnExpensiveCacheChanged;

            Mission active = ActiveMissionTracker.Current;
            if (active != null)
            {
                SetMission(active);
            }
            else
            {
                Render();
            }
        }

        private void OnDisable()
        {
            MissionValidationExpensiveCache.Changed -= OnExpensiveCacheChanged;
        }

        private void OnExpensiveCacheChanged(Mission mission)
        {
            if (_mission == null) return;
            if (mission != null && mission != _mission) return;
            Render();
        }

        private void SetMission(Mission mission)
        {
            if (_mission == mission) return;
            _mission = mission;
            _cheapIssues = Array.Empty<ValidationIssue>();
            _hasRun = false;
            if (_missionField != null)
            {
                _missionField.SetValueWithoutNotify(mission);
            }
            if (mission != null)
            {
                Run(ValidatorCost.Cheap);
            }
            else
            {
                Render();
            }
        }

        private void UseActiveMission()
        {
            Mission active = ActiveMissionTracker.Current;
            if (active == null)
            {
                EditorUtility.DisplayDialog(
                    "Mission Validation Report",
                    "No active mission. Open a Mission asset in the Mission Editor window first.",
                    "OK");
                return;
            }
            _missionField?.SetValueWithoutNotify(active);
            if (_mission == active)
            {
                Run(ValidatorCost.Cheap);
                return;
            }
            SetMission(active);
        }

        private void Run(ValidatorCost? costFilter)
        {
            if (_mission == null)
            {
                Render();
                return;
            }

            if (costFilter == ValidatorCost.Cheap)
            {
                MissionValidationReport cheap = MissionValidationReport.Run(new MissionValidationContext(_mission), ValidatorCost.Cheap);
                _cheapIssues = cheap.Issues;
                _hasRun = true;
                _lastRunUtc = DateTime.UtcNow;
                Render();
                return;
            }

            MissionValidationReport cheapPart = MissionValidationReport.Run(new MissionValidationContext(_mission), ValidatorCost.Cheap);
            _cheapIssues = cheapPart.Issues;
            MissionValidationReport expensive;
            try
            {
                expensive = MissionValidationReport.Run(
                    new MissionValidationContext(_mission),
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
            MissionValidationExpensiveCache.Set(_mission, expensive.Issues);
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

            bool hasMission = _mission != null;
            _runQuickButton.SetEnabled(hasMission);
            _runFullButton.SetEnabled(hasMission);
            _exportButton.SetEnabled(hasMission && _hasRun);
            _applyAllFixesButton.SetEnabled(hasMission && _hasRun && CountAvailableFixes() > 0);

            if (!hasMission)
            {
                _summaryLabel.text = "Pick a mission to validate.";
                _lastRunLabel.text = string.Empty;
                _emptyState.style.display = DisplayStyle.Flex;
                _issuesContainer.style.display = DisplayStyle.None;
                return;
            }

            IReadOnlyList<ValidationIssue> expensive = MissionValidationExpensiveCache.Get(_mission);
            int errors = 0, warnings = 0, info = 0;
            CountBySeverity(_cheapIssues, ref errors, ref warnings, ref info);
            CountBySeverity(expensive, ref errors, ref warnings, ref info);
            int total = errors + warnings + info;

            _summaryLabel.text = total == 0
                ? "No issues found."
                : $"{errors} error{Plural(errors)}, {warnings} warning{Plural(warnings)}, {info} info";
            string expensiveStatus = MissionValidationExpensiveCache.HasRunFor(_mission)
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
                if (issue.Fixes.Count > 0) n++;
            }
            foreach (var issue in MissionValidationExpensiveCache.Get(_mission))
            {
                if (issue.Fixes.Count > 0) n++;
            }
            return n;
        }

        private void ApplyAllAutoFixes()
        {
            if (_mission == null || !_hasRun) return;
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Apply all auto-fixes");
            int applied = 0;
            int skipped = 0;
            foreach (var issue in _cheapIssues)
            {
                applied += ApplyFirstFixSafely(issue, ref skipped);
            }
            foreach (var issue in MissionValidationExpensiveCache.Get(_mission))
            {
                applied += ApplyFirstFixSafely(issue, ref skipped);
            }
            Undo.CollapseUndoOperations(undoGroup);
            Run(ValidatorCost.Cheap);
            Debug.Log($"[MissionValidationReportWindow] Applied {applied} fix{Plural(applied)}{(skipped > 0 ? $", {skipped} skipped" : string.Empty)}.");
        }

        private static int ApplyFirstFixSafely(ValidationIssue issue, ref int skipped)
        {
            if (issue.Fixes.Count == 0) return 0;
            try
            {
                issue.Fixes[0].Apply?.Invoke();
                return 1;
            }
            catch (Exception e)
            {
                Debug.LogError($"[MissionValidationReportWindow] Fix for {issue.Code} threw: {e.Message}");
                skipped++;
                return 0;
            }
        }

        private void ExportReport()
        {
            if (_mission == null || !_hasRun) return;
            string missionId = _mission.missionData?.ID;
            if (string.IsNullOrEmpty(missionId))
            {
                missionId = _mission.name;
            }

            string defaultName = $"validation-{missionId}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.md";
            string path = EditorUtility.SaveFilePanel("Export validation report", string.Empty, defaultName, "md");
            if (string.IsNullOrEmpty(path)) return;

            IReadOnlyList<ValidationIssue> expensive = MissionValidationExpensiveCache.Get(_mission);
            string contents = BuildExportText(missionId, _cheapIssues, expensive, _lastRunUtc);
            File.WriteAllText(path, contents);
            EditorUtility.RevealInFinder(path);
        }

        private static string BuildExportText(string missionId, IReadOnlyList<ValidationIssue> cheap, IReadOnlyList<ValidationIssue> expensive, DateTime runUtc)
        {
            var sb = new StringBuilder();
            sb.Append("# Validation report - ").AppendLine(missionId);
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
