using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KSP;
using KSP.Rendering;
using KSP.Rendering.Planets;
using KSP.Sim.Definitions;
using Ksp2UnityTools.Editor.API;
using Ksp2UnityTools.Editor.Modding;
using Ksp2UnityTools.Editor.ScriptableObjects;
using Redux.CelestialBody;
using Uber.Scatter;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Wizards
{
    /// <summary>
    /// Wizard for creating a new celestial body.
    /// </summary>
    /// <remarks>
    /// Generates a Celestial.&lt;Key&gt;.prefab root carrying CoreCelestialBodyData with the addressable
    /// keys for Scaled and, when solid, Local. Scaled and Local are separate addressable prefab assets.
    /// An editor-only authoring scene with all three prefab instances plus a directional light is also
    /// created so live preview runs against a real Unity scene rather than a nested-prefab graph.
    /// </remarks>
    public class CreateCelestialBodyWindow : EditorWindow
    {
        private const string CelestialBodiesLabel = "celestial_bodies";
        private static string ProjectLevelGroupName => PlanetAuthoringAddressables.CelestialBodiesGroupName;

        // Default sun direction for new authoring scenes. Tuned to cast visible shadows across a
        // sphere centered at world origin without being directly overhead or grazing.
        private const float SunIntensity = 1.0f;
        private static readonly Quaternion SunRotation = Quaternion.Euler(50f, -30f, 0f);

        private const string LocalShaderName = "Redux/Environment/CelestialBody_Local";
        private const string ScaledShaderName = "KSP2/Planets/Scaled";

        private static readonly Color OkColor = new(0.4f, 0.8f, 0.4f);
        private static readonly Color ErrorColor = new(0.85f, 0.45f, 0.3f);

        private enum BodyType { SolidSurface, GasGiant, Star }

        private static readonly (BodyType Type, string Label)[] BodyTypeOptions =
        {
            (BodyType.SolidSurface, "Solid Surface"),
            (BodyType.GasGiant, "Gas Giant"),
            (BodyType.Star, "Star"),
        };

        /// <summary>
        /// Centralized naming convention for celestial body assets. Keeping every "Celestial.{key}"
        /// and "_Local.mat" string in one place so a future rename is a single edit.
        /// </summary>
        private static class Naming
        {
            public static string RootPrefab(string key) => $"Celestial.{key}.prefab";
            public static string ScaledPrefab(string key) => $"Celestial.{key}.Scaled.prefab";
            public static string LocalPrefab(string key) => $"Celestial.{key}.Local.prefab";
            public static string Scene(string key) => $"Celestial.{key}.unity";
            public static string ScaledMaterial(string key) => $"{key}_Scaled.mat";
            public static string LocalMaterial(string key) => $"{key}_Local.mat";
            public static string PqsData(string key) => $"{key}_PQS.asset";
            public static string PqsDecalData(string key) => $"{key}_PQSDecalData.asset";
            public static string ScienceRegions(string key) => $"{key}_ScienceRegions.asset";
            public static string RootGameObject(string key) => $"Celestial.{key}";
        }

        /// <summary>
        /// Opens the Create Celestial Body wizard window.
        /// </summary>
        [MenuItem("Assets/KSP2 Unity Tools/Planet Authoring/Celestial Body", priority = KSP2UnityTools.MenuPriority)]
        public static void ShowWindow()
        {
            var window = GetWindow<CreateCelestialBodyWindow>();
            window.titleContent = new GUIContent("Create Celestial Body");
            window.minSize = new Vector2(420, 220);
        }

        /// <summary>
        /// Generates an authoring scene next to a selected celestial body root prefab.
        /// </summary>
        /// <remarks>
        /// Migrates bodies without an existing authoring scene to the scene-based authoring flow.
        /// </remarks>
        [MenuItem("Assets/KSP2 Unity Tools/Planet Authoring/Create Authoring Scene For Selected Celestial Body", priority = KSP2UnityTools.MenuPriority + 1)]
        public static void CreateAuthoringSceneForSelected()
        {
            UnityEngine.Object selected = Selection.activeObject;
            string prefabPath = selected != null ? AssetDatabase.GetAssetPath(selected) : null;
            if (string.IsNullOrEmpty(prefabPath) || !prefabPath.EndsWith(".prefab"))
            {
                EditorUtility.DisplayDialog("Selection invalid", "Select the celestial body root prefab (Celestial.<Key>.prefab) in the Project window.", "OK");
                return;
            }

            GameObject rootPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (rootPrefab == null || rootPrefab.GetComponent<CoreCelestialBodyData>() == null)
            {
                EditorUtility.DisplayDialog("Not a celestial body root", prefabPath + " has no CoreCelestialBodyData component. Select the root body prefab.", "OK");
                return;
            }

            string folder = Path.GetDirectoryName(prefabPath)?.Replace('\\', '/');
            string fileName = Path.GetFileNameWithoutExtension(prefabPath);
            string scenePath = folder + "/" + fileName + ".unity";

            if (File.Exists(scenePath))
            {
                bool overwrite = EditorUtility.DisplayDialog(
                    "Authoring scene exists",
                    $"An authoring scene already exists at {scenePath}. Overwriting will discard any edits to it. Continue?",
                    "Overwrite",
                    "Cancel"
                );
                if (!overwrite)
                    return;
            }

            CelestialBodyData data = rootPrefab.GetComponent<CoreCelestialBodyData>().Core?.data;
            string scaledKey = data?.assetKeyScaled;
            string simulationKey = data?.assetKeySimulation;

            GameObject scaledPrefab = !string.IsNullOrEmpty(scaledKey)
                ? AssetDatabase.LoadAssetAtPath<GameObject>(folder + "/" + scaledKey)
                : null;
            GameObject localPrefab = !string.IsNullOrEmpty(simulationKey)
                ? AssetDatabase.LoadAssetAtPath<GameObject>(folder + "/" + simulationKey)
                : null;

            CreateAuthoringScene(scenePath, rootPrefab, scaledPrefab, localPrefab);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            UnityEngine.Object sceneAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(scenePath);
            if (sceneAsset != null)
            {
                Selection.activeObject = sceneAsset;
                EditorGUIUtility.PingObject(sceneAsset);
            }
        }

        private TextField _keyField;
        private DropdownField _typeField;
        private Label _statusLabel;
        private Button _createButton;

        private void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.style.paddingLeft = 8;
            root.style.paddingRight = 8;
            root.style.paddingTop = 8;
            root.style.paddingBottom = 8;

            _keyField = new TextField("Body Key")
            {
                tooltip = "Internal name for this body. Must start with a letter and contain only letters, digits, or underscores.",
            };
            _keyField.RegisterValueChangedCallback(_ => Validate());
            root.Add(_keyField);

            _typeField = new DropdownField(
                "Body Type",
                BodyTypeOptions.Select(o => o.Label).ToList(),
                0
            );
            _typeField.tooltip = "Solid Surface bodies get both Local and Scaled prefabs. Gas Giants and Stars get only the Scaled prefab.";
            _typeField.RegisterValueChangedCallback(_ => Validate());
            root.Add(_typeField);

            _statusLabel = new Label();
            _statusLabel.style.whiteSpace = WhiteSpace.Normal;
            _statusLabel.style.marginTop = 8;
            _statusLabel.style.marginBottom = 8;
            root.Add(_statusLabel);

            var buttons = new VisualElement();
            buttons.style.flexDirection = FlexDirection.Row;
            buttons.style.justifyContent = Justify.FlexEnd;
            buttons.style.marginTop = 8;

            var cancel = new Button(Close) { text = "Cancel" };
            _createButton = new Button(OnCreate) { text = "Create" };
            _createButton.style.marginLeft = 6;

            buttons.Add(cancel);
            buttons.Add(_createButton);
            root.Add(buttons);

            Validate();
        }

        private static string ResolveTargetFolder()
        {
            UnityEngine.Object selected = Selection.activeObject;
            if (selected != null)
            {
                string path = AssetDatabase.GetAssetPath(selected);
                if (!string.IsNullOrEmpty(path))
                {
                    if (Directory.Exists(path))
                        return path;
                    if (File.Exists(path))
                        return Path.GetDirectoryName(path)?.Replace('\\', '/');
                }
            }
            return "Assets";
        }

        private static bool IsValidKey(string key)
        {
            if (string.IsNullOrEmpty(key))
                return false;
            if (!char.IsLetter(key[0]))
                return false;
            foreach (char c in key)
            {
                if (!char.IsLetterOrDigit(c) && c != '_')
                    return false;
            }
            return true;
        }

        private BodyType CurrentBodyType()
        {
            string label = _typeField.value;
            foreach (var option in BodyTypeOptions)
            {
                if (option.Label == label)
                    return option.Type;
            }
            return BodyType.SolidSurface;
        }

        private void Validate()
        {
            string key = _keyField.value;
            string parent = ResolveTargetFolder();

            if (!IsValidKey(key))
            {
                _statusLabel.text = "Body Key must start with a letter and contain only letters, digits, or underscores.";
                _statusLabel.style.color = ErrorColor;
                _createButton.SetEnabled(false);
                return;
            }

            string folder = parent + "/" + key;
            if (Directory.Exists(folder))
            {
                _statusLabel.text = $"Folder {folder} already exists.";
                _statusLabel.style.color = ErrorColor;
                _createButton.SetEnabled(false);
                return;
            }

            bool solid = CurrentBodyType() == BodyType.SolidSurface;
            string preview = $"Will create folder {folder} with:\n  {Naming.RootPrefab(key)}\n  {Naming.ScaledPrefab(key)}\n  {Naming.ScaledMaterial(key)}";
            if (solid)
                preview += $"\n  {Naming.LocalPrefab(key)}\n  {Naming.LocalMaterial(key)}\n  {Naming.PqsData(key)}\n  {Naming.ScienceRegions(key)}";
            _statusLabel.text = preview;
            _statusLabel.style.color = OkColor;
            _createButton.SetEnabled(true);
        }

        private void OnCreate()
        {
            string key = _keyField.value;
            string parent = ResolveTargetFolder();
            BodyType type = CurrentBodyType();

            try
            {
                if (CreateAssets(key, parent, type))
                    Close();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                EditorUtility.DisplayDialog("Create Celestial Body Failed", ex.Message + "\n\nPartial assets were rolled back.", "OK");
            }
        }

        // Returns false when the user aborted (e.g. shader missing dialog), true on success.
        // Throws on a creation error after rolling back the assets it produced.
        private static bool CreateAssets(string key, string parentFolder, BodyType type)
        {
            if (!TryResolveShaders(type, out Shader localShader, out Shader scaledShader))
                return false;

            string folder = parentFolder + "/" + key;
            var createdPaths = new List<string>();
            bool succeeded = false;

            try
            {
                AssetDatabase.CreateFolder(parentFolder, key);
                createdPaths.Add(folder);

                bool solid = type == BodyType.SolidSurface;
                Material scaledMaterial = CreateScaledMaterial(key, folder, scaledShader, createdPaths);
                Material localMaterial = solid ? CreateLocalMaterial(key, folder, localShader, createdPaths) : null;
                PQSData pqsData = solid ? CreatePqsData(key, folder, localMaterial, createdPaths) : null;
                PQSDecalData decalData = solid ? CreatePqsDecalData(key, folder, createdPaths) : null;
                if (solid)
                    CreateScienceRegionData(key, folder, createdPaths);

                GameObject scaledPrefab = CreateScaledPrefab(key, folder, scaledMaterial, createdPaths);
                GameObject localPrefab = solid ? CreateLocalPrefab(key, folder, pqsData, decalData, createdPaths) : null;
                GameObject rootPrefab = CreateRootPrefab(key, folder, type, createdPaths);

                RegisterAddressables(scaledPrefab, localPrefab, key);

                string scenePath = folder + "/" + Naming.Scene(key);
                CreateAuthoringScene(scenePath, rootPrefab, scaledPrefab, localPrefab);
                createdPaths.Add(scenePath);

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Selection.activeObject = rootPrefab;
                EditorGUIUtility.PingObject(rootPrefab);
                succeeded = true;
                return true;
            }
            finally
            {
                if (!succeeded)
                    Rollback(createdPaths);
            }
        }

        private static bool TryResolveShaders(BodyType type, out Shader localShader, out Shader scaledShader)
        {
            scaledShader = Shader.Find(ScaledShaderName);
            localShader = type == BodyType.SolidSurface ? Shader.Find(LocalShaderName) : null;

            if (scaledShader == null)
            {
                EditorUtility.DisplayDialog(
                    "Shader missing",
                    $"Could not find shader '{ScaledShaderName}'. Ensure the SDK shader package is imported before creating a celestial body.",
                    "OK"
                );
                return false;
            }

            if (type == BodyType.SolidSurface && localShader == null)
            {
                EditorUtility.DisplayDialog(
                    "Shader missing",
                    $"Could not find shader '{LocalShaderName}'. Ensure the SDK shader package is imported before creating a solid-surface body.",
                    "OK"
                );
                return false;
            }

            return true;
        }

        private static void Rollback(List<string> createdPaths)
        {
            // Reverse order so leaves go before their parent folder.
            for (int i = createdPaths.Count - 1; i >= 0; i--)
            {
                string p = createdPaths[i];
                if (!string.IsNullOrEmpty(p))
                    AssetDatabase.DeleteAsset(p);
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static Material CreateScaledMaterial(string key, string folder, Shader shader, List<string> createdPaths)
        {
            Material mat = new(shader) { name = $"{key}_Scaled" };
            string path = folder + "/" + Naming.ScaledMaterial(key);
            AssetDatabase.CreateAsset(mat, path);
            createdPaths.Add(path);
            return mat;
        }

        private static Material CreateLocalMaterial(string key, string folder, Shader shader, List<string> createdPaths)
        {
            Material mat = new(shader) { name = $"{key}_Local" };
            string path = folder + "/" + Naming.LocalMaterial(key);
            AssetDatabase.CreateAsset(mat, path);
            createdPaths.Add(path);
            return mat;
        }

        private static PQSData CreatePqsData(string key, string folder, Material localMaterial, List<string> createdPaths)
        {
            PQSData data = ScriptableObject.CreateInstance<PQSData>();
            data.materialSettings.surfaceMaterial = localMaterial;
            string path = folder + "/" + Naming.PqsData(key);
            AssetDatabase.CreateAsset(data, path);
            createdPaths.Add(path);
            return data;
        }

        private static PQSDecalData CreatePqsDecalData(string key, string folder, List<string> createdPaths)
        {
            PQSDecalData data = ScriptableObject.CreateInstance<PQSDecalData>();
            string path = folder + "/" + Naming.PqsDecalData(key);
            AssetDatabase.CreateAsset(data, path);
            createdPaths.Add(path);
            return data;
        }

        private static ScienceRegionData CreateScienceRegionData(string key, string folder, List<string> createdPaths)
        {
            ScienceRegionData data = ScriptableObject.CreateInstance<ScienceRegionData>();
            // Pre-populate BodyName so the asset locator can match this asset to the new body
            // without the artist having to fill it in manually.
            data.information.BodyName = key;
            data.information.Version = "1.0";
            // Sensible situation-data defaults. Scalars at 1.0 = situation is enabled and unscaled
            // (0 disables the situation entirely, which is rarely what the artist wants for a fresh body).
            // Altitudes stay at 0 because they're meaningful only relative to the body's final radius,
            // which the artist sets after creation.
            data.information.SituationData = new KSP.Game.Science.CBSituationData
            {
                HighOrbitMaxAltitude = 0,
                LowOrbitMaxAltutude = 0,
                AtmosphereMaxAltutude = 0,
                CelestialBodyScalar = 1f,
                HighOrbitScalar = 1f,
                LowOrbitScalar = 1f,
                AtmosphereScalar = 1f,
                SplashedScalar = 1f,
                LandedScalar = 1f,
            };
            string path = folder + "/" + Naming.ScienceRegions(key);
            AssetDatabase.CreateAsset(data, path);
            createdPaths.Add(path);
            return data;
        }

        private static GameObject CreateScaledPrefab(string key, string folder, Material scaledMaterial, List<string> createdPaths)
        {
            GameObject temp = new("Scaled");
            try
            {
                temp.AddComponent<MeshFilter>();
                MeshRenderer renderer = temp.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = scaledMaterial;
                temp.AddComponent<CelestialBodyLighting>();
                temp.AddComponent<CelestialBodyPostProcess>();
                temp.AddComponent<CelestialScaledMaterialReplacer>();

                string path = folder + "/" + Naming.ScaledPrefab(key);
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(temp, path);
                createdPaths.Add(path);
                return prefab;
            }
            finally
            {
                if (temp != null)
                    DestroyImmediate(temp);
            }
        }

        private static GameObject CreateLocalPrefab(string key, string folder, PQSData pqsData, PQSDecalData decalData, List<string> createdPaths)
        {
            GameObject temp = new("Local");
            try
            {
                PQS pqs = temp.AddComponent<PQS>();
                PQSRenderer pqsRenderer = temp.AddComponent<PQSRenderer>();
                pqs.PQSRenderer = pqsRenderer;
                pqsRenderer.Pqs = pqs;
                pqs.data = pqsData;
                pqs.isActive = true;
                temp.AddComponent<CelestialSimulationMaterialReplacer>();
                temp.AddComponent<PqsTerrain>();
                PQSDecalController decalController = temp.AddComponent<PQSDecalController>();
                // AddComponent leaves enabled=true by default, but make this explicit so a future
                // class-level [DisallowMultipleComponent] / OnValidate flip can't quietly disable it.
                decalController.enabled = true;
                decalController.PqsDecalData = decalData;
                decalController.Pqs = pqs;
                decalController.SharedHeightmap = AssetDatabase.LoadAssetAtPath<Texture2D>(SDKConfiguration.BasePath + "/Assets/DecalMaps/Flat Decal.png");
                decalController.SharedAlphaMap = AssetDatabase.LoadAssetAtPath<Texture2D>(SDKConfiguration.BasePath + "/Assets/DecalMaps/Full Decal Alpha.png");

                string path = folder + "/" + Naming.LocalPrefab(key);
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(temp, path);
                createdPaths.Add(path);
                return prefab;
            }
            finally
            {
                if (temp != null)
                    DestroyImmediate(temp);
            }
        }

        private static GameObject CreateRootPrefab(string key, string folder, BodyType type, List<string> createdPaths)
        {
            GameObject temp = new(Naming.RootGameObject(key));
            try
            {
                CoreCelestialBodyData coreData = temp.AddComponent<CoreCelestialBodyData>();
                coreData.Core.data = new CelestialBodyData
                {
                    bodyName = key,
                    isStar = type == BodyType.Star,
                    hasSolidSurface = type == BodyType.SolidSurface,
                    assetKeyScaled = Naming.ScaledPrefab(key),
                    assetKeySimulation = Naming.LocalPrefab(key),
                };

                string path = folder + "/" + Naming.RootPrefab(key);
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(temp, path);
                createdPaths.Add(path);
                return prefab;
            }
            finally
            {
                if (temp != null)
                    DestroyImmediate(temp);
            }
        }

        // Editor-only authoring scene with root, Scaled, and Local prefab instances plus a sun light.
        // The runtime never loads it. CelestialBodyAuthoringSceneGuard blocks play-mode entry against it.
        private static void CreateAuthoringScene(string scenePath, GameObject rootPrefab, GameObject scaledPrefab, GameObject localPrefab)
        {
            // Open additively so the user's currently-loaded scenes are not disturbed.
            Scene authoringScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);

            var sun = new GameObject("Sun");
            Light light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = SunIntensity;
            sun.transform.rotation = SunRotation;
            SceneManager.MoveGameObjectToScene(sun, authoringScene);

            GameObject rootInstance = (GameObject)PrefabUtility.InstantiatePrefab(rootPrefab, authoringScene);
            if (scaledPrefab != null)
            {
                GameObject scaledInstance = (GameObject)PrefabUtility.InstantiatePrefab(scaledPrefab, authoringScene);
                scaledInstance.transform.SetParent(rootInstance.transform);
            }
            if (localPrefab != null)
            {
                GameObject localInstance = (GameObject)PrefabUtility.InstantiatePrefab(localPrefab, authoringScene);
                localInstance.transform.SetParent(rootInstance.transform);
                // Pre-wire the controller's body reference so DecalControllerHelper doesn't need
                // its scene-walk fallback for newly-authored bodies. Without this, the controller's
                // CoreCelestialBodyData stays null until something calls Resolve.
                var bodyData = rootInstance.GetComponentInChildren<CoreCelestialBodyData>(true);
                var decalController = localInstance.GetComponentInChildren<PQSDecalController>(true);
                if (decalController != null && bodyData != null && decalController.CoreCelestialBodyData == null)
                {
                    decalController.CoreCelestialBodyData = bodyData;
                    EditorUtility.SetDirty(decalController);
                }
            }

            EditorSceneManager.SaveScene(authoringScene, scenePath);
            EditorSceneManager.CloseScene(authoringScene, true);
        }

        private static void RegisterAddressables(GameObject scaled, GameObject local, string key)
        {
            Mod mod = KSP2UnityTools.FindParentMod(scaled);
            if (mod != null && mod.celestialBodiesGroup != null)
            {
                AddToGroup(mod.celestialBodiesGroup, scaled, local, key);
                return;
            }

            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            AddressableAssetGroup projectGroup = settings?.groups.FirstOrDefault(g => g != null && g.Name == ProjectLevelGroupName);
            if (projectGroup != null)
            {
                bool ok = EditorUtility.DisplayDialog(
                    "Register addressables",
                    $"Register the new Scaled and Local prefabs in the '{ProjectLevelGroupName}' addressables group? Declining means you'll need to wire them in manually.",
                    "Yes",
                    "No"
                );
                if (ok)
                    AddToGroup(projectGroup, scaled, local, key);
                return;
            }

            EditorUtility.DisplayDialog(
                "Addressables not registered",
                $"No parent mod and no '{ProjectLevelGroupName}' addressables group found. The Scaled and Local prefabs were created but not registered. Add them to addressables manually.",
                "OK"
            );
        }

        private static void AddToGroup(AddressableAssetGroup group, GameObject scaled, GameObject local, string key)
        {
            AddressablesTools.MakeAddressable(group, AssetDatabase.GetAssetPath(scaled), Naming.ScaledPrefab(key), CelestialBodiesLabel);
            if (local != null)
                AddressablesTools.MakeAddressable(group, AssetDatabase.GetAssetPath(local), Naming.LocalPrefab(key), CelestialBodiesLabel);
        }
    }
}
