using System.Collections.Generic;
using KSP.Game.Missions.Definitions;
using Ksp2UnityTools.Editor.Localization.Export;
using Ksp2UnityTools.Editor.MissionAuthoring.StageStrip;
using Ksp2UnityTools.Editor.MissionAuthoring.Validation;
using Ksp2UnityTools.Editor.Validation;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.MissionAuthoring.Windows
{
    /// <summary>
    /// Editor window for authoring a single <see cref="Mission" />. Hosts the stage strip
    /// view (horizontal array of stage cards) and a header bar with title plus validation
    /// readiness chip. Opens automatically on double-click of a Mission asset, or via the
    /// Mission Editor menu when a Mission is selected.
    /// </summary>
    public class MissionEditorWindow : EditorWindow
    {
        // Survives domain reload via Unity's serialization.
        [SerializeField] private Mission _boundMission;

        private StageStripView _stripView;
        private Label _missionTitleLabel;
        private Button _validationChip;
        private Button _bakeJsonChip;
        private Button _exportLocalizationsChip;

        [OnOpenAsset]
        private static bool OnOpenMissionAsset(EntityId assetId, int line)
        {
            string path = AssetDatabase.GetAssetPath(assetId);
            if (string.IsNullOrEmpty(path)) return false;
            Mission mission = AssetDatabase.LoadAssetAtPath<Mission>(path);
            if (mission == null) return false;
            OpenFor(mission);
            return true;
        }

        [MenuItem("Modding/Mission Authoring/Mission Editor")]
        private static void OpenFromMenu()
        {
            var mission = Selection.activeObject as Mission;
            if (mission == null)
            {
                EditorUtility.DisplayDialog(
                    "Mission Editor",
                    "Select a Mission asset in the Project window first.",
                    "OK");
                return;
            }
            OpenFor(mission);
        }

        /// <summary>
        /// Focuses an existing editor window bound to <paramref name="mission" />, or opens a new one.
        /// </summary>
        /// <param name="mission">The mission to bind the window to.</param>
        public static void OpenFor(Mission mission)
        {
            var existing = Resources.FindObjectsOfTypeAll<MissionEditorWindow>();
            foreach (var window in existing)
            {
                if (window._boundMission == mission)
                {
                    window.Focus();
                    return;
                }
            }
            var newWindow = CreateInstance<MissionEditorWindow>();
            newWindow._boundMission = mission;
            newWindow.Show();
        }

        private const string UxmlPath = "/Assets/Windows/MissionAuthoring/MissionEditorWindow.uxml";
        private const string UssPath = "/Assets/Windows/MissionAuthoring/MissionEditorWindow.uss";
        private const string DataEditorsUssPath = "/Assets/Windows/DataEditors.uss";

        private void CreateGUI()
        {
            titleContent = new GUIContent("Mission Editor");

            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                SDKConfiguration.BasePath + UxmlPath);
            if (visualTree == null)
            {
                rootVisualElement.Add(new Label("Failed to load MissionEditorWindow.uxml"));
                return;
            }

            visualTree.CloneTree(rootVisualElement);
            Ksp2UnityToolsStyles.Apply(rootVisualElement, UssPath);
            Ksp2UnityToolsStyles.Apply(rootVisualElement, DataEditorsUssPath);

            _missionTitleLabel = rootVisualElement.Q<Label>("missionTitle");
            _validationChip = rootVisualElement.Q<Button>("readiness-chip-valid");
            _bakeJsonChip = rootVisualElement.Q<Button>("bake-json-chip");
            _exportLocalizationsChip = rootVisualElement.Q<Button>("export-localizations-chip");
            var stripHost = rootVisualElement.Q<VisualElement>("stripHost");

            _stripView = new StageStripView();
            _stripView.ModelChanged += OnModelChanged;
            stripHost.Add(_stripView);

            if (_validationChip != null)
            {
                _validationChip.clicked += OnValidationChipClicked;
                _validationChip.tooltip = "Open the Mission Validation Report.";
            }
            if (_bakeJsonChip != null)
            {
                _bakeJsonChip.clicked += OnBakeJsonChipClicked;
                _bakeJsonChip.tooltip = "Bake the current mission to JSON and register it as an addressable using the mission's ID.";
            }
            if (_exportLocalizationsChip != null)
            {
                _exportLocalizationsChip.clicked += OnExportLocalizationsChipClicked;
                _exportLocalizationsChip.tooltip = "Export this mission's loc keys (incl. per-stage keys) to a CSV.";
            }
            MissionValidationExpensiveCache.Changed += OnExpensiveCacheChanged;

            if (_boundMission != null) BindMission(_boundMission);
            else UpdateValidationChip();
        }

        private void OnExportLocalizationsChipClicked()
        {
            if (_boundMission == null) return;
            LocExportFlow.RunForAsset(_boundMission);
        }

        private void OnDisable()
        {
            MissionValidationExpensiveCache.Changed -= OnExpensiveCacheChanged;
            if (_stripView != null) _stripView.ModelChanged -= OnModelChanged;
            if (ActiveMissionTracker.Current == _boundMission) ActiveMissionTracker.Current = null;
        }

        private void OnFocus()
        {
            if (_boundMission != null) ActiveMissionTracker.Current = _boundMission;
        }

        private void BindMission(Mission mission)
        {
            _boundMission = mission;
            _missionTitleLabel.text = $"Mission: {mission.missionData?.ID ?? "(unnamed)"}";
            _stripView.Bind(mission);
            ActiveMissionTracker.Current = mission;
            UpdateValidationChip();
        }

        private void OnModelChanged()
        {
            if (_missionTitleLabel != null && _boundMission != null)
            {
                _missionTitleLabel.text = $"Mission: {_boundMission.missionData?.ID ?? "(unnamed)"}";
            }
            UpdateValidationChip();
        }

        private void OnExpensiveCacheChanged(Mission mission)
        {
            if (_boundMission == null) return;
            if (mission != null && mission != _boundMission) return;
            UpdateValidationChip();
        }

        private void OnValidationChipClicked()
        {
            MissionValidationReportWindow.Open(_boundMission);
        }

        private void OnBakeJsonChipClicked()
        {
            if (_boundMission == null) return;
            MissionAuthoringActions.BakeToJson(_boundMission);
        }

        private void UpdateValidationChip()
        {
            if (_validationChip == null) return;
            if (_boundMission == null)
            {
                _validationChip.text = "Validation: n/a";
                SetReadinessState(_validationChip, null);
                return;
            }

            var cheap = MissionValidationReport.Run(new MissionValidationContext(_boundMission), ValidatorCost.Cheap);
            IReadOnlyList<ValidationIssue> expensive = MissionValidationExpensiveCache.Get(_boundMission);
            int errors = cheap.ErrorCount;
            int warnings = cheap.WarningCount;
            int info = cheap.InfoCount;
            foreach (var issue in expensive)
            {
                switch (issue.Severity)
                {
                    case ValidationSeverity.Error: errors++; break;
                    case ValidationSeverity.Warning: warnings++; break;
                    case ValidationSeverity.Info: info++; break;
                }
            }

            if (errors + warnings + info == 0)
            {
                _validationChip.text = "No issues";
                SetReadinessState(_validationChip, "is-ok");
            }
            else
            {
                _validationChip.text = $"✕ {errors}  ·  ⚠ {warnings}  ·  ⓘ {info}";
                SetReadinessState(_validationChip, errors > 0 ? "is-error" : "is-warn");
            }
        }

        private static void SetReadinessState(VisualElement chip, string stateClass)
        {
            chip.EnableInClassList("is-ok", stateClass == "is-ok");
            chip.EnableInClassList("is-warn", stateClass == "is-warn");
            chip.EnableInClassList("is-error", stateClass == "is-error");
        }
    }
}
