using System.Collections.Generic;
using System.IO;
using System.Linq;
using KSP;
using Ksp2UnityTools.Editor.Modding;
using Ksp2UnityTools.Editor.PartAuthoring.Windows;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PartAuthoring
{
    public sealed class Ksp1ModConverterWindow : EditorWindow
    {
        private const string USS_PATH = "/Assets/Windows/PartAuthoring/Windows/Ksp1ModConverterWindow.uss";

        private Mod _targetMod;
        private string _sourcePathOverride = "";
        private string _partFilter = "";
        private bool _overwriteGenerated = true;
        private string _lastReport = "";
        private readonly List<Ksp1EditorPartSelection> _scannedParts = new();
        private string _scannedSourcePath = "";

        private ObjectField _targetModField;
        private Toggle _overwriteToggle;
        private Button _clearSourceButton;
        private Button _scanButton;
        private Button _convertButton;
        private Button _openReportButton;
        private Button _selectAllButton;
        private Button _clearSelectionButton;
        private Button _selectVisibleButton;
        private Button _clearVisibleButton;
        private TextField _partFilterField;
        private VisualElement _dropZone;
        private Label _dropZoneLabel;
        private VisualElement _partSelectionPanel;
        private ScrollView _partListScroll;
        private Label _partSelectionSummaryLabel;
        private Label _sourceSummaryLabel;
        private Label _reportSummaryLabel;
        private HelpBox _validationHelp;

        [MenuItem(PartAuthoringWindows.MENU_ROOT + "KSP1 Mod Converter", priority = PartAuthoringWindows.PRIORITY_KSP1_MOD_CONVERTER)]
        public static void Open()
        {
            ShowWindow(null);
        }

        public static void Open(Mod mod)
        {
            ShowWindow(mod);
        }

        private static void ShowWindow(Mod mod)
        {
            Ksp1ModConverterWindow window = GetWindow<Ksp1ModConverterWindow>();
            window.titleContent = new GUIContent("KSP1 Mod Converter");
            window.minSize = new Vector2(540f, 420f);
            if (mod != null)
            {
                window._targetMod = mod;
                window._targetModField?.SetValueWithoutNotify(mod);
                window.Refresh();
            }
            window.Show();
        }

        private void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.Clear();
            Ksp2UnityToolsStyles.Apply(
                root,
                "/Assets/Windows/PartAuthoring/Inspectors/CorePartDataEditor.uss",
                USS_PATH
            );

            ScrollView scroll = new(ScrollViewMode.Vertical);
            scroll.AddToClassList("ksp1-converter-scroll");
            root.Add(scroll);

            VisualElement shell = new();
            shell.AddToClassList("ksp1-converter-root");
            scroll.Add(shell);

            BuildHeader(shell);
            BuildInputPanel(shell);
            BuildPartSelectionPanel(shell);
            BuildReportPanel(shell);
            Refresh();
        }

        private void BuildHeader(VisualElement root)
        {
            VisualElement header = new();
            header.AddToClassList("part-inspector-header");
            header.AddToClassList("ksp1-converter-header");
            root.Add(header);

            Label title = new("KSP1 Mod Converter");
            title.AddToClassList("part-inspector-part-name");
            title.AddToClassList("ksp1-converter-title");
            header.Add(title);

            Label subtitle = new("Convert KSP1 part-mod folders into generated KSP2 prefabs, part JSON, resources, localization, and plume variants.");
            subtitle.AddToClassList("ksp1-converter-header-subtitle");
            header.Add(subtitle);
        }

        private void BuildInputPanel(VisualElement root)
        {
            VisualElement panel = new();
            panel.AddToClassList("ksp1-converter-panel");
            root.Add(panel);

            Label sectionTitle = new("Import Source");
            sectionTitle.AddToClassList("part-inspector-section-label");
            sectionTitle.AddToClassList("ksp1-converter-section-title");
            panel.Add(sectionTitle);

            _targetModField = new ObjectField("Target Redux Mod")
            {
                objectType = typeof(Mod),
                allowSceneObjects = false,
                value = _targetMod
            };
            _targetModField.AddToClassList("ksp1-converter-field");
            _targetModField.RegisterValueChangedCallback(evt =>
            {
                _targetMod = evt.newValue as Mod;
                Refresh();
            });
            panel.Add(_targetModField);

            Label sourceTitle = new("Source Folder");
            sourceTitle.AddToClassList("part-inspector-section-label");
            sourceTitle.AddToClassList("ksp1-converter-subsection-title");
            panel.Add(sourceTitle);

            _dropZone = new VisualElement();
            _dropZone.AddToClassList("ksp1-converter-drop-zone");
            _dropZone.RegisterCallback<DragUpdatedEvent>(OnFolderDragUpdated);
            _dropZone.RegisterCallback<DragPerformEvent>(OnFolderDragPerform);
            _dropZone.RegisterCallback<DragLeaveEvent>(_ => _dropZone.RemoveFromClassList("is-dragging"));
            _dropZone.RegisterCallback<ClickEvent>(_ => OnBrowse());
            panel.Add(_dropZone);

            Label dropTitle = new("Click to Choose KSP1 Source Folder");
            dropTitle.AddToClassList("ksp1-converter-drop-title");
            _dropZone.Add(dropTitle);

            _dropZoneLabel = new("Opens a folder picker. You can also drag GameData or a KSP1 mod folder here.");
            _dropZoneLabel.AddToClassList("ksp1-converter-drop-detail");
            _dropZone.Add(_dropZoneLabel);

            _sourceSummaryLabel = new();
            _sourceSummaryLabel.AddToClassList("ksp1-converter-selected-source");
            panel.Add(_sourceSummaryLabel);

            VisualElement sourceButtons = new();
            sourceButtons.AddToClassList("ksp1-converter-source-buttons");
            panel.Add(sourceButtons);

            _clearSourceButton = new Button(ClearSource)
            {
                text = "Clear Source"
            };
            _clearSourceButton.AddToClassList("ksp1-converter-small-button");
            sourceButtons.Add(_clearSourceButton);

            _overwriteToggle = new Toggle("Overwrite generated assets")
            {
                value = _overwriteGenerated
            };
            _overwriteToggle.AddToClassList("ksp1-converter-field");
            _overwriteToggle.RegisterValueChangedCallback(evt => _overwriteGenerated = evt.newValue);
            panel.Add(_overwriteToggle);

            _validationHelp = new HelpBox("", HelpBoxMessageType.Info);
            _validationHelp.AddToClassList("ksp1-converter-help");
            panel.Add(_validationHelp);

            VisualElement actionRow = new();
            actionRow.AddToClassList("ksp1-converter-action-row");
            panel.Add(actionRow);

            _scanButton = new Button(OnScanParts)
            {
                text = "Load Part List"
            };
            _scanButton.AddToClassList("part-inspector-chip");
            _scanButton.AddToClassList("ksp1-converter-secondary-button");
            actionRow.Add(_scanButton);

            _convertButton = new Button(OnConvert)
            {
                text = "Convert Selected Parts"
            };
            _convertButton.AddToClassList("part-inspector-chip");
            _convertButton.AddToClassList("ksp1-converter-primary-button");
            actionRow.Add(_convertButton);
        }

        private void BuildPartSelectionPanel(VisualElement root)
        {
            _partSelectionPanel = new VisualElement();
            _partSelectionPanel.AddToClassList("ksp1-converter-panel");
            _partSelectionPanel.AddToClassList("ksp1-converter-part-panel");
            root.Add(_partSelectionPanel);

            VisualElement titleRow = new();
            titleRow.AddToClassList("ksp1-converter-part-title-row");
            _partSelectionPanel.Add(titleRow);

            Label title = new("Parts To Convert");
            title.AddToClassList("part-inspector-section-label");
            title.AddToClassList("ksp1-converter-section-title");
            titleRow.Add(title);

            _partSelectionSummaryLabel = new("Load a part list to choose what gets converted.");
            _partSelectionSummaryLabel.AddToClassList("ksp1-converter-summary");
            titleRow.Add(_partSelectionSummaryLabel);

            _partFilterField = new TextField("Filter")
            {
                value = _partFilter
            };
            _partFilterField.AddToClassList("ksp1-converter-part-filter");
            _partFilterField.RegisterValueChangedCallback(evt =>
            {
                _partFilter = evt.newValue ?? "";
                RebuildPartList();
                Refresh();
            });
            _partSelectionPanel.Add(_partFilterField);

            VisualElement selectionActions = new();
            selectionActions.AddToClassList("ksp1-converter-selection-actions");
            _partSelectionPanel.Add(selectionActions);

            _selectAllButton = new Button(() => SetAllPartSelections(true))
            {
                text = "Select All"
            };
            _selectAllButton.AddToClassList("ksp1-converter-small-button");
            selectionActions.Add(_selectAllButton);

            _clearSelectionButton = new Button(() => SetAllPartSelections(false))
            {
                text = "Clear"
            };
            _clearSelectionButton.AddToClassList("ksp1-converter-small-button");
            selectionActions.Add(_clearSelectionButton);

            _selectVisibleButton = new Button(() => SetVisiblePartSelections(true))
            {
                text = "Select Visible"
            };
            _selectVisibleButton.AddToClassList("ksp1-converter-small-button");
            _selectVisibleButton.AddToClassList("ksp1-converter-filter-button");
            selectionActions.Add(_selectVisibleButton);

            _clearVisibleButton = new Button(() => SetVisiblePartSelections(false))
            {
                text = "Clear Visible"
            };
            _clearVisibleButton.AddToClassList("ksp1-converter-small-button");
            _clearVisibleButton.AddToClassList("ksp1-converter-filter-button");
            selectionActions.Add(_clearVisibleButton);

            _partListScroll = new ScrollView(ScrollViewMode.Vertical);
            _partListScroll.AddToClassList("ksp1-converter-part-list");
            _partSelectionPanel.Add(_partListScroll);

            RebuildPartList();
        }

        private void BuildReportPanel(VisualElement root)
        {
            VisualElement panel = new();
            panel.AddToClassList("ksp1-converter-panel");
            panel.AddToClassList("ksp1-converter-report-panel");
            root.Add(panel);

            VisualElement titleRow = new();
            titleRow.AddToClassList("ksp1-converter-report-title-row");
            panel.Add(titleRow);

            Label title = new("Summary");
            title.AddToClassList("part-inspector-section-label");
            title.AddToClassList("ksp1-converter-section-title");
            titleRow.Add(title);

            _reportSummaryLabel = new("No conversion has run in this window.");
            _reportSummaryLabel.AddToClassList("ksp1-converter-summary");
            titleRow.Add(_reportSummaryLabel);

            VisualElement actionRow = new();
            actionRow.AddToClassList("ksp1-converter-report-actions");
            panel.Add(actionRow);

            _openReportButton = new Button(OpenLastReport)
            {
                text = "Open Full Report"
            };
            _openReportButton.AddToClassList("ksp1-converter-small-button");
            actionRow.Add(_openReportButton);
        }

        private void OnBrowse()
        {
            string startPath = !string.IsNullOrWhiteSpace(_sourcePathOverride) && Directory.Exists(_sourcePathOverride)
                ? _sourcePathOverride
                : Directory.GetCurrentDirectory();
            string selected = EditorUtility.OpenFolderPanel("Select KSP1 GameData or Mod Folder", startPath, "");
            if (string.IsNullOrWhiteSpace(selected))
            {
                return;
            }

            _sourcePathOverride = selected;
            ResetScannedParts();
            Refresh();
        }

        private void ClearSource()
        {
            _sourcePathOverride = "";
            ResetScannedParts();
            Refresh();
        }

        private void OnFolderDragUpdated(DragUpdatedEvent evt)
        {
            if (TryGetDraggedFolder(out _, out _))
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                _dropZone.AddToClassList("is-dragging");
                evt.StopPropagation();
            }
            else
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Rejected;
            }
        }

        private void OnFolderDragPerform(DragPerformEvent evt)
        {
            _dropZone.RemoveFromClassList("is-dragging");
            if (!TryGetDraggedFolder(out string path, out DefaultAsset projectFolder))
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Rejected;
                return;
            }

            DragAndDrop.AcceptDrag();
            if (projectFolder != null)
            {
                _sourcePathOverride = AssetDatabase.GetAssetPath(projectFolder);
            }
            else
            {
                _sourcePathOverride = path;
            }

            ResetScannedParts();
            Refresh();
            evt.StopPropagation();
        }

        private static bool TryGetDraggedFolder(out string path, out DefaultAsset projectFolder)
        {
            path = null;
            projectFolder = null;

            foreach (Object draggedObject in DragAndDrop.objectReferences ?? new Object[0])
            {
                string assetPath = AssetDatabase.GetAssetPath(draggedObject);
                if (string.IsNullOrWhiteSpace(assetPath) || !AssetDatabase.IsValidFolder(assetPath))
                {
                    continue;
                }

                path = assetPath;
                projectFolder = draggedObject as DefaultAsset ?? AssetDatabase.LoadAssetAtPath<DefaultAsset>(assetPath);
                return projectFolder != null;
            }

            foreach (string draggedPath in DragAndDrop.paths ?? new string[0])
            {
                if (string.IsNullOrWhiteSpace(draggedPath))
                {
                    continue;
                }

                if (AssetDatabase.IsValidFolder(draggedPath))
                {
                    path = draggedPath;
                    projectFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(draggedPath);
                    return projectFolder != null;
                }

                string fullPath = Path.IsPathRooted(draggedPath) ? draggedPath : Path.GetFullPath(draggedPath);
                if (Directory.Exists(fullPath))
                {
                    path = fullPath;
                    return true;
                }
            }

            return false;
        }

        private void OnScanParts()
        {
            string sourcePath = GetAbsoluteSourcePath();
            if (string.IsNullOrWhiteSpace(sourcePath) || !Directory.Exists(sourcePath))
            {
                return;
            }

            _scanButton.SetEnabled(false);
            try
            {
                Ksp1EditorPartScanResult result = Ksp1EditorPartScanner.Scan(sourcePath);
                _scannedParts.Clear();
                _scannedParts.AddRange(result.Parts);
                _scannedSourcePath = sourcePath;
                _lastReport = result.ReportText;
                _reportSummaryLabel.text = _scannedParts.Count == 1
                    ? "Loaded 1 part."
                    : $"Loaded {_scannedParts.Count} parts.";
                RebuildPartList();
            }
            finally
            {
                _scanButton.SetEnabled(true);
                Refresh();
            }
        }

        private void OnConvert()
        {
            string sourcePath = GetAbsoluteSourcePath();
            List<string> selectedPartNames = _scannedParts
                .Where(part => part.IsSelected)
                .Select(part => part.PartName)
                .ToList();

            _convertButton.SetEnabled(false);
            try
            {
                _lastReport = Ksp1EditorModConverter.Run(_targetMod, sourcePath, _overwriteGenerated, selectedPartNames);
                _reportSummaryLabel.text = BuildReportSummary(_lastReport);
            }
            finally
            {
                _convertButton.SetEnabled(true);
                Refresh();
            }
        }

        private void Refresh()
        {
            if (_targetModField != null && _targetModField.value != _targetMod)
            {
                _targetModField.SetValueWithoutNotify(_targetMod);
            }

            string sourcePath = GetSourcePath();
            bool hasSource = !string.IsNullOrWhiteSpace(sourcePath);
            string absoluteSourcePath = hasSource ? GetAbsoluteSourcePath() : "";
            bool sourceExists = hasSource && Directory.Exists(absoluteSourcePath);
            bool hasLoadedCurrentPartList = sourceExists && string.Equals(_scannedSourcePath, absoluteSourcePath, System.StringComparison.OrdinalIgnoreCase);
            int selectedCount = _scannedParts.Count(part => part.IsSelected);
            bool hasFilter = !string.IsNullOrWhiteSpace(_partFilter);
            bool hasVisibleParts = hasFilter && GetVisibleParts().Count > 0;
            bool canScan = sourceExists;
            bool canConvert = _targetMod != null && hasLoadedCurrentPartList && selectedCount > 0;

            if (_scanButton != null)
            {
                _scanButton.SetEnabled(canScan);
            }

            if (_convertButton != null)
            {
                _convertButton.SetEnabled(canConvert);
            }

            if (_selectAllButton != null)
            {
                _selectAllButton.SetEnabled(_scannedParts.Count > 0);
            }

            if (_clearSourceButton != null)
            {
                _clearSourceButton.SetEnabled(hasSource);
            }

            if (_clearSelectionButton != null)
            {
                _clearSelectionButton.SetEnabled(_scannedParts.Count > 0);
            }

            if (_selectVisibleButton != null)
            {
                _selectVisibleButton.SetEnabled(hasVisibleParts);
                _selectVisibleButton.EnableInClassList("is-hidden", !hasFilter);
            }

            if (_clearVisibleButton != null)
            {
                _clearVisibleButton.SetEnabled(hasVisibleParts);
                _clearVisibleButton.EnableInClassList("is-hidden", !hasFilter);
            }

            if (_partFilterField != null && _partFilterField.value != _partFilter)
            {
                _partFilterField.SetValueWithoutNotify(_partFilter);
            }

            if (_openReportButton != null)
            {
                _openReportButton.SetEnabled(!string.IsNullOrWhiteSpace(_lastReport));
            }

            if (_sourceSummaryLabel != null)
            {
                _sourceSummaryLabel.text = sourceExists
                    ? $"Selected source: {sourcePath}"
                    : "No source selected.";
                _sourceSummaryLabel.EnableInClassList("is-empty", !sourceExists);
            }

            if (_dropZoneLabel != null)
            {
                _dropZoneLabel.text = sourceExists
                    ? "Click to browse for a different folder, or drag one here."
                    : "Click to browse, or drag GameData / a KSP1 mod folder here.";
            }

            if (_validationHelp != null)
            {
                if (_targetMod == null)
                {
                    _validationHelp.text = "Select the target Redux mod asset.";
                    _validationHelp.messageType = HelpBoxMessageType.Warning;
                }
                else if (!sourceExists)
                {
                    _validationHelp.text = "Select a valid KSP1 mod folder. Project folders and external folders are both supported.";
                    _validationHelp.messageType = HelpBoxMessageType.Info;
                }
                else
                {
                    _validationHelp.text = hasLoadedCurrentPartList
                        ? "Uncheck any parts to skip, then convert the selected parts."
                        : "Load the part list, review the selected parts, then convert.";
                    _validationHelp.messageType = HelpBoxMessageType.Info;
                }
            }

            RefreshPartSelectionSummary(hasLoadedCurrentPartList);
        }

        private void OpenLastReport()
        {
            if (string.IsNullOrWhiteSpace(_lastReport))
            {
                return;
            }

            Ksp1ImportReportWindow.Open(_lastReport, _targetMod == null ? "KSP1 Import Report" : $"{_targetMod.PickerDisplayName} - KSP1 Import Report");
        }

        private string GetSourcePath()
        {
            return _sourcePathOverride;
        }

        private string GetAbsoluteSourcePath()
        {
            string sourcePath = GetSourcePath();
            return string.IsNullOrWhiteSpace(sourcePath)
                ? ""
                : Path.IsPathRooted(sourcePath)
                    ? sourcePath
                    : Path.GetFullPath(sourcePath);
        }

        private void ResetScannedParts()
        {
            _scannedParts.Clear();
            _scannedSourcePath = "";
            RebuildPartList();
        }

        private void SetAllPartSelections(bool selected)
        {
            foreach (Ksp1EditorPartSelection part in _scannedParts)
            {
                part.IsSelected = selected;
            }

            RebuildPartList();
            Refresh();
        }

        private void SetVisiblePartSelections(bool selected)
        {
            foreach (Ksp1EditorPartSelection part in GetVisibleParts())
            {
                part.IsSelected = selected;
            }

            RebuildPartList();
            Refresh();
        }

        private void RebuildPartList()
        {
            if (_partListScroll == null)
            {
                return;
            }

            _partListScroll.Clear();
            if (_scannedParts.Count == 0)
            {
                Label empty = new("No parts loaded.");
                empty.AddToClassList("ksp1-converter-part-list-empty");
                _partListScroll.Add(empty);
                RefreshPartSelectionSummary(false);
                return;
            }

            List<Ksp1EditorPartSelection> visibleParts = GetVisibleParts()
                .OrderBy(part => part.DisplayTitle)
                .ToList();
            if (visibleParts.Count == 0)
            {
                Label empty = new("No parts match the current filter.");
                empty.AddToClassList("ksp1-converter-part-list-empty");
                _partListScroll.Add(empty);
                RefreshPartSelectionSummary(true);
                return;
            }

            foreach (Ksp1EditorPartSelection part in visibleParts)
            {
                VisualElement row = new();
                row.AddToClassList("ksp1-converter-part-row");
                row.RegisterCallback<ClickEvent>(_ =>
                {
                    part.IsSelected = !part.IsSelected;
                    RebuildPartList();
                    Refresh();
                });

                Toggle toggle = new()
                {
                    value = part.IsSelected
                };
                toggle.AddToClassList("ksp1-converter-part-toggle");
                toggle.RegisterValueChangedCallback(evt =>
                {
                    part.IsSelected = evt.newValue;
                    Refresh();
                });
                toggle.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());
                row.Add(toggle);

                VisualElement text = new();
                text.AddToClassList("ksp1-converter-part-text");
                row.Add(text);

                Label title = new(part.DisplayTitle);
                title.AddToClassList("ksp1-converter-part-title");
                text.Add(title);

                string detail = string.Equals(part.DisplayTitle, part.PartName, System.StringComparison.OrdinalIgnoreCase)
                    ? part.Detail
                    : $"{part.PartName} - {part.Detail}";
                Label detailLabel = new(detail);
                detailLabel.AddToClassList("ksp1-converter-part-detail");
                text.Add(detailLabel);

                _partListScroll.Add(row);
            }

            RefreshPartSelectionSummary(true);
        }

        private List<Ksp1EditorPartSelection> GetVisibleParts()
        {
            string filter = _partFilter?.Trim();
            return _scannedParts
                .Where(part => part.MatchesFilter(filter))
                .ToList();
        }

        private void RefreshPartSelectionSummary(bool currentSourceLoaded)
        {
            if (_partSelectionSummaryLabel == null)
            {
                return;
            }

            if (_scannedParts.Count == 0)
            {
                _partSelectionSummaryLabel.text = "Load a part list to choose what gets converted.";
                return;
            }

            int selectedCount = _scannedParts.Count(part => part.IsSelected);
            int visibleCount = GetVisibleParts().Count;
            string filterSummary = string.IsNullOrWhiteSpace(_partFilter)
                ? ""
                : $" {visibleCount} visible.";
            string stale = currentSourceLoaded ? "" : " Source changed; reload before converting.";
            _partSelectionSummaryLabel.text = $"{selectedCount} of {_scannedParts.Count} selected.{filterSummary}{stale}";
        }

        private static string BuildReportSummary(string report)
        {
            if (string.IsNullOrWhiteSpace(report))
            {
                return "No report.";
            }

            int parts = -1;
            int resources = -1;
            int warnings = -1;
            int errors = 0;
            using StringReader reader = new(report);
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                if (TryReadReportInt(line, "Parts:", out int parsedParts))
                {
                    parts = parsedParts;
                }
                else if (TryReadReportInt(line, "Resources:", out int parsedResources))
                {
                    resources = parsedResources;
                }
                else if (TryReadReportInt(line, "Warnings:", out int parsedWarnings))
                {
                    warnings = parsedWarnings;
                }
                else if (line.StartsWith("Error:", System.StringComparison.Ordinal))
                {
                    errors++;
                }
            }

            if (parts < 0 && resources < 0 && warnings < 0)
            {
                return "Conversion report generated.";
            }

            return $"{FormatCount(parts, "part")} converted, {FormatCount(resources, "resource")} imported, {FormatCount(warnings, "warning")}, {FormatCount(errors, "error")}.";
        }

        private static bool TryReadReportInt(string line, string prefix, out int value)
        {
            value = 0;
            if (line == null || !line.StartsWith(prefix, System.StringComparison.Ordinal))
            {
                return false;
            }

            return int.TryParse(line.Substring(prefix.Length).Trim(), out value);
        }

        private static string FormatCount(int value, string noun)
        {
            if (value < 0)
            {
                value = 0;
            }

            return value == 1 ? $"1 {noun}" : $"{value} {noun}s";
        }
    }
}
