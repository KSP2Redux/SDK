using System;
using System.Collections.Generic;
using System.IO;
using Ksp2UnityTools.Editor.Localization.CsvIO;
using Ksp2UnityTools.Editor.Localization.Widgets;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.Localization.Windows
{
    /// <summary>
    /// Editor window that opens an I2-format localization CSV, mounts it in a <see cref="LocTableView" />,
    /// and writes edits back to disk via <see cref="LocCsvWriter" />. Hijacks double-click on matching
    /// CSV TextAssets through <see cref="UnityEditor.Callbacks.OnOpenAssetAttribute" />.
    /// </summary>
    public sealed class LocCsvEditorWindow : EditorWindow
    {
        private const string UxmlPath = "/Assets/Windows/Localization/Windows/LocCsvEditorWindow.uxml";
        private const string UssPath = "/Assets/Windows/Localization/Windows/LocCsvEditorWindow.uss";
        private const string DefaultBrowsePath = "Assets/ReduxAssets/Localizations";
        private const double DiskPollIntervalSeconds = 1.0;
        private const string I2HeaderPrefix = "Key,Type,Desc,";

        [SerializeField] private string _filePath;

        private List<LocColumnSpec> _columns;
        private List<LocRow> _rows;
        private string _lineEnding = "\n";
        private bool _hasTrailingNewline = true;
        private bool _dirty;
        private DateTime _lastDiskModTime;
        private double _lastPollTime;
        private bool _ignoreNextDiskBump;

        private VisualElement _rootHost;
        private Label _filePathLabel;
        private Button _columnsBtn;
        private Button _saveBtn;
        private Button _validateBtn;
        private VisualElement _bannerHost;
        private VisualElement _tableHost;
        private Label _statusLabel;
        private LocTableView _table;

        #region Open hooks

        [OnOpenAsset]
        public static bool OnOpenAsset(int instanceID, int line)
        {
            var path = AssetDatabase.GetAssetPath(instanceID);
            if (string.IsNullOrEmpty(path)) return false;
            if (!path.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)) return false;
            string head;
            try
            {
                head = ReadFirstLine(path);
            }
            catch
            {
                return false;
            }
            if (head == null || !head.StartsWith(I2HeaderPrefix, StringComparison.Ordinal)) return false;
            Open(path);
            return true;
        }

        [MenuItem("Modding/Localization/Open CSV Editor...")]
        public static void OpenFromMenu()
        {
            var picked = EditorUtility.OpenFilePanel("Open localization CSV", DefaultBrowsePath, "csv");
            if (string.IsNullOrEmpty(picked)) return;
            var path = ToProjectRelativeIfPossible(picked);
            Open(path);
        }

        public static void Open(string path)
        {
            var window = GetWindow<LocCsvEditorWindow>();
            window.minSize = new Vector2(700, 320);
            window._filePath = path;
            window.LoadFile();
            window.Show();
            window.Focus();
        }

        private static string ToProjectRelativeIfPossible(string absolutePath)
        {
            var dataPath = Application.dataPath.Replace('\\', '/');
            var norm = absolutePath.Replace('\\', '/');
            if (norm.StartsWith(dataPath, StringComparison.Ordinal))
            {
                return "Assets" + norm.Substring(dataPath.Length);
            }
            return absolutePath;
        }

        private static string ReadFirstLine(string path)
        {
            using var sr = new StreamReader(path);
            return sr.ReadLine();
        }

        #endregion

        #region Lifecycle

        private void CreateGUI()
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(SDKConfiguration.BasePath + UxmlPath);
            if (tree == null)
            {
                rootVisualElement.Add(new Label("Failed to load LocCsvEditorWindow.uxml"));
                return;
            }
            tree.CloneTree(rootVisualElement);
            Ksp2UnityToolsStyles.Apply(rootVisualElement, UssPath);
            rootVisualElement.style.flexGrow = 1f;

            _rootHost = rootVisualElement;
            _filePathLabel = _rootHost.Q<Label>("filePath");
            _columnsBtn = _rootHost.Q<Button>("columns");
            _saveBtn = _rootHost.Q<Button>("save");
            _validateBtn = _rootHost.Q<Button>("validate");
            _bannerHost = _rootHost.Q<VisualElement>("banner");
            _tableHost = _rootHost.Q<VisualElement>("tableHost");
            _statusLabel = _rootHost.Q<Label>("status");

            _columnsBtn.clicked += OnColumnsClicked;
            _saveBtn.clicked += OnSaveClicked;
            _validateBtn.clicked += OnValidateClicked;
            _saveBtn.SetEnabled(false);
            _columnsBtn.SetEnabled(false);

            if (!string.IsNullOrEmpty(_filePath))
            {
                LoadFile();
            }
            else
            {
                _statusLabel.text = "No file loaded.";
            }
        }

        private void OnDestroy()
        {
            if (!_dirty || string.IsNullOrEmpty(_filePath)) return;
            bool save = EditorUtility.DisplayDialog(
                "Unsaved changes",
                $"You have unsaved changes in {Path.GetFileName(_filePath)}. Save now?",
                "Save",
                "Discard");
            if (save) WriteToDisk();
        }

        private void Update()
        {
            if (string.IsNullOrEmpty(_filePath)) return;
            if (EditorApplication.timeSinceStartup - _lastPollTime < DiskPollIntervalSeconds) return;
            _lastPollTime = EditorApplication.timeSinceStartup;
            if (!File.Exists(_filePath)) return;
            var mod = File.GetLastWriteTime(_filePath);
            if (mod <= _lastDiskModTime) return;
            if (_ignoreNextDiskBump)
            {
                _ignoreNextDiskBump = false;
                _lastDiskModTime = mod;
                return;
            }
            _lastDiskModTime = mod;
            ShowDiskChangedBanner();
        }

        #endregion

        #region File I/O

        private void LoadFile()
        {
            if (_rootHost == null) return;
            if (string.IsNullOrEmpty(_filePath) || !File.Exists(_filePath))
            {
                _statusLabel.text = $"File not found: {_filePath}";
                return;
            }

            string text;
            try
            {
                text = File.ReadAllText(_filePath);
            }
            catch (Exception e)
            {
                _statusLabel.text = $"Read error: {e.Message}";
                return;
            }

            var parsed = LocCsvReader.Parse(text, _filePath);
            _columns = parsed.Columns;
            _rows = parsed.Rows;
            _lineEnding = parsed.LineEnding;
            _hasTrailingNewline = parsed.HasTrailingNewline;
            _lastDiskModTime = File.GetLastWriteTime(_filePath);
            _dirty = false;

            _filePathLabel.text = _filePath.Replace('\\', '/');
            _bannerHost.Clear();

            MountTable();
            UpdateTitle();
            UpdateStatus();
            _saveBtn.SetEnabled(false);
            _columnsBtn.SetEnabled(true);
        }

        private void OnColumnsClicked()
        {
            _table?.OpenColumnsMenu(_columnsBtn);
        }

        private void MountTable()
        {
            _tableHost.Clear();
            _table = new LocTableView(_columns, _rows, _filePath);
            _table.style.flexGrow = 1f;
            _table.RowEdited += OnRowEdited;
            _tableHost.Add(_table);
        }

        private void OnRowEdited(LocRow _)
        {
            if (_dirty) return;
            _dirty = true;
            UpdateTitle();
            UpdateStatus();
            _saveBtn.SetEnabled(true);
        }

        private void OnSaveClicked()
        {
            WriteToDisk();
        }

        private void WriteToDisk()
        {
            if (string.IsNullOrEmpty(_filePath) || _columns == null || _rows == null) return;
            try
            {
                LocCsvWriter.Write(_filePath, _columns, _rows, _lineEnding, _hasTrailingNewline);
            }
            catch (Exception e)
            {
                _statusLabel.text = $"Write error: {e.Message}";
                return;
            }
            _ignoreNextDiskBump = true;
            _lastDiskModTime = File.GetLastWriteTime(_filePath);
            if (_filePath.StartsWith("Assets/", StringComparison.Ordinal))
            {
                AssetDatabase.ImportAsset(_filePath);
            }
            _dirty = false;
            UpdateTitle();
            UpdateStatus($"Saved at {DateTime.Now:HH:mm:ss}.");
            _saveBtn.SetEnabled(false);
        }

        #endregion

        #region Validation

        private void OnValidateClicked()
        {
            var issues = ValidateAll();
            if (issues.Count == 0)
            {
                UpdateStatus("Validation passed.");
                return;
            }
            int preview = issues.Count > 3 ? 3 : issues.Count;
            string head = string.Join(" | ", issues.GetRange(0, preview));
            string suffix = issues.Count > preview ? $" (+{issues.Count - preview} more)" : string.Empty;
            UpdateStatus($"Validation: {issues.Count} issue(s). {head}{suffix}");
        }

        private List<string> ValidateAll()
        {
            var issues = new List<string>();
            if (_rows == null || _columns == null) return issues;

            var headerCount = _columns.Count;
            var seenKeys = new HashSet<string>();
            for (int i = 0; i < _rows.Count; i++)
            {
                var row = _rows[i];
                var key = row.Get("Key");
                if (string.IsNullOrEmpty(key))
                {
                    issues.Add($"Row {i + 1}: empty key");
                    continue;
                }
                if (key.IndexOf(',') >= 0)
                {
                    issues.Add($"Row {i + 1}: key contains comma");
                }
                if (!seenKeys.Add(key))
                {
                    issues.Add($"Row {i + 1}: duplicate key '{key}'");
                }
                if (row.Get("$Status") == "Localized" && string.IsNullOrEmpty(row.Get("English")))
                {
                    issues.Add($"Row {i + 1}: Status=Localized but English empty");
                }
            }
            return issues;
        }

        #endregion

        #region Banner

        private void ShowDiskChangedBanner()
        {
            _bannerHost.Clear();
            var banner = new VisualElement();
            banner.AddToClassList("loc-editor-banner");
            var label = new Label("File modified on disk. Reload to pick up the latest version. Unsaved edits would be lost.");
            label.AddToClassList("loc-editor-banner-text");
            banner.Add(label);
            var reloadBtn = new Button(() => { LoadFile(); _bannerHost.Clear(); }) { text = "Reload" };
            reloadBtn.AddToClassList("loc-editor-banner-btn");
            banner.Add(reloadBtn);
            var dismissBtn = new Button(() => _bannerHost.Clear()) { text = "Dismiss" };
            dismissBtn.AddToClassList("loc-editor-banner-btn");
            banner.Add(dismissBtn);
            _bannerHost.Add(banner);
        }

        #endregion

        #region UI updates

        private void UpdateTitle()
        {
            var fileName = string.IsNullOrEmpty(_filePath) ? null : Path.GetFileName(_filePath);
            var prefix = _dirty ? "* " : string.Empty;
            titleContent = new GUIContent(prefix + (string.IsNullOrEmpty(fileName) ? "Loc Editor" : fileName));
        }

        private void UpdateStatus(string overrideText = null)
        {
            if (overrideText != null)
            {
                _statusLabel.text = overrideText;
                return;
            }
            if (_rows == null || _columns == null)
            {
                _statusLabel.text = string.Empty;
                return;
            }
            _statusLabel.text = $"{_rows.Count} rows | {_columns.Count} columns" + (_dirty ? " | edited" : string.Empty);
        }

        #endregion
    }
}
