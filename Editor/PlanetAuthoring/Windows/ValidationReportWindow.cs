using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using KSP;
using Ksp2UnityTools.Editor.PlanetAuthoring.Validation;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Windows
{
    /// <summary>
    /// Validation Report dashboard window: pick a body, run cheap or full validation, browse grouped findings.
    /// </summary>
    /// <remarks>
    /// Replaces the per-inspector foldout as the primary surface for full validation runs. The inspector foldout
    /// remains the always-on cheap-tick view. Expensive results are shared via <see cref="ValidationExpensiveCache" />
    /// so running full validation here also lights up issues in the inspector and the inline severity chip.
    /// </remarks>
    public class ValidationReportWindow : EditorWindow
    {
        private const string UxmlPath = "/Assets/Windows/PlanetAuthoring/Windows/ValidationReportWindow.uxml";
        private const string UssPath = "/Assets/Windows/PlanetAuthoring/Windows/ValidationReportWindow.uss";

        private const string FilterPrefsKey = "Ksp2UnityTools.ValidationReport.SeverityFilters";
        private const int FilterErrorBit = 1 << 0;
        private const int FilterWarningBit = 1 << 1;
        private const int FilterInfoBit = 1 << 2;
        private const int FilterDefault = FilterErrorBit | FilterWarningBit | FilterInfoBit;

        private ObjectField _bodyField;
        private Button _useActiveButton;
        private Button _runQuickButton;
        private Button _runFullButton;
        private Button _exportButton;
        private Toggle _showErrorsToggle;
        private Toggle _showWarningsToggle;
        private Toggle _showInfoToggle;
        private Label _summaryLabel;
        private Label _lastRunLabel;
        private VisualElement _issuesContainer;
        private VisualElement _emptyState;
        private Button _vramComputeButton;
        private VisualElement _vramContent;
        private Label _vramTotalLabel;
        private VisualElement _vramBarFill;
        private VisualElement _vramCategoriesContainer;
        private VisualElement _vramTopContainer;
        private Label _vramStreamingLabel;

        private CoreCelestialBodyData _body;
        private IReadOnlyList<ValidationIssue> _cheapIssues = Array.Empty<ValidationIssue>();
        private DateTime _lastRunUtc;
        private bool _hasRun;

        /// <summary>
        /// Opens the Validation Report window, optionally targeting <paramref name="body" />.
        /// </summary>
        /// <param name="body">Body to focus the report on. Null leaves the previous target in place.</param>
        public static void Open(CoreCelestialBodyData body)
        {
            var window = GetWindow<ValidationReportWindow>();
            window.titleContent = new GUIContent("Validation Report");
            window.minSize = new Vector2(420f, 360f);
            if (body != null)
                window.SetBody(body);
        }

        /// <summary>
        /// Opens the Validation Report window from the main menu.
        /// </summary>
        [MenuItem(PlanetAuthoringWindows.MenuRoot + "Validation Report", priority = PlanetAuthoringWindows.PriorityValidationReport)]
        public static void ShowWindow() => Open(null);

        /// <inheritdoc />
        private void CreateGUI()
        {
            var root = rootVisualElement;
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(SDKConfiguration.BasePath + UxmlPath);
            if (tree == null)
            {
                root.Add(new Label("Failed to load ValidationReportWindow.uxml"));
                return;
            }
            tree.CloneTree(root);
            Ksp2UnityToolsStyles.Apply(root, UssPath);

            _bodyField = root.Q<ObjectField>("body-field");
            _useActiveButton = root.Q<Button>("use-active-button");
            _runQuickButton = root.Q<Button>("run-quick-button");
            _runFullButton = root.Q<Button>("run-full-button");
            _exportButton = root.Q<Button>("export-button");
            _showErrorsToggle = root.Q<Toggle>("show-errors-toggle");
            _showWarningsToggle = root.Q<Toggle>("show-warnings-toggle");
            _showInfoToggle = root.Q<Toggle>("show-info-toggle");
            _summaryLabel = root.Q<Label>("summary-label");
            _lastRunLabel = root.Q<Label>("last-run-label");
            _issuesContainer = root.Q<VisualElement>("issues-container");
            _emptyState = root.Q<VisualElement>("empty-state");
            _vramComputeButton = root.Q<Button>("vram-compute-button");
            _vramContent = root.Q<VisualElement>("vram-content");
            _vramTotalLabel = root.Q<Label>("vram-total-label");
            _vramBarFill = root.Q<VisualElement>("vram-bar-fill");
            _vramCategoriesContainer = root.Q<VisualElement>("vram-categories");
            _vramTopContainer = root.Q<VisualElement>("vram-top");
            _vramStreamingLabel = root.Q<Label>("vram-streaming-label");
            if (_vramComputeButton != null)
                _vramComputeButton.clicked += ComputeVramBreakdown;

            _bodyField.objectType = typeof(CoreCelestialBodyData);
            _bodyField.allowSceneObjects = true;
            _bodyField.RegisterValueChangedCallback(evt => SetBody(evt.newValue as CoreCelestialBodyData));

            _useActiveButton.clicked += UseActiveSessionBody;
            _runQuickButton.clicked += () => Run(ValidatorCost.Cheap);
            _runFullButton.clicked += () => Run(null);
            _exportButton.clicked += ExportReport;

            int filters = EditorPrefs.GetInt(FilterPrefsKey, FilterDefault);
            _showErrorsToggle.SetValueWithoutNotify((filters & FilterErrorBit) != 0);
            _showWarningsToggle.SetValueWithoutNotify((filters & FilterWarningBit) != 0);
            _showInfoToggle.SetValueWithoutNotify((filters & FilterInfoBit) != 0);
            _showErrorsToggle.RegisterValueChangedCallback(_ => OnFiltersChanged());
            _showWarningsToggle.RegisterValueChangedCallback(_ => OnFiltersChanged());
            _showInfoToggle.RegisterValueChangedCallback(_ => OnFiltersChanged());

            ValidationExpensiveCache.Changed += OnExpensiveCacheChanged;

            // Default target: the session body when one is active.
            CoreCelestialBodyData session = PlanetAuthoringSession.Active?.Body;
            if (session != null)
                SetBody(session);
            else
                Render();
        }

        private void OnDisable()
        {
            ValidationExpensiveCache.Changed -= OnExpensiveCacheChanged;
        }

        private void OnExpensiveCacheChanged(CoreCelestialBodyData body)
        {
            if (_body == null) return;
            if (body != null && body != _body) return;
            Render();
        }

        private void SetBody(CoreCelestialBodyData body)
        {
            if (_body == body) return;
            _body = body;
            _cheapIssues = Array.Empty<ValidationIssue>();
            _hasRun = false;
            ResetVramPanel();
            if (_bodyField != null)
                _bodyField.SetValueWithoutNotify(body);
            if (body != null)
                Run(ValidatorCost.Cheap);
            else
                Render();
        }

        private void ResetVramPanel()
        {
            if (_vramContent != null) _vramContent.style.display = DisplayStyle.None;
        }

        private void ComputeVramBreakdown()
        {
            if (_vramContent == null) return;
            if (_body == null)
            {
                _vramContent.style.display = DisplayStyle.Flex;
                _vramTotalLabel.text = "Pick a body first.";
                _vramBarFill.style.width = new StyleLength(new Length(0f, LengthUnit.Percent));
                _vramCategoriesContainer.Clear();
                _vramTopContainer.Clear();
                _vramStreamingLabel.text = string.Empty;
                return;
            }
            TextureBudgetEnumerator calc;
            try
            {
                EditorUtility.DisplayProgressBar("VRAM breakdown", "Walking textures...", 0.5f);
                calc = TextureBudgetEnumerator.Compute(_body);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            RenderVramPanel(calc);
        }

        private void RenderVramPanel(TextureBudgetEnumerator calc)
        {
            _vramContent.style.display = DisplayStyle.Flex;
            long budget = Validation.Validators.Performance.VramBudgetValidator.BudgetBytes;
            double pct = budget > 0 ? (double)calc.TotalBytes / budget * 100.0 : 0.0;

            _vramTotalLabel.text = $"Total: {TextureBudgetEnumerator.FormatSize(calc.TotalBytes)} / {TextureBudgetEnumerator.FormatSize(budget)} ({pct:0.#}%)";

            float fillPct = (float)System.Math.Min(100.0, pct);
            _vramBarFill.style.width = new StyleLength(new Length(fillPct, LengthUnit.Percent));
            _vramBarFill.RemoveFromClassList("vram-bar-fill--near");
            _vramBarFill.RemoveFromClassList("vram-bar-fill--over");
            if (pct >= 100.0) _vramBarFill.AddToClassList("vram-bar-fill--over");
            else if (pct >= 80.0) _vramBarFill.AddToClassList("vram-bar-fill--near");

            _vramCategoriesContainer.Clear();
            if (calc.BytesByCategory.Count == 0)
            {
                _vramCategoriesContainer.Add(MakeHintRow("No textures reachable from this body."));
            }
            else
            {
                var sortedCats = new List<KeyValuePair<string, long>>(calc.BytesByCategory);
                sortedCats.Sort((a, b) => b.Value.CompareTo(a.Value));
                foreach (var kv in sortedCats)
                {
                    int count = calc.CountByCategory.TryGetValue(kv.Key, out int c) ? c : 0;
                    string meta = count == 1 ? "1 texture" : $"{count} textures";
                    _vramCategoriesContainer.Add(MakeRow(kv.Key, meta, TextureBudgetEnumerator.FormatSize(kv.Value)));
                }
            }

            _vramTopContainer.Clear();
            if (calc.Entries.Count == 0)
            {
                _vramTopContainer.Add(MakeHintRow("No textures."));
            }
            else
            {
                var sortedEntries = new List<TextureBudgetEnumerator.Entry>(calc.Entries);
                sortedEntries.Sort((a, b) => b.Bytes.CompareTo(a.Bytes));
                int n = System.Math.Min(10, sortedEntries.Count);
                for (int i = 0; i < n; i++)
                {
                    var e = sortedEntries[i];
                    _vramTopContainer.Add(MakeTextureRow(e));
                }
            }

            _vramStreamingLabel.text = calc.Entries.Count == 0
                ? string.Empty
                : $"Streaming mip-maps: enabled on {calc.StreamingMipsCount} / {calc.Entries.Count} textures (resident size lower in play mode).";
        }

        private static VisualElement MakeRow(string label, string meta, string size)
        {
            var row = new VisualElement();
            row.AddToClassList("vram-row");
            var l = new Label(label);
            l.AddToClassList("vram-row-label");
            row.Add(l);
            if (!string.IsNullOrEmpty(meta))
            {
                var m = new Label(meta);
                m.AddToClassList("vram-row-meta");
                row.Add(m);
            }
            var s = new Label(size);
            s.AddToClassList("vram-row-size");
            row.Add(s);
            return row;
        }

        private static VisualElement MakeTextureRow(TextureBudgetEnumerator.Entry e)
        {
            string name = TextureBudgetEnumerator.TextureName(e.Tex);
            string dims = TextureBudgetEnumerator.TextureDimensions(e.Tex);
            string size = TextureBudgetEnumerator.FormatSize(e.Bytes);

            var row = new VisualElement();
            row.AddToClassList("vram-tex-row");
            row.tooltip = $"{name}\n{dims}\n{e.Category}\n{size}\n\nClick to ping the asset in the Project window.";

            var top = new VisualElement();
            top.AddToClassList("vram-tex-row-top");
            var nameLabel = new Label(name);
            nameLabel.AddToClassList("vram-tex-row-name");
            top.Add(nameLabel);
            var sizeLabel = new Label(size);
            sizeLabel.AddToClassList("vram-tex-row-size");
            top.Add(sizeLabel);
            row.Add(top);

            var metaParts = new List<string>(3);
            if (!string.IsNullOrEmpty(dims)) metaParts.Add(dims);
            if (!string.IsNullOrEmpty(e.Category)) metaParts.Add(e.Category);
            if (!string.IsNullOrEmpty(e.Detail)) metaParts.Add(e.Detail);
            var metaLabel = new Label(string.Join("  ·  ", metaParts));
            metaLabel.AddToClassList("vram-tex-row-meta");
            row.Add(metaLabel);

            if (e.Tex != null)
                row.RegisterCallback<ClickEvent>(_ => EditorGUIUtility.PingObject(e.Tex));
            return row;
        }

        private static VisualElement MakeHintRow(string text)
        {
            var label = new Label(text);
            label.AddToClassList("sdk-hint");
            return label;
        }

        private void UseActiveSessionBody()
        {
            CoreCelestialBodyData session = PlanetAuthoringSession.Active?.Body;
            if (session == null)
            {
                EditorUtility.DisplayDialog(
                    "Validation Report",
                    "No active planet authoring session. Open an authoring scene and click Enable Preview on a body, or pick a body asset above.",
                    "OK");
                return;
            }
            SetBody(session);
        }

        private void Run(ValidatorCost? costFilter)
        {
            if (_body == null)
            {
                Render();
                return;
            }

            if (costFilter == ValidatorCost.Cheap)
            {
                PlanetValidationReport cheap = PlanetValidationReport.Run(_body, ValidatorCost.Cheap);
                _cheapIssues = cheap.Issues;
                _hasRun = true;
                _lastRunUtc = DateTime.UtcNow;
                Render();
                return;
            }

            PlanetValidationReport cheapPart = PlanetValidationReport.Run(_body, ValidatorCost.Cheap);
            _cheapIssues = cheapPart.Issues;
            PlanetValidationReport expensive;
            try
            {
                expensive = PlanetValidationReport.Run(
                    _body,
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
            ValidationExpensiveCache.Set(_body, expensive.Issues);
            _hasRun = true;
            _lastRunUtc = DateTime.UtcNow;
        }

        private void OnFiltersChanged()
        {
            int filters = 0;
            if (_showErrorsToggle.value) filters |= FilterErrorBit;
            if (_showWarningsToggle.value) filters |= FilterWarningBit;
            if (_showInfoToggle.value) filters |= FilterInfoBit;
            EditorPrefs.SetInt(FilterPrefsKey, filters);
            Render();
        }

        private void Render()
        {
            if (_issuesContainer == null) return;
            _issuesContainer.Clear();

            bool hasBody = _body != null;
            _runQuickButton.SetEnabled(hasBody);
            _runFullButton.SetEnabled(hasBody);
            _exportButton.SetEnabled(hasBody && _hasRun);

            if (!hasBody)
            {
                _summaryLabel.text = "Pick a body to validate.";
                _lastRunLabel.text = string.Empty;
                _emptyState.style.display = DisplayStyle.Flex;
                _issuesContainer.style.display = DisplayStyle.None;
                return;
            }

            IReadOnlyList<ValidationIssue> expensive = ValidationExpensiveCache.Get(_body);
            int errors = 0, warnings = 0, info = 0;
            CountBySeverity(_cheapIssues, ref errors, ref warnings, ref info);
            CountBySeverity(expensive, ref errors, ref warnings, ref info);
            int total = errors + warnings + info;

            _summaryLabel.text = total == 0
                ? "No issues found."
                : $"{errors} error{Plural(errors)}, {warnings} warning{Plural(warnings)}, {info} info";
            string expensiveStatus = ValidationExpensiveCache.HasRunFor(_body)
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

            // After any fix button runs, re-run cheap validators and re-render so the row
            // disappears (or its message updates) without the user having to click Run Quick.
            // Expensive issues are intentionally left as-is - re-running them on every fix would
            // be too slow.
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
                    sb.Append(' ');
                sb.Append(typeName[i]);
            }
            return sb.ToString();
        }

        private void ExportReport()
        {
            if (_body == null || !_hasRun) return;
            string bodyName = _body.Data?.bodyName;
            if (string.IsNullOrEmpty(bodyName))
                bodyName = _body.gameObject != null ? _body.gameObject.name : "body";

            string defaultName = $"validation-{bodyName}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.md";
            string path = EditorUtility.SaveFilePanel("Export validation report", string.Empty, defaultName, "md");
            if (string.IsNullOrEmpty(path)) return;

            IReadOnlyList<ValidationIssue> expensive = ValidationExpensiveCache.Get(_body);
            string contents = BuildExportText(bodyName, _cheapIssues, expensive, _lastRunUtc);
            File.WriteAllText(path, contents);
            EditorUtility.RevealInFinder(path);
        }

        private static string BuildExportText(string bodyName, IReadOnlyList<ValidationIssue> cheap, IReadOnlyList<ValidationIssue> expensive, DateTime runUtc)
        {
            var sb = new StringBuilder();
            sb.Append("# Validation report - ").AppendLine(bodyName);
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
                if (issue.Severity == severity) n++;
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
