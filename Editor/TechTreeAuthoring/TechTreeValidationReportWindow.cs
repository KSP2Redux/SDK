using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Ksp2UnityTools.Editor.TechTreeAuthoring.Validation;
using Ksp2UnityTools.Editor.Validation;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.TechTreeAuthoring
{
    /// <summary>
    /// Validation Report window for a tech tree or tech node asset: run validation, browse grouped findings, apply
    /// fixes, export to markdown, and go to the node an issue is about.
    /// </summary>
    /// <remarks>
    /// Mirrors MissionValidationReportWindow. Every tech tree validator is cheap, so one Run covers them all.
    /// </remarks>
    public class TechTreeValidationReportWindow : EditorWindow
    {
        private const string UXML_PATH = "/Assets/Windows/TechTreeAuthoring/TechTreeValidationReportWindow.uxml";

        private const string USS_PATH = "/Assets/Windows/TechTreeAuthoring/TechTreeValidationReportWindow.uss";

        private const string FILTER_PREFS_KEY = "Ksp2UnityTools.TechTreeValidationReport.SeverityFilters";

        private const int FILTER_ERROR_BIT = 1 << 0;

        private const int FILTER_WARNING_BIT = 1 << 1;

        private const int FILTER_INFO_BIT = 1 << 2;

        private const int FILTER_DEFAULT = FILTER_ERROR_BIT | FILTER_WARNING_BIT | FILTER_INFO_BIT;

        private ObjectField _treeField;

        private Button _runButton;

        private Button _applyAllFixesButton;

        private Button _exportButton;

        private Toggle _showErrorsToggle;

        private Toggle _showWarningsToggle;

        private Toggle _showInfoToggle;

        private Label _summaryLabel;

        private Label _lastRunLabel;

        private VisualElement _issuesContainer;

        private VisualElement _emptyState;

        private TechTreeAsset _tree;

        private bool _snapsToGrid = true;

        private List<TechTreeIssue> _issues = new();

        private DateTime _lastRunUtc;

        private bool _hasRun;

        /// <summary>
        /// Opens the window on a tree and runs validation.
        /// </summary>
        /// <param name="tree">The tree or node asset to validate, or null to open with no target.</param>
        /// <param name="snapsToGrid">Whether the editor snaps to the stock grid, so nodes off it are noted.</param>
        public static void Open(TechTreeAsset tree, bool snapsToGrid = true)
        {
            var window = GetWindow<TechTreeValidationReportWindow>(utility: true, title: "Tech Tree Validation Report");
            window.minSize = new Vector2(420f, 360f);
            window._snapsToGrid = snapsToGrid;
            window.SetTree(tree);
        }

        /// <summary>
        /// Shows a run's counts on a readiness chip, as the part, mission and planet editors' chips show theirs.
        /// </summary>
        /// <param name="chip">The chip.</param>
        /// <param name="issues">The run's issues.</param>
        public static void ShowOnChip(VisualElement chip, IReadOnlyCollection<TechTreeIssue> issues)
        {
            int errors = issues.Count(issue => issue.Issue.Severity == ValidationSeverity.Error);
            int warnings = issues.Count(issue => issue.Issue.Severity == ValidationSeverity.Warning);
            int info = issues.Count - errors - warnings;
            if (chip is TextElement text)
            {
                text.text = issues.Count == 0 ? "No issues" : $"✕ {errors}  ·  ⚠ {warnings}  ·  ⓘ {info}";
            }

            chip.EnableInClassList("is-error", errors > 0);
            chip.EnableInClassList("is-warn", errors == 0 && warnings > 0);
            chip.EnableInClassList("is-ok", errors == 0 && warnings == 0);
        }

        /// <summary>
        /// Gets the worst severity among each node's issues, for the canvas to mark.
        /// </summary>
        /// <param name="issues">The run's issues.</param>
        /// <returns>The worst severity by node ID, for nodes with any issue.</returns>
        public static Dictionary<string, ValidationSeverity> WorstByNode(IEnumerable<TechTreeIssue> issues) =>
            issues
                .Where(issue => issue.NodeId != null)
                .GroupBy(issue => issue.NodeId)
                .ToDictionary(group => group.Key, group => group.Max(issue => issue.Issue.Severity));

        /// <summary>Opens the window from the main menu.</summary>
        [MenuItem("Modding/Tech Tree Authoring/Validation Report")]
        public static void ShowWindow() => Open(Selection.activeObject as TechTreeAsset);

        private void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(SDKConfiguration.BasePath + UXML_PATH);
            if (visualTree == null)
            {
                root.Add(new Label("Failed to load TechTreeValidationReportWindow.uxml"));
                return;
            }

            visualTree.CloneTree(root);
            Ksp2UnityToolsStyles.Apply(root, USS_PATH);

            _treeField = root.Q<ObjectField>("tree-field");
            _runButton = root.Q<Button>("run-button");
            _applyAllFixesButton = root.Q<Button>("apply-all-fixes-button");
            _exportButton = root.Q<Button>("export-button");
            _showErrorsToggle = root.Q<Toggle>("show-errors-toggle");
            _showWarningsToggle = root.Q<Toggle>("show-warnings-toggle");
            _showInfoToggle = root.Q<Toggle>("show-info-toggle");
            _summaryLabel = root.Q<Label>("summary-label");
            _lastRunLabel = root.Q<Label>("last-run-label");
            _issuesContainer = root.Q<VisualElement>("issues-container");
            _emptyState = root.Q<VisualElement>("empty-state");

            _treeField.objectType = typeof(TechTreeAsset);
            _treeField.allowSceneObjects = false;
            _treeField.SetValueWithoutNotify(_tree);
            _treeField.RegisterValueChangedCallback(evt => SetTree(evt.newValue as TechTreeAsset));

            _runButton.clicked += Run;
            _applyAllFixesButton.clicked += ApplyAllAutoFixes;
            _exportButton.clicked += ExportReport;

            int filters = EditorPrefs.GetInt(FILTER_PREFS_KEY, FILTER_DEFAULT);
            _showErrorsToggle.SetValueWithoutNotify((filters & FILTER_ERROR_BIT) != 0);
            _showWarningsToggle.SetValueWithoutNotify((filters & FILTER_WARNING_BIT) != 0);
            _showInfoToggle.SetValueWithoutNotify((filters & FILTER_INFO_BIT) != 0);
            _showErrorsToggle.RegisterValueChangedCallback(_ => OnFiltersChanged());
            _showWarningsToggle.RegisterValueChangedCallback(_ => OnFiltersChanged());
            _showInfoToggle.RegisterValueChangedCallback(_ => OnFiltersChanged());

            Run();
        }

        private void SetTree(TechTreeAsset tree)
        {
            _tree = tree;
            _issues = new List<TechTreeIssue>();
            _hasRun = false;
            _treeField?.SetValueWithoutNotify(tree);
            Run();
        }

        private void Run()
        {
            if (_tree == null || _issuesContainer == null)
            {
                Render();
                return;
            }

            // A node asset is checked against the tree it is placed in
            IReadOnlyList<TechTreeNode> reference = _tree is TechNodeAsset node ? TechTreeReference.Load(node) : null;
            _issues = TechTreeValidationReport.Run(new TechTreeValidationContext(_tree, reference, _snapsToGrid), null);
            _hasRun = true;
            _lastRunUtc = DateTime.UtcNow;
            Render();
        }

        private void OnFiltersChanged()
        {
            int filters = 0;
            if (_showErrorsToggle.value)
            {
                filters |= FILTER_ERROR_BIT;
            }

            if (_showWarningsToggle.value)
            {
                filters |= FILTER_WARNING_BIT;
            }

            if (_showInfoToggle.value)
            {
                filters |= FILTER_INFO_BIT;
            }

            EditorPrefs.SetInt(FILTER_PREFS_KEY, filters);
            Render();
        }

        private void Render()
        {
            if (_issuesContainer == null)
                return;

            _issuesContainer.Clear();
            bool hasTree = _tree != null;
            _runButton.SetEnabled(hasTree);
            _exportButton.SetEnabled(hasTree && _hasRun);
            _applyAllFixesButton.SetEnabled(hasTree && _hasRun && _issues.Any(issue => issue.Issue.Fixes.Count > 0));

            if (!hasTree)
            {
                _summaryLabel.text = "Pick a tech tree to validate.";
                _lastRunLabel.text = string.Empty;
                _emptyState.style.display = DisplayStyle.Flex;
                _issuesContainer.style.display = DisplayStyle.None;
                return;
            }

            int errors = Count(ValidationSeverity.Error);
            int warnings = Count(ValidationSeverity.Warning);
            int info = Count(ValidationSeverity.Info);
            _summaryLabel.text = _issues.Count == 0
                ? "No issues found."
                : $"{errors} error{Plural(errors)}, {warnings} warning{Plural(warnings)}, {info} info";
            _lastRunLabel.text = _hasRun ? $"Last run: {_lastRunUtc.ToLocalTime():HH:mm:ss}" : string.Empty;

            if (_issues.Count == 0)
            {
                _emptyState.style.display = DisplayStyle.Flex;
                _issuesContainer.style.display = DisplayStyle.None;
                return;
            }

            _emptyState.style.display = DisplayStyle.None;
            _issuesContainer.style.display = DisplayStyle.Flex;
            if (_showErrorsToggle.value && errors > 0)
            {
                AddGroup(ValidationSeverity.Error, $"Errors ({errors})");
            }

            if (_showWarningsToggle.value && warnings > 0)
            {
                AddGroup(ValidationSeverity.Warning, $"Warnings ({warnings})");
            }

            if (_showInfoToggle.value && info > 0)
            {
                AddGroup(ValidationSeverity.Info, $"Info ({info})");
            }
        }

        private void AddGroup(ValidationSeverity severity, string header)
        {
            var group = new VisualElement();
            group.AddToClassList("validation-report-group");
            group.AddToClassList("validation-report-group--" + severity.ToString().ToLowerInvariant());

            var headerLabel = new Label(header);
            headerLabel.AddToClassList("validation-report-group-header");
            group.Add(headerLabel);

            foreach (TechTreeIssue issue in _issues.Where(issue => issue.Issue.Severity == severity))
            {
                group.Add(BuildIssueRow(issue));
            }

            _issuesContainer.Add(group);
        }

        private VisualElement BuildIssueRow(TechTreeIssue issue)
        {
            var row = new VisualElement();
            row.AddToClassList("validation-issue-row");
            row.AddToClassList("validation-issue-row--" + issue.Issue.Severity.ToString().ToLowerInvariant());
            if (issue.NodeId != null)
            {
                row.AddToClassList("tech-tree-report__row--linked");
                row.tooltip = $"Go to {issue.NodeId}";
                row.RegisterCallback<ClickEvent>(_ => GoToNode(issue.NodeId));
            }

            var code = new Label(issue.Issue.Code);
            code.AddToClassList("validation-report-issue-code");
            row.Add(code);

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
                    Run();
                }) { text = fix.Label };
                button.AddToClassList("validation-issue-fix");

                // The row's own click goes to the node, a fix button only fixes
                button.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());
                fixes.Add(button);
            }

            row.Add(fixes);
            return row;
        }

        // A tree's node is shown in its editor, a node asset in its inspector
        private void GoToNode(string nodeId)
        {
            if (_tree is TechNodeAsset)
            {
                Selection.activeObject = _tree;
                return;
            }

            TechTreeEditorWindow.OpenFor(_tree).FocusNode(nodeId);
        }

        private void ApplyAllAutoFixes()
        {
            if (_tree == null || !_hasRun)
                return;

            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Apply all auto-fixes");
            int applied = 0;
            int skipped = 0;
            foreach (TechTreeIssue issue in _issues.Where(issue => issue.Issue.Fixes.Count > 0))
            {
                try
                {
                    issue.Issue.Fixes[0].Apply?.Invoke();
                    applied++;
                }
                catch (Exception e)
                {
                    Debug.LogError($"[TechTreeValidationReportWindow] Fix for {issue.Issue.Code} threw: {e.Message}");
                    skipped++;
                }
            }

            Undo.CollapseUndoOperations(undoGroup);
            Run();
            Debug.Log($"[TechTreeValidationReportWindow] Applied {applied} fix{(applied == 1 ? string.Empty : "es")}{(skipped > 0 ? $", {skipped} skipped" : string.Empty)}.");
        }

        private void ExportReport()
        {
            if (_tree == null || !_hasRun)
                return;

            string defaultName = $"validation-{_tree.name}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.md";
            string path = EditorUtility.SaveFilePanel("Export validation report", string.Empty, defaultName, "md");
            if (string.IsNullOrEmpty(path))
                return;

            var text = new StringBuilder();
            text.Append("# Validation report - ").AppendLine(_tree.name);
            text.Append("Generated: ").AppendLine(_lastRunUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"));
            text.AppendLine();
            AppendExportGroup(text, ValidationSeverity.Error, "Errors");
            AppendExportGroup(text, ValidationSeverity.Warning, "Warnings");
            AppendExportGroup(text, ValidationSeverity.Info, "Info");
            File.WriteAllText(path, text.ToString());
            EditorUtility.RevealInFinder(path);
        }

        private void AppendExportGroup(StringBuilder text, ValidationSeverity severity, string header)
        {
            List<TechTreeIssue> issues = _issues.Where(issue => issue.Issue.Severity == severity).ToList();
            text.Append("## ").Append(header).Append(" (").Append(issues.Count).AppendLine(")");
            if (issues.Count == 0)
            {
                text.AppendLine("None.");
            }

            foreach (TechTreeIssue issue in issues)
            {
                text.Append("- **").Append(issue.Issue.Code).Append("**: ").AppendLine(issue.Issue.Message);
            }

            text.AppendLine();
        }

        private int Count(ValidationSeverity severity) => _issues.Count(issue => issue.Issue.Severity == severity);

        private static string Plural(int n) => n == 1 ? string.Empty : "s";
    }
}
