using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Ksp2UnityTools.Editor.Localization.CsvIO;
using Ksp2UnityTools.Editor.Localization.Export;
using Ksp2UnityTools.Editor.Localization.Widgets;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.Localization.Windows
{
    /// <summary>
    /// Modal-style editor window driven by <see cref="LocExportFlow" />. Lets the author pick a
    /// target CSV, choose merge mode, preview the diff, and commit the merge via
    /// <see cref="CsvMergeWriter" />.
    /// </summary>
    public sealed class LocExportModal : EditorWindow
    {
        private const string UxmlPath = "/Assets/Windows/Localization/Windows/LocExportModal.uxml";
        private const string UssPath = "/Assets/Windows/Localization/Windows/LocExportModal.uss";
        private const int PreviewLimit = 50;

        private List<LocalizationKeyEntry> _entries;
        private string _defaultTargetPath;
        private MergeMode _mode = MergeMode.AppendOnly;

        private TextField _pathField;
        private Label _previewLabel;
        private ScrollView _previewScroll;
        private RadioButton _appendRadio;
        private RadioButton _refreshRadio;

        public static void Open(List<LocalizationKeyEntry> entries, string defaultTargetPath)
        {
            var window = CreateInstance<LocExportModal>();
            window.titleContent = new GUIContent("Export Localizations");
            window.minSize = new Vector2(720, 380);
            window._entries = entries ?? new List<LocalizationKeyEntry>();
            window._defaultTargetPath = defaultTargetPath ?? string.Empty;
            window.ShowUtility();
        }

        private void CreateGUI()
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(SDKConfiguration.BasePath + UxmlPath);
            if (tree == null)
            {
                rootVisualElement.Add(new Label("Failed to load LocExportModal.uxml"));
                return;
            }
            tree.CloneTree(rootVisualElement);
            Ksp2UnityToolsStyles.Apply(rootVisualElement, UssPath);
            rootVisualElement.style.flexGrow = 1f;

            _pathField = rootVisualElement.Q<TextField>("targetPath");
            _previewLabel = rootVisualElement.Q<Label>("previewLabel");
            _previewScroll = rootVisualElement.Q<ScrollView>("previewScroll");
            _appendRadio = rootVisualElement.Q<RadioButton>("mode-append");
            _refreshRadio = rootVisualElement.Q<RadioButton>("mode-refresh");

            _pathField.value = _defaultTargetPath;
            _appendRadio.value = true;
            _refreshRadio.value = false;
            _previewScroll.style.display = DisplayStyle.None;

            _appendRadio.RegisterValueChangedCallback(evt => { if (evt.newValue) _mode = MergeMode.AppendOnly; });
            _refreshRadio.RegisterValueChangedCallback(evt => { if (evt.newValue) _mode = MergeMode.RefreshDescriptions; });

            rootVisualElement.Q<Button>("browse").clicked += OnBrowse;
            rootVisualElement.Q<Button>("preview").clicked += OnPreview;
            rootVisualElement.Q<Button>("cancel").clicked += Close;
            rootVisualElement.Q<Button>("export").clicked += OnExport;
        }

        private void OnBrowse()
        {
            var current = _pathField.value ?? string.Empty;
            var startDir = string.IsNullOrEmpty(current) ? "Assets/ReduxAssets/Localizations" : Path.GetDirectoryName(current);
            var startName = string.IsNullOrEmpty(current) ? "loc.csv" : Path.GetFileName(current);
            var picked = EditorUtility.SaveFilePanel("Choose target CSV", startDir, startName, "csv");
            if (string.IsNullOrEmpty(picked)) return;
            _pathField.value = ToProjectRelativeIfPossible(picked);
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

        private void OnPreview()
        {
            _previewLabel.text = BuildPreviewText();
            _previewScroll.style.display = DisplayStyle.Flex;
        }

        private string BuildPreviewText()
        {
            var path = _pathField.value;
            if (string.IsNullOrEmpty(path)) return "(no target path)";
            if (_entries == null || _entries.Count == 0) return "(no entries to export)";

            Dictionary<string, LocRow> existing = null;
            if (File.Exists(path))
            {
                try
                {
                    var text = File.ReadAllText(path);
                    var parsed = LocCsvReader.Parse(text, path);
                    existing = new Dictionary<string, LocRow>(StringComparer.Ordinal);
                    foreach (var row in parsed.Rows)
                    {
                        var k = row.Get("Key");
                        if (!string.IsNullOrEmpty(k)) existing[k] = row;
                    }
                }
                catch (Exception e)
                {
                    return $"(failed to parse existing target: {e.Message})";
                }
            }

            var newKeys = new List<string>();
            var descUpdates = new List<string>();
            int unchanged = 0;

            foreach (var entry in _entries)
            {
                if (string.IsNullOrEmpty(entry.Key)) continue;
                if (existing != null && existing.TryGetValue(entry.Key, out var row))
                {
                    if (_mode == MergeMode.RefreshDescriptions && row.Get("Desc") != entry.Description)
                    {
                        descUpdates.Add(entry.Key);
                    }
                    else
                    {
                        unchanged++;
                    }
                }
                else
                {
                    newKeys.Add(entry.Key);
                }
            }

            var sb = new StringBuilder();
            sb.AppendLine("Will write: " + path);
            sb.AppendLine();
            sb.AppendLine($"New rows ({newKeys.Count}):");
            AppendCapped(sb, newKeys, "+ ");
            sb.AppendLine();
            sb.AppendLine($"Description updates ({descUpdates.Count}):");
            if (_mode == MergeMode.AppendOnly && descUpdates.Count == 0)
            {
                sb.AppendLine("  (none - append-only mode)");
            }
            else
            {
                AppendCapped(sb, descUpdates, "~ ");
            }
            sb.AppendLine();
            sb.AppendLine("Unchanged: " + unchanged);
            return sb.ToString();
        }

        private static void AppendCapped(StringBuilder sb, List<string> items, string prefix)
        {
            int cap = items.Count < PreviewLimit ? items.Count : PreviewLimit;
            for (int i = 0; i < cap; i++)
            {
                sb.Append("  ").Append(prefix).AppendLine(items[i]);
            }
            if (items.Count > cap)
            {
                sb.Append("  +").Append(items.Count - cap).AppendLine(" more");
            }
        }

        private void OnExport()
        {
            var path = _pathField.value;
            if (string.IsNullOrEmpty(path))
            {
                _previewLabel.text = "Pick a target CSV first.";
                _previewScroll.style.display = DisplayStyle.Flex;
                return;
            }
            MergeResult result;
            try
            {
                result = CsvMergeWriter.Merge(path, _entries, _mode);
            }
            catch (Exception e)
            {
                _previewLabel.text = "Export failed: " + e.Message;
                _previewScroll.style.display = DisplayStyle.Flex;
                return;
            }
            Debug.Log($"[LocExportModal] Exported to {path}. {result.NewKeys.Count} new key(s), {result.RefreshedDescs.Count} desc update(s), {result.Unchanged} unchanged.");
            Close();
        }
    }
}
