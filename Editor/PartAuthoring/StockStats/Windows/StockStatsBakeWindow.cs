#if REDUX
using System;
using System.IO;
using System.Text;
using Ksp2UnityTools.Editor.PartAuthoring.Windows;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PartAuthoring.StockStats.Windows
{
    /// <summary>Editor window that drives the <see cref="StockStatsBaker" /> bake against a user-picked source folder.</summary>
    /// <remarks>
    /// Persists the picked source folder in <see cref="EditorPrefs" /> so it survives between
    /// sessions. The output asset path sits in the package's <c>Assets/</c> subfolder alongside
    /// the rest of the package data. The asset's type lives in the editor assembly so it cannot
    /// be loaded at runtime regardless of where the file lives on disk. The window is
    /// REDUX-only and not registered in SDK builds.
    /// </remarks>
    public sealed class StockStatsBakeWindow : EditorWindow
    {
        private const string UXML_PATH = "/Assets/Windows/PartAuthoring/Windows/StockStatsBakeWindow.uxml";
        private const string USS_PATH = "/Assets/Windows/PartAuthoring/Windows/StockStatsBakeWindow.uss";

        private const string SOURCE_DIR_PREF_KEY = "Ksp2UnityTools.StockStats.SourceDir";

        private const string OUTPUT_ASSET_PATH = SDKConfiguration.BasePath + "/Assets/StockStats/StockStatsLookup.asset";

        private Label _sourcePathLabel;
        private Button _pickSourceButton;
        private Label _sourceSummaryLabel;
        private VisualElement _sourceHelpSlot;
        private Label _statusLabel;
        private Label _lastBakedLabel;
        private Label _hashMatchLabel;
        private Label _outputPathLabel;
        private Button _rebakeButton;
        private Foldout _lastBakeSummary;
        private Label _summaryLabel;
        private Foldout _verboseLogFoldout;
        private Label _verboseLogLabel;

        private string _sourceDir;
        private StockStatsBaker.BakeResult _lastResult;
        private HelpBox _sourceHelpBox;

        [MenuItem(PartAuthoringWindows.MENU_ROOT + "Stock Stats Bake", priority = PartAuthoringWindows.PRIORITY_STOCK_STATS_BAKE)]
        public static void ShowWindow()
        {
            var window = GetWindow<StockStatsBakeWindow>();
            window.titleContent = new GUIContent("Stock Stats Bake");
            window.minSize = new Vector2(460f, 360f);
        }

        private void CreateGUI()
        {
            var root = rootVisualElement;
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(SDKConfiguration.BasePath + UXML_PATH);
            if (tree == null)
            {
                root.Add(new Label($"Failed to load {UXML_PATH}"));
                return;
            }
            tree.CloneTree(root);
            Ksp2UnityToolsStyles.Apply(root, USS_PATH);

            _sourcePathLabel = root.Q<Label>("source-path-label");
            _pickSourceButton = root.Q<Button>("pick-source-button");
            _sourceSummaryLabel = root.Q<Label>("source-summary-label");
            _sourceHelpSlot = root.Q<VisualElement>("source-help-slot");
            _statusLabel = root.Q<Label>("status-label");
            _lastBakedLabel = root.Q<Label>("last-baked-label");
            _hashMatchLabel = root.Q<Label>("hash-match-label");
            _outputPathLabel = root.Q<Label>("output-path-label");
            _rebakeButton = root.Q<Button>("rebake-button");
            _lastBakeSummary = root.Q<Foldout>("last-bake-summary");
            _summaryLabel = root.Q<Label>("summary-label");
            _verboseLogFoldout = root.Q<Foldout>("verbose-log-foldout");
            _verboseLogLabel = root.Q<Label>("verbose-log-label");

            _outputPathLabel.text = OUTPUT_ASSET_PATH;
            _pickSourceButton.clicked += OnPickSource;
            _rebakeButton.clicked += OnRebake;

            _sourceDir = EditorPrefs.GetString(SOURCE_DIR_PREF_KEY, SuggestDefaultSourceDir());
            Refresh();
        }

        private void OnPickSource()
        {
            string startPath = !string.IsNullOrEmpty(_sourceDir) && Directory.Exists(_sourceDir)
                ? _sourceDir
                : SuggestDefaultSourceDir();
            string pick = EditorUtility.OpenFolderPanel("Pick ksp2-assets/Assets folder", startPath, "");
            if (string.IsNullOrEmpty(pick))
            {
                return;
            }
            _sourceDir = pick;
            EditorPrefs.SetString(SOURCE_DIR_PREF_KEY, pick);
            Refresh();
        }

        private void OnRebake()
        {
            try
            {
                EditorUtility.DisplayProgressBar("Stock Stats Bake", "Scanning source folder...", 0.3f);
                _lastResult = StockStatsBaker.Bake(_sourceDir, OUTPUT_ASSET_PATH, new StockStatsBaker.BakeOptions { Verbose = true });
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                EditorUtility.DisplayDialog("Stock Stats Bake", "Bake failed: " + ex.Message, "OK");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                Refresh();
            }
        }

        private void Refresh()
        {
            _sourcePathLabel.text = string.IsNullOrEmpty(_sourceDir) ? "(not set)" : _sourceDir;

            ClearHelpBox();
            bool exists = !string.IsNullOrEmpty(_sourceDir) && Directory.Exists(_sourceDir);
            if (!exists)
            {
                _sourceSummaryLabel.text = string.Empty;
                _statusLabel.text = "Source folder not found.";
                _lastBakedLabel.text = string.Empty;
                _hashMatchLabel.text = string.Empty;
                SetHashClass(null);
                _rebakeButton.SetEnabled(false);
                ShowHelpBox("Pick the ksp2-assets/Assets folder containing the base-game .bytes files.", HelpBoxMessageType.Info);
                RefreshLastBakeSection();
                return;
            }

            int bytesCount = Directory.GetFiles(_sourceDir, "*.bytes", SearchOption.TopDirectoryOnly).Length;
            _sourceSummaryLabel.text = bytesCount == 0
                ? "0 .bytes files found. Wrong folder?"
                : $"{bytesCount} .bytes files found";

            string currentHash = StockStatsBaker.ComputeSourceHash(_sourceDir);
            _statusLabel.text = $"Source hash: {Shorten(currentHash, 12)}";

            StockStatsLookup existing = AssetDatabase.LoadAssetAtPath<StockStatsLookup>(OUTPUT_ASSET_PATH);
            if (existing == null)
            {
                _lastBakedLabel.text = "No lookup asset yet.";
                _hashMatchLabel.text = "Rebake to generate.";
                SetHashClass(null);
            }
            else
            {
                _lastBakedLabel.text = $"Last baked: {existing.BakedAt ?? "unknown"}";
                if (!string.IsNullOrEmpty(existing.SourceHash) && existing.SourceHash == currentHash)
                {
                    _hashMatchLabel.text = "Up to date.";
                    SetHashClass("fresh");
                }
                else
                {
                    _hashMatchLabel.text = "Source changed since last bake.";
                    SetHashClass("stale");
                }
            }
            _rebakeButton.SetEnabled(bytesCount > 0);
            RefreshLastBakeSection();
        }

        private void RefreshLastBakeSection()
        {
            if (_lastResult == null)
            {
                _lastBakeSummary.value = false;
                _summaryLabel.text = string.Empty;
                _verboseLogLabel.text = string.Empty;
                return;
            }
            _lastBakeSummary.value = true;
            _summaryLabel.text = BuildSummaryText(_lastResult);
            _verboseLogLabel.text = _lastResult.VerboseLog == null || _lastResult.VerboseLog.Count == 0
                ? "(no verbose entries this run)"
                : string.Join("\n", _lastResult.VerboseLog);
        }

        private void SetHashClass(string state)
        {
            if (_hashMatchLabel == null)
            {
                return;
            }
            _hashMatchLabel.RemoveFromClassList("stale");
            _hashMatchLabel.RemoveFromClassList("fresh");
            if (!string.IsNullOrEmpty(state))
            {
                _hashMatchLabel.AddToClassList(state);
            }
        }

        private void ShowHelpBox(string text, HelpBoxMessageType type)
        {
            if (_sourceHelpSlot == null)
            {
                return;
            }
            _sourceHelpBox = new HelpBox(text, type);
            _sourceHelpSlot.Add(_sourceHelpBox);
        }

        private void ClearHelpBox()
        {
            if (_sourceHelpBox != null)
            {
                _sourceHelpBox.RemoveFromHierarchy();
                _sourceHelpBox = null;
            }
        }

        private static string BuildSummaryText(StockStatsBaker.BakeResult r)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Parts scanned:      {r.PartsScanned}");
            sb.AppendLine($"Buckets created:    {r.BucketCount}");
            sb.AppendLine($"Raw resources:      {r.RawResourcesResolved}");
            sb.AppendLine($"Recipes resolved:   {r.RecipesResolved}");
            if (r.UnresolvedRecipes > 0)
            {
                sb.AppendLine($"Recipes unresolved: {r.UnresolvedRecipes}");
            }
            if (r.FailedFiles > 0)
            {
                sb.AppendLine($"Failed files:       {r.FailedFiles}");
            }
            sb.Append($"Source hash:        {Shorten(r.SourceHash, 16)}");
            return sb.ToString();
        }

        private static string Shorten(string s, int max)
        {
            if (string.IsNullOrEmpty(s))
            {
                return string.Empty;
            }
            return s.Length <= max ? s : s.Substring(0, max) + "...";
        }

        private static string SuggestDefaultSourceDir()
        {
            try
            {
                string workspaceRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
                string candidate = Path.Combine(workspaceRoot, "ksp2-assets", "Assets");
                return Directory.Exists(candidate) ? candidate : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
#endif
