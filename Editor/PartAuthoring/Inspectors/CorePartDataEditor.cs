using System.Collections.Generic;
using System.IO;
using KSP;
using KSP.Sim.Definitions;
using Ksp2UnityTools.Editor.IO;
using Ksp2UnityTools.Editor.PartAuthoring.Gizmos;
using Ksp2UnityTools.Editor.PartAuthoring.Inspectors.Sections;
using Ksp2UnityTools.Editor.PartAuthoring.Inspectors.Tabs;
using Ksp2UnityTools.Editor.PartAuthoring.Tools;
using Redux.VFX.ReentryMeshGeneration;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PartAuthoring.Inspectors
{
    /// <summary>
    /// UI Toolkit custom editor for <see cref="CorePartData" /> that hosts the part-authoring tabs.
    /// </summary>
    /// <remarks>
    /// Header chrome above the tab bar surfaces identity (part name, family, size category), the
    /// Quick Tools chip row, and a Gizmo Settings foldout. Tabs are Core, Modules, Variants,
    /// Interacts.
    /// </remarks>
    [CustomEditor(typeof(CorePartData))]
    public sealed class CorePartDataEditor : UnityEditor.Editor
    {
        private const string UXML_PATH = "/Assets/Windows/PartAuthoring/Inspectors/CorePartDataEditor.uxml";
        private const string USS_PATH = "/Assets/Windows/PartAuthoring/Inspectors/CorePartDataEditor.uss";

        private const string SESSION_STATE_KEY_ACTIVE_TAB = "PartAuthoring.ActiveTab";
        private const string DEFAULT_TAB = "core";
        private const string TAB_ACTIVE_CLASS = "part-tab--active";

        private static readonly string[] TAB_IDS = { "core", "modules", "variants", "interacts" };

        private VisualElement _root;
        private VisualElement _tabContent;
        private string _activeTab;
        private Dictionary<PartBehaviourModule, HideFlags> _originalModuleHideFlags;

        private void OnEnable()
        {
            _activeTab = SessionState.GetString(SESSION_STATE_KEY_ACTIVE_TAB, DEFAULT_TAB);
            HideModuleComponentEditors();
        }

        private void OnDisable()
        {
            RestoreModuleComponentEditors();
        }

        private void HideModuleComponentEditors()
        {
            _originalModuleHideFlags = new Dictionary<PartBehaviourModule, HideFlags>();
            if (target is not CorePartData cpd)
            {
                return;
            }
            foreach (var module in cpd.gameObject.GetComponents<PartBehaviourModule>())
            {
                _originalModuleHideFlags[module] = module.hideFlags;
                module.hideFlags |= HideFlags.HideInInspector;
            }
        }

        private void RestoreModuleComponentEditors()
        {
            if (_originalModuleHideFlags == null)
            {
                return;
            }
            foreach (var pair in _originalModuleHideFlags)
            {
                if (pair.Key != null)
                {
                    pair.Key.hideFlags = pair.Value;
                }
            }
            _originalModuleHideFlags = null;
        }

        /// <inheritdoc />
        public override VisualElement CreateInspectorGUI()
        {
            _root = new VisualElement();

            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(SDKConfiguration.BasePath + UXML_PATH);
            if (tree == null)
            {
                _root.Add(new Label("Failed to load CorePartDataEditor.uxml"));
                return _root;
            }
            tree.CloneTree(_root);

            Ksp2UnityToolsStyles.Apply(_root, USS_PATH);

            _tabContent = _root.Q<VisualElement>("part-tab-content");

            PopulateIdentityRow();
            PopulateIcon();
            PopulateReadinessChips();
            WireQuickToolsChips();
            WireGizmoSettings();

            foreach (var id in TAB_IDS)
            {
                var button = _root.Q<Button>($"tab-{id}");
                if (button == null)
                {
                    continue;
                }
                var tabId = id;
                button.clicked += () => SetActiveTab(tabId);
            }

            UpdateTabHighlights();
            RenderActiveTab();
            return _root;
        }

        private void PopulateIdentityRow()
        {
            var partData = (target as CorePartData)?.Core?.data;
            if (partData == null)
            {
                return;
            }

            var nameLabel = _root.Q<Label>("header-part-name");
            if (nameLabel != null)
            {
                nameLabel.text = string.IsNullOrWhiteSpace(partData.partName) ? "<unnamed part>" : partData.partName;
            }

            var familyChip = _root.Q<Label>("header-family-chip");
            if (familyChip != null)
            {
                familyChip.text = $"family: {(string.IsNullOrEmpty(partData.family) ? "(none)" : partData.family)}";
            }

            var sizeChip = _root.Q<Label>("header-size-chip");
            if (sizeChip != null)
            {
                sizeChip.text = $"sizeCategory: {partData.sizeCategory}";
            }
        }

        private void PopulateIcon()
        {
            var iconEl = _root.Q<VisualElement>("header-icon");
            if (iconEl == null)
            {
                return;
            }
            var cpd = target as CorePartData;
            if (cpd == null)
            {
                return;
            }
            string iconPath = TryResolveIconPath(cpd);
            if (string.IsNullOrEmpty(iconPath))
            {
                return;
            }
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(iconPath);
            if (tex != null)
            {
                iconEl.style.backgroundImage = new StyleBackground(tex);
            }
        }

        private void PopulateReadinessChips()
        {
            var cpd = target as CorePartData;
            if (cpd == null)
            {
                return;
            }

            var jsonChip = _root.Q<Label>("readiness-chip-json");
            if (jsonChip != null)
            {
                bool exists = JsonSidecarExists(cpd);
                jsonChip.text = exists ? "JSON saved" : "JSON missing";
                SetReadinessState(jsonChip, exists ? "is-ok" : "is-warn");
            }

            var iconChip = _root.Q<Label>("readiness-chip-icon");
            if (iconChip != null)
            {
                bool exists = !string.IsNullOrEmpty(TryResolveIconPath(cpd));
                iconChip.text = exists ? "Icon baked" : "Icon missing";
                SetReadinessState(iconChip, exists ? "is-ok" : "is-warn");
            }

            var reentryChip = _root.Q<Label>("readiness-chip-reentry");
            if (reentryChip != null)
            {
                bool baked = ReentryMeshBaked(cpd);
                reentryChip.text = baked ? "Reentry baked" : "Reentry missing";
                SetReadinessState(reentryChip, baked ? "is-ok" : "is-warn");
            }

            var validChip = _root.Q<Label>("readiness-chip-valid");
            if (validChip != null)
            {
                validChip.text = "Validators pending";
                SetReadinessState(validChip, null);
            }
        }

        private static void SetReadinessState(Label chip, string stateClass)
        {
            chip.EnableInClassList("is-ok", stateClass == "is-ok");
            chip.EnableInClassList("is-warn", stateClass == "is-warn");
            chip.EnableInClassList("is-error", stateClass == "is-error");
        }

        private static string TryResolvePrefabDirectory(CorePartData cpd)
        {
            string prefabPath = PathUtils.GetPrefabOrAssetPath(cpd, cpd.gameObject);
            return string.IsNullOrEmpty(prefabPath) ? null : Path.GetDirectoryName(prefabPath);
        }

        private static string TryResolveIconPath(CorePartData cpd)
        {
            string dir = TryResolvePrefabDirectory(cpd);
            if (string.IsNullOrEmpty(dir))
            {
                return null;
            }
            string name = !string.IsNullOrWhiteSpace(cpd.Core?.data?.partName)
                ? cpd.Core.data.partName
                : cpd.gameObject.name;
            string iconPath = $"{dir}/{name}_icon.png".Replace('\\', '/');
            return File.Exists(iconPath) ? iconPath : null;
        }

        private static bool ReentryMeshBaked(CorePartData cpd)
        {
            return cpd.gameObject.GetComponentsInChildren<GeneratedReentryMeshRoot>(true).Length > 0;
        }

        private static bool JsonSidecarExists(CorePartData cpd)
        {
            string dir = TryResolvePrefabDirectory(cpd);
            if (string.IsNullOrEmpty(dir))
            {
                return false;
            }
            return File.Exists($"{dir}/{cpd.name}.json");
        }

        private void WireQuickToolsChips()
        {
            WireChip("chip-bake-icon", () => PartIconBaker.Bake((CorePartData)target));
            WireChip("chip-bake-reentry", () => ReentryMeshBaker.Bake((CorePartData)target));
            WireChip("chip-reexport-json", () => PartJsonSaver.Save((CorePartData)target));
            WireChip("chip-open-prefab", OpenPrefab);
        }

        private void WireChip(string name, System.Action onClick)
        {
            var btn = _root.Q<Button>(name);
            if (btn != null)
            {
                btn.clicked += onClick;
            }
        }

        private void OpenPrefab()
        {
            if (target is not CorePartData cpd)
            {
                return;
            }
            string prefabPath = AssetDatabase.GetAssetPath(cpd);
            if (string.IsNullOrEmpty(prefabPath))
            {
                var stage = PrefabStageUtility.GetCurrentPrefabStage();
                if (stage != null && stage.prefabContentsRoot != null && cpd.transform.root == stage.prefabContentsRoot.transform)
                {
                    // Already editing this part's prefab.
                    return;
                }
                prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(cpd.gameObject);
            }
            if (!string.IsNullOrEmpty(prefabPath))
            {
                PrefabStageUtility.OpenPrefab(prefabPath);
            }
        }

        private void WireGizmoSettings()
        {
            BindGizmoPill(
                "gizmo-pill-com",
                () => PartAuthoringGizmoSettings.ShowCenterOfMass,
                v => PartAuthoringGizmoSettings.ShowCenterOfMass = v);
            BindGizmoPill(
                "gizmo-pill-col",
                () => PartAuthoringGizmoSettings.ShowCenterOfLift,
                v => PartAuthoringGizmoSettings.ShowCenterOfLift = v);
            BindGizmoPill(
                "gizmo-pill-attach",
                () => PartAuthoringGizmoSettings.ShowAttachNodes,
                v => PartAuthoringGizmoSettings.ShowAttachNodes = v);
        }

        private void BindGizmoPill(string name, System.Func<bool> getter, System.Action<bool> setter)
        {
            var pill = _root.Q<Button>(name);
            if (pill == null)
            {
                return;
            }
            pill.EnableInClassList("is-active", getter());
            pill.clicked += () =>
            {
                bool newValue = !getter();
                setter(newValue);
                pill.EnableInClassList("is-active", newValue);
                SceneView.RepaintAll();
            };
        }

        private void SetActiveTab(string id)
        {
            if (id == _activeTab)
            {
                return;
            }
            _activeTab = id;
            SessionState.SetString(SESSION_STATE_KEY_ACTIVE_TAB, id);
            UpdateTabHighlights();
            RenderActiveTab();
        }

        private void UpdateTabHighlights()
        {
            if (_root == null)
            {
                return;
            }
            foreach (var id in TAB_IDS)
            {
                var button = _root.Q<Button>($"tab-{id}");
                if (button == null)
                {
                    continue;
                }
                button.EnableInClassList(TAB_ACTIVE_CLASS, id == _activeTab);
            }
        }

        private void RenderActiveTab()
        {
            if (_tabContent == null)
            {
                return;
            }
            _tabContent.Clear();
            switch (_activeTab)
            {
                case "core":
                    var cpd = (CorePartData)target;
                    _tabContent.Add(CoreDataSections.BuildIdentity(serializedObject));
                    _tabContent.Add(new IconPreviewSection(cpd));
                    _tabContent.Add(CoreDataSections.BuildMassCostCrew(serializedObject));
                    _tabContent.Add(CoreDataSections.BuildBreakageThermal(serializedObject));
                    _tabContent.Add(CoreDataSections.BuildAerodynamicsPhysics(serializedObject));
                    _tabContent.Add(CoreDataSections.BuildAttachment(serializedObject, cpd));
                    _tabContent.Add(CoreDataSections.BuildStaging(serializedObject));
                    _tabContent.Add(CoreDataSections.BuildCentersBuoyancy(serializedObject, cpd));
                    _tabContent.Add(CoreDataSections.BuildResources(serializedObject));
                    _tabContent.Add(CoreDataSections.BuildOabEditor(serializedObject));
                    _tabContent.Add(new ReentryMeshSection(cpd));
                    break;
                case "modules":
                    _tabContent.Add(ModulesTab.Build((CorePartData)target));
                    break;
                case "variants":
                case "interacts":
                    _tabContent.Add(BuildPlaceholder());
                    break;
                default:
                    _tabContent.Add(BuildPlaceholder());
                    break;
            }
        }

        private static HelpBox BuildPlaceholder()
        {
            return new HelpBox("Coming soon.", HelpBoxMessageType.Info);
        }
    }
}
