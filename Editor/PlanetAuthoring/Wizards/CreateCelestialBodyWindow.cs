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
    /// Wizard for creating a new celestial body. Generates a Celestial.&lt;Key&gt;.prefab root carrying
    /// CoreCelestialBodyData (with the addressable keys for Scaled and, when solid, Local). Scaled
    /// and Local are separate addressable prefab assets. An editor-only authoring scene with all three
    /// prefab instances plus a directional light is also created so live preview runs against a real
    /// Unity scene rather than a nested-prefab graph.
    /// </summary>
    public class CreateCelestialBodyWindow : EditorWindow
    {
        private const string CelestialBodiesLabel = "celestial_bodies";
        private const string ProjectLevelGroupName = "Celestial Bodies";

        private static readonly Color OkColor = new(0.4f, 0.8f, 0.4f);
        private static readonly Color ErrorColor = new(0.85f, 0.45f, 0.3f);

        private enum BodyType { SolidSurface, GasGiant, Star }

        [MenuItem("Assets/KSP2 Unity Tools/Celestial Body", priority = KSP2UnityTools.MenuPriority)]
        public static void ShowWindow()
        {
            var window = GetWindow<CreateCelestialBodyWindow>();
            window.titleContent = new GUIContent("Create Celestial Body");
            window.minSize = new Vector2(420, 220);
        }

        /// <summary>
        /// Generates an authoring scene next to a selected celestial body root prefab. Migrates bodies
        /// without an existing authoring scene to the scene-based authoring flow.
        /// </summary>
        [MenuItem("Assets/KSP2 Unity Tools/Create Authoring Scene For Selected Celestial Body", priority = KSP2UnityTools.MenuPriority + 1)]
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
                new List<string> { "Solid Surface", "Gas Giant", "Star" },
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
            return _typeField.value switch
            {
                "Gas Giant" => BodyType.GasGiant,
                "Star" => BodyType.Star,
                _ => BodyType.SolidSurface,
            };
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
                _statusLabel.text = "Folder " + folder + " already exists.";
                _statusLabel.style.color = ErrorColor;
                _createButton.SetEnabled(false);
                return;
            }

            bool solid = CurrentBodyType() == BodyType.SolidSurface;
            string preview = "Will create folder " + folder + " with:\n  Celestial." + key + ".prefab\n  Celestial." + key + ".Scaled.prefab\n  " + key + "_Scaled.mat";
            if (solid)
            {
                preview += "\n  Celestial." + key + ".Local.prefab\n  " + key + "_Local.mat\n  " + key + "_PQS.asset";
            }
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
                CreateAssets(key, parent, type);
                Close();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                EditorUtility.DisplayDialog("Create Celestial Body Failed", ex.Message, "OK");
            }
        }

        private static void CreateAssets(string key, string parentFolder, BodyType type)
        {
            string folder = parentFolder + "/" + key;
            AssetDatabase.CreateFolder(parentFolder, key);

            bool solid = type == BodyType.SolidSurface;

            Shader localShader = Shader.Find("Redux/Environment/CelestialBody_Local");
            Shader scaledShader = Shader.Find("KSP2/Planets/Scaled");
            Shader fallback = Shader.Find("Standard");

            Material scaledMaterial = new Material(scaledShader != null ? scaledShader : fallback)
            {
                name = key + "_Scaled",
            };
            AssetDatabase.CreateAsset(scaledMaterial, folder + "/" + key + "_Scaled.mat");

            Material localMaterial = null;
            PQSData pqsData = null;
            PQSDecalData decalData = null;
            if (solid)
            {
                localMaterial = new Material(localShader != null ? localShader : fallback)
                {
                    name = key + "_Local",
                };
                AssetDatabase.CreateAsset(localMaterial, folder + "/" + key + "_Local.mat");

                pqsData = ScriptableObject.CreateInstance<PQSData>();
                pqsData.materialSettings.surfaceMaterial = localMaterial;
                AssetDatabase.CreateAsset(pqsData, folder + "/" + key + "_PQS.asset");

                decalData = ScriptableObject.CreateInstance<PQSDecalData>();
                AssetDatabase.CreateAsset(decalData, folder + "/" + key + "_PQSDecalData.asset");
            }

            string scaledPath = folder + "/Celestial." + key + ".Scaled.prefab";
            var scaledTemp = new GameObject("Scaled");
            scaledTemp.AddComponent<MeshFilter>();
            MeshRenderer scaledRenderer = scaledTemp.AddComponent<MeshRenderer>();
            scaledRenderer.sharedMaterial = scaledMaterial;
            scaledTemp.AddComponent<CelestialBodyLighting>();
            scaledTemp.AddComponent<CelestialBodyPostProcess>();
            scaledTemp.AddComponent<CelestialScaledMaterialReplacer>();
            GameObject scaledPrefab = PrefabUtility.SaveAsPrefabAsset(scaledTemp, scaledPath);
            DestroyImmediate(scaledTemp);

            GameObject localPrefab = null;
            if (solid)
            {
                string localPath = folder + "/Celestial." + key + ".Local.prefab";
                var localTemp = new GameObject("Local");
                PQS pqs = localTemp.AddComponent<PQS>();
                PQSRenderer pqsRenderer = localTemp.AddComponent<PQSRenderer>();
                pqs.PQSRenderer = pqsRenderer;
                pqsRenderer.Pqs = pqs;
                pqs.data = pqsData;
                pqs.isActive = true;
                localTemp.AddComponent<CelestialSimulationMaterialReplacer>();
                localTemp.AddComponent<PqsTerrain>();
                PQSDecalController decalController = localTemp.AddComponent<PQSDecalController>();
                decalController.PqsDecalData = decalData;
                decalController.Pqs = pqs;
                localPrefab = PrefabUtility.SaveAsPrefabAsset(localTemp, localPath);
                DestroyImmediate(localTemp);
            }

            string rootPath = folder + "/Celestial." + key + ".prefab";
            var rootTemp = new GameObject("Celestial." + key);
            CoreCelestialBodyData coreData = rootTemp.AddComponent<CoreCelestialBodyData>();
            coreData.Core.data = new CelestialBodyData
            {
                bodyName = key,
                isStar = type == BodyType.Star,
                hasSolidSurface = solid,
                assetKeyScaled = "Celestial." + key + ".Scaled.prefab",
                assetKeySimulation = "Celestial." + key + ".Local.prefab",
            };

            GameObject rootPrefab = PrefabUtility.SaveAsPrefabAsset(rootTemp, rootPath);
            DestroyImmediate(rootTemp);

            RegisterAddressables(scaledPrefab, localPrefab, key);

            string scenePath = folder + "/Celestial." + key + ".unity";
            CreateAuthoringScene(scenePath, rootPrefab, scaledPrefab, localPrefab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = rootPrefab;
            EditorGUIUtility.PingObject(rootPrefab);
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
            light.intensity = 1.0f;
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
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
                    "Register the new Scaled and Local prefabs in the '" + ProjectLevelGroupName + "' addressables group?",
                    "Yes",
                    "No"
                );
                if (ok)
                    AddToGroup(projectGroup, scaled, local, key);
                return;
            }

            EditorUtility.DisplayDialog(
                "Addressables not registered",
                "No parent mod and no '" + ProjectLevelGroupName + "' addressables group found. The Scaled and Local prefabs were created but not registered. Add them to addressables manually.",
                "OK"
            );
        }

        private static void AddToGroup(AddressableAssetGroup group, GameObject scaled, GameObject local, string key)
        {
            AddressablesTools.MakeAddressable(group, AssetDatabase.GetAssetPath(scaled), "Celestial." + key + ".Scaled.prefab", CelestialBodiesLabel);
            if (local != null)
                AddressablesTools.MakeAddressable(group, AssetDatabase.GetAssetPath(local), "Celestial." + key + ".Local.prefab", CelestialBodiesLabel);
        }
    }
}
