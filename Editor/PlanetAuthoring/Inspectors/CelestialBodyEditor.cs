using System.IO;
using System.Reflection;
using KSP;
using KSP.IO;
using KSP.Rendering.Planets;
using KSP.Sim.Definitions;
using Ksp2UnityTools.Editor.API;
using Ksp2UnityTools.Editor.IO;
using Ksp2UnityTools.Editor.PlanetAuthoring.Validation;
using Ksp2UnityTools.Editor.PlanetAuthoring.Windows;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Inspectors
{
    /// <summary>
    /// Custom inspector for <see cref="CoreCelestialBodyData" />.
    /// </summary>
    /// <remarks>
    /// Adds a readiness checklist, the Enable/Disable preview toggle, quick-launch shortcuts to the
    /// per-body authoring windows, and a Save Body JSON action that writes alongside the prefab.
    /// </remarks>
    [CustomEditor(typeof(CoreCelestialBodyData))]
    public class CelestialBodyEditor : UnityEditor.Editor
    {
        private const string UxmlPath = "/Assets/Windows/CelestialBodyInspector.uxml";
        private const string UssPath = "/Assets/Windows/CelestialBodyInspector.uss";

        private static bool _initialized;

        private Label _readinessHeader;
        private VisualElement _readinessErrors;
        private Button _previewButton;
        private VisualElement _starSection;
        private ValidationSectionBuilder.Handle _validationHandle;

        private CoreCelestialBodyData TargetData => (CoreCelestialBodyData)target;
        private GameObject TargetObject => TargetData.gameObject;
        private CelestialBodyCore TargetCore => TargetData.Core;

        /// <inheritdoc />
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();

            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(SDKConfiguration.BasePath + UxmlPath);
            if (tree == null)
            {
                root.Add(new Label("Failed to load CelestialBodyInspector.uxml"));
                return root;
            }
            tree.CloneTree(root);

            Ksp2UnityToolsStyles.Apply(root, UssPath);

            _readinessHeader = root.Q<Label>("readiness-header");
            _readinessErrors = root.Q<VisualElement>("readiness-errors");
            _previewButton = root.Q<Button>("preview-button");
            _previewButton.clicked += OnPreviewButtonClicked;
            _starSection = root.Q<VisualElement>("star-section");

            var validationSlot = root.Q<VisualElement>("validation-slot");
            if (validationSlot != null)
                _validationHandle = ValidationSectionBuilder.Mount(validationSlot);

            WireQuickToolButton(root, "quick-preview-controls", PreviewControlsWindow.ShowWindow);
            WireQuickToolButton(root, "quick-validation", PlanetAuthoringWindows.ShowValidationReportPlaceholder);
            WireQuickToolButton(root, "quick-biome-painter", PlanetAuthoringWindows.ShowBiomePainterPlaceholder);
            WireQuickToolButton(root, "quick-decal-manager", PlanetAuthoringWindows.ShowDecalManager);
            WireQuickToolButton(root, "quick-discoverable-manager", PlanetAuthoringWindows.ShowDiscoverableManager);

            BuildSaveSection(root.Q<VisualElement>("save-section-content"));
            BuildMineDustColorField(root.Q<VisualElement>("mine-dust-color-slot"));
            BuildSoiCalculationField(root.Q<VisualElement>("soi-calc-slot"));

            root.Bind(serializedObject);

            RefreshDynamicState();
            root.schedule.Execute(RefreshDynamicState).Every(500);
            return root;
        }

        private static void WireQuickToolButton(VisualElement root, string name, System.Action handler)
        {
            var button = root.Q<Button>(name);
            if (button != null)
                button.clicked += handler;
        }

        private void BuildSaveSection(VisualElement container)
        {
            if (container == null)
                return;

            string prefabPath = PathUtils.GetPrefabOrAssetPath(target, TargetObject);
            if (string.IsNullOrEmpty(prefabPath))
            {
                container.Add(new HelpBox(
                    "Body must be saved as a prefab to enable JSON export.",
                    HelpBoxMessageType.Info
                ));
                return;
            }

            container.Add(new Button(SaveBodyJson) { text = "Save Body JSON" });
        }

        private void BuildSoiCalculationField(VisualElement slot)
        {
            if (slot == null)
                return;

            slot.Clear();

            SerializedProperty prop = serializedObject.FindProperty("core.data.SphereOfInfluenceCalculationType");
            if (prop == null)
                return;

            var labels = new System.Collections.Generic.List<string>
            {
                "Child of Body",
                "Child of Galactic Origin",
                "Force None",
            };

            int initial = Mathf.Clamp(prop.intValue, 0, labels.Count - 1);
            var dropdown = new DropdownField("SOI Calculation Type", labels, initial)
            {
                tooltip = "Method used to compute this body's sphere of influence. " +
                          "Child of Body: standard SOI relative to the parent body. " +
                          "Child of Galactic Origin: SOI extends to one light-year (use for system primaries). " +
                          "Force None: SOI is zero (no gravitational capture).",
            };
            dropdown.AddToClassList("unity-base-field__aligned");
            dropdown.RegisterValueChangedCallback(evt =>
            {
                int idx = labels.IndexOf(evt.newValue);
                if (idx < 0) return;
                prop.intValue = idx;
                serializedObject.ApplyModifiedProperties();
            });

            slot.Add(dropdown);
        }

        private void BuildMineDustColorField(VisualElement slot)
        {
            if (slot == null)
                return;

            slot.Clear();

            SerializedProperty prop = serializedObject.FindProperty("core.data.MineDustColor");
            if (prop == null)
                return;

            SerializedProperty r = prop.FindPropertyRelative("x");
            SerializedProperty g = prop.FindPropertyRelative("y");
            SerializedProperty b = prop.FindPropertyRelative("z");
            SerializedProperty a = prop.FindPropertyRelative("w");

            var color = new ColorField("Mine Dust Color")
            {
                showAlpha = true,
                tooltip = "RGBA color of dust particles emitted by mining operations on this body.",
                value = new Color(r.floatValue, g.floatValue, b.floatValue, a.floatValue),
            };
            color.AddToClassList("unity-base-field__aligned");
            color.RegisterValueChangedCallback(evt =>
            {
                r.floatValue = evt.newValue.r;
                g.floatValue = evt.newValue.g;
                b.floatValue = evt.newValue.b;
                a.floatValue = evt.newValue.a;
                serializedObject.ApplyModifiedProperties();
            });

            slot.Add(color);
        }

        private void OnPreviewButtonClicked()
        {
            if (PlanetAuthoringSession.Active != null && PlanetAuthoringSession.Active.Body == TargetData)
            {
                PlanetAuthoringSession.Active.End();
                RefreshDynamicState();
                return;
            }

            CoreCelestialBodyData sceneBody = ResolveSceneBody(TargetData);
            if (sceneBody == null)
            {
                EditorUtility.DisplayDialog(
                    "Authoring scene missing",
                    "No authoring scene was found next to this body's prefab. Run 'Assets > KSP2 Unity Tools > Create Authoring Scene For Selected Celestial Body' on the root prefab first.",
                    "OK"
                );
                return;
            }

            var session = PlanetAuthoringSession.Begin(sceneBody);
            if (session != null && !EditorWindow.HasOpenInstances<Windows.PreviewControlsWindow>())
                Windows.PreviewControlsWindow.ShowWindow();
            RefreshDynamicState();
        }

        // Returns the scene-instance body for live preview. If target is a prefab asset, opens the
        // matching .unity next to it and returns the body instance found there. Returns null if the
        // authoring scene is missing.
        private static CoreCelestialBodyData ResolveSceneBody(CoreCelestialBodyData target)
        {
            if (target == null)
                return null;
            if (!PrefabUtility.IsPartOfPrefabAsset(target))
                return target;

            string prefabPath = AssetDatabase.GetAssetPath(target);
            if (string.IsNullOrEmpty(prefabPath))
                return null;
            string scenePath = Path.ChangeExtension(prefabPath, ".unity");
            if (!System.IO.File.Exists(scenePath))
                return null;

            Scene scene = default;
            for (int i = 0; i < EditorSceneManager.sceneCount; i++)
            {
                Scene candidate = EditorSceneManager.GetSceneAt(i);
                if (candidate.path == scenePath)
                {
                    scene = candidate;
                    break;
                }
            }
            // Additive so any other scenes the user has open (boot-ksp, work scenes) survive.
            // Single-mode loading would close them and risk losing unsaved changes.
            if (!scene.IsValid())
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);

            string bodyName = target.Core?.data?.bodyName;
            foreach (GameObject go in scene.GetRootGameObjects())
            {
                CoreCelestialBodyData ccd = go.GetComponentInChildren<CoreCelestialBodyData>(true);
                if (ccd != null && (string.IsNullOrEmpty(bodyName) || ccd.Core?.data?.bodyName == bodyName))
                    return ccd;
            }
            return null;
        }

        private void RefreshDynamicState()
        {
            if (target == null || _readinessHeader == null)
                return;

            PlanetAuthoringSession.ReadinessReport report = PlanetAuthoringSession.CheckReadiness(TargetData);
            UpdateReadinessSection(report);
            UpdatePreviewButton(report);
            UpdateStarSection();
            if (_validationHandle.IsValid)
                ValidationSectionBuilder.Refresh(_validationHandle, TargetData);
        }

        private void UpdateStarSection()
        {
            if (_starSection == null)
                return;

            bool isStar = TargetCore?.data?.isStar == true;
            _starSection.style.display = isStar ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void UpdateReadinessSection(PlanetAuthoringSession.ReadinessReport report)
        {
            _readinessErrors.Clear();
            _readinessHeader.RemoveFromClassList("is-ready");
            _readinessHeader.RemoveFromClassList("has-errors");

            if (report.IsReady)
            {
                _readinessHeader.text = "All checks passed.";
                _readinessHeader.AddToClassList("is-ready");
                return;
            }

            int count = report.Errors.Count;
            _readinessHeader.text = count == 1
                ? "Readiness (1 issue):"
                : "Readiness (" + count + " issues):";
            _readinessHeader.AddToClassList("has-errors");

            foreach (PlanetAuthoringSession.ReadinessError error in report.Errors)
            {
                _readinessErrors.Add(BuildErrorRow(error));
            }
        }

        private VisualElement BuildErrorRow(PlanetAuthoringSession.ReadinessError error)
        {
            var row = new VisualElement();
            row.AddToClassList("body-inspector-error-row");

            var label = new Label("- " + error.Message);
            label.AddToClassList("body-inspector-error-label");
            row.Add(label);

            if (error.Code == PlanetAuthoringSession.ReadinessErrorCode.NoPqsData)
            {
                var fix = new Button(CreateEmptyPqsData) { text = "Fix" };
                fix.AddToClassList("body-inspector-error-fix");
                row.Add(fix);
            }

            return row;
        }

        private void CreateEmptyPqsData()
        {
            PQS pqs = TargetObject.GetComponentInChildren<PQS>(true);
            if (pqs == null)
                return;

            string bodyName = TargetCore?.data?.bodyName;
            if (string.IsNullOrEmpty(bodyName))
                bodyName = TargetObject.name;

            string prefabPath = PathUtils.GetPrefabOrAssetPath(target, TargetObject);
            string dir = !string.IsNullOrEmpty(prefabPath)
                ? Path.GetDirectoryName(prefabPath)
                : "Assets";
            string assetPath = AssetDatabase.GenerateUniqueAssetPath(dir + "/" + bodyName + "_PQSData.asset");

            var data = ScriptableObject.CreateInstance<PQSData>();
            AssetDatabase.CreateAsset(data, assetPath);
            AssetDatabase.SaveAssets();

            Undo.RecordObject(pqs, "Assign PQSData");
            pqs.data = data;
            EditorUtility.SetDirty(pqs);

            RefreshDynamicState();
        }

        private void UpdatePreviewButton(PlanetAuthoringSession.ReadinessReport report)
        {
            bool isMine = PlanetAuthoringSession.Active != null
                && PlanetAuthoringSession.Active.Body == TargetData;

            if (isMine)
            {
                _previewButton.text = "Disable Preview";
                _previewButton.SetEnabled(true);
            }
            else
            {
                _previewButton.text = "Enable Preview";
                _previewButton.SetEnabled(report.IsReady);
            }
        }

        private void SaveBodyJson()
        {
            if (!_initialized)
                Initialize();

            if (TargetCore == null)
                return;

            string prefabPath = PathUtils.GetPrefabOrAssetPath(target, TargetObject);
            if (string.IsNullOrEmpty(prefabPath))
                return;

            string bodyName = TargetCore.data.bodyName;
            if (string.IsNullOrEmpty(bodyName))
                bodyName = TargetObject.name;

            string path = Path.GetDirectoryName(prefabPath) + "/" + bodyName + ".json";

            string json = IOProvider.ToJson(
                TargetCore,
                new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }
            );
            JObject jObject = JObject.Parse(json);
            json = jObject.ToString(Formatting.Indented);

            string directoryName = new FileInfo(path).DirectoryName;
            Directory.CreateDirectory(directoryName);
            File.WriteAllText(path, json);
            AssetDatabase.ImportAsset(path);

            bool madeAddressable = false;
            AddressableAssetGroup group = PlanetAuthoringAddressables.ResolveCelestialBodiesGroup(target);
            if (group != null)
            {
                madeAddressable = true;
                AddressablesTools.MakeAddressable(
                    group,
                    path,
                    bodyName + ".json",
                    "celestial_bodies"
                );
            }

            AssetDatabase.Refresh();
            AssetDatabase.SaveAssets();

            EditorUtility.DisplayDialog(
                "Body Exported",
                madeAddressable
                    ? "JSON saved to: " + path
                    : "JSON saved to: " + path + "\nYou need to manually make it addressable.",
                "OK"
            );
        }

        private static void Initialize()
        {
            typeof(IOProvider).GetMethod("Init", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                ?.Invoke(null, new object[] { });
            _initialized = true;
        }
    }
}
