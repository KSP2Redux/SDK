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
using Ksp2UnityTools.Editor.PlanetAuthoring.Tools;
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
    /// Generates a Celestial.&lt;Key&gt;.Scaled.prefab carrying CoreCelestialBodyData and, when solid,
    /// a sibling Celestial.&lt;Key&gt;.Local.prefab. Both are separate addressable prefab assets.
    /// An editor-only authoring scene with the Scaled and Local prefabs as sibling scene roots is
    /// also created so live preview runs against a real Unity scene rather than a nested-prefab graph.
    /// Keeping them as siblings (not parent/child) avoids accidentally nesting Local under Scaled
    /// and dirtying the Scaled prefab asset with a transform-child override.
    /// </remarks>
    public class CreateCelestialBodyWindow : EditorWindow
    {
        private static string ProjectLevelGroupName => PlanetAuthoringAddressables.CelestialBodiesGroupName;

        // Default sun direction for new authoring scenes. Tuned to cast visible shadows across a
        // sphere centered at world origin without being directly overhead or grazing.
        private const float SunIntensity = 1.0f;
        private static readonly Quaternion SunRotation = Quaternion.Euler(50f, -30f, 0f);

        private const string LocalShaderName = "Redux/Environment/CelestialBody_Local";

        private static readonly Color OkColor = new(0.4f, 0.8f, 0.4f);
        private static readonly Color ErrorColor = new(0.85f, 0.45f, 0.3f);

        private enum BodyType { SolidSurface, GasGiant, Star }

        private static readonly (BodyType Type, string Label)[] BodyTypeOptions =
        {
            (BodyType.SolidSurface, "Solid Surface"),
            (BodyType.GasGiant, "Gas Giant"),
            (BodyType.Star, "Star"),
        };

        // Naming conventions for celestial body assets live in PlanetAuthoringNaming.

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
        /// Generates an authoring scene next to a selected Scaled celestial body prefab.
        /// </summary>
        /// <remarks>
        /// Use for bodies that have prefabs but no authoring scene yet. Select the Celestial.&lt;Key&gt;.Scaled.prefab.
        /// </remarks>
        [MenuItem("Assets/KSP2 Unity Tools/Planet Authoring/Create Authoring Scene For Selected Celestial Body", priority = KSP2UnityTools.MenuPriority + 1)]
        public static void CreateAuthoringSceneForSelected()
        {
            UnityEngine.Object selected = Selection.activeObject;
            string prefabPath = selected != null ? AssetDatabase.GetAssetPath(selected) : null;
            if (string.IsNullOrEmpty(prefabPath) || !prefabPath.EndsWith(".prefab"))
            {
                EditorUtility.DisplayDialog("Selection invalid", "Select the Scaled celestial body prefab (Celestial.<Key>.Scaled.prefab) in the Project window.", "OK");
                return;
            }

            GameObject scaledPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (scaledPrefab == null || scaledPrefab.GetComponent<CoreCelestialBodyData>() == null)
            {
                EditorUtility.DisplayDialog("Not a celestial body prefab", prefabPath + " has no CoreCelestialBodyData component. Select the Scaled body prefab.", "OK");
                return;
            }

            string folder = Path.GetDirectoryName(prefabPath)?.Replace('\\', '/');
            string fileName = Path.GetFileNameWithoutExtension(prefabPath);
            // Strip ".Scaled" suffix to get the body's base name for the scene file.
            string sceneStem = fileName.EndsWith(".Scaled") ? fileName.Substring(0, fileName.Length - ".Scaled".Length) : fileName;
            string scenePath = folder + "/" + sceneStem + ".unity";

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

            CelestialBodyData data = scaledPrefab.GetComponent<CoreCelestialBodyData>().Core?.data;
            string simulationKey = data?.assetKeySimulation;

            GameObject localPrefab = !string.IsNullOrEmpty(simulationKey)
                ? AssetDatabase.LoadAssetAtPath<GameObject>(folder + "/" + simulationKey)
                : null;

            CreateAuthoringScene(scenePath, scaledPrefab, localPrefab);
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
            string preview = $"Will create folder {folder} with:\n  {PlanetAuthoringNaming.ScaledPrefab(key)}\n  {PlanetAuthoringNaming.ScaledMaterial(key)}";
            if (solid)
                preview += $"\n  {PlanetAuthoringNaming.LocalPrefab(key)}\n  {PlanetAuthoringNaming.LocalMaterial(key)}\n  {PlanetAuthoringNaming.PqsData(key)}\n  {PlanetAuthoringNaming.ScienceRegions(key)}";
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
            if (!TryResolveLayers(type, out int scaledLayer, out int localLayer))
                return false;

            string folder = parentFolder + "/" + key;
            var createdPaths = new List<string>();
            bool succeeded = false;

            try
            {
                string folderGuid = AssetDatabase.CreateFolder(parentFolder, key);
                if (string.IsNullOrEmpty(folderGuid))
                    throw new InvalidOperationException($"Could not create folder '{folder}'.");
                createdPaths.Add(folder);

                bool solid = type == BodyType.SolidSurface;
                Material scaledMaterial = CreateScaledMaterial(key, folder, scaledShader, createdPaths);
                Material localMaterial = solid ? CreateLocalMaterial(key, folder, localShader, createdPaths) : null;
                PQSData pqsData = solid ? CreatePqsData(key, folder, localMaterial, createdPaths) : null;
                PQSDecalData decalData = solid ? CreatePqsDecalData(key, folder, createdPaths) : null;
                if (solid)
                    CreateScienceRegionData(key, folder, createdPaths);

                GameObject scaledPrefab = CreateScaledPrefab(key, folder, type, scaledMaterial, scaledLayer, createdPaths);
                GameObject localPrefab = solid ? CreateLocalPrefab(key, folder, pqsData, decalData, localLayer, createdPaths) : null;

                RegisterAddressables(scaledPrefab, localPrefab, key);

                string scenePath = folder + "/" + PlanetAuthoringNaming.Scene(key);
                CreateAuthoringScene(scenePath, scaledPrefab, localPrefab);
                createdPaths.Add(scenePath);

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Selection.activeObject = scaledPrefab;
                EditorGUIUtility.PingObject(scaledPrefab);
                succeeded = true;
                return true;
            }
            finally
            {
                if (!succeeded)
                    Rollback(createdPaths);
            }
        }

        // Required project-side layers used by the scaled-space and local-space rendering passes.
        // Both layers are stamped by the SDK import, so a missing layer means the import is broken
        // or incomplete - refuse to create the body and tell the artist to re-import the SDK rather
        // than nudging them to fix Tags and Layers by hand.
        private static bool TryResolveLayers(BodyType type, out int scaledLayer, out int localLayer)
        {
            scaledLayer = LayerMask.NameToLayer(PlanetAuthoringLayers.Scaled);
            localLayer = type == BodyType.SolidSurface ? LayerMask.NameToLayer(PlanetAuthoringLayers.Local) : 0;

            if (scaledLayer < 0)
                return BadImport(PlanetAuthoringLayers.Scaled);
            if (type == BodyType.SolidSurface && localLayer < 0)
                return BadImport(PlanetAuthoringLayers.Local);
            return true;

            static bool BadImport(string layerName)
            {
                Debug.LogError($"[CreateCelestialBody] Project layer '{layerName}' is missing. The SDK import did not register the layers it ships with - re-import the SDK before creating a celestial body.");
                EditorUtility.DisplayDialog(
                    "SDK import incomplete",
                    $"Project layer '{layerName}' is missing. The SDK import normally stamps it; re-import the SDK package and try again.",
                    "OK"
                );
                return false;
            }
        }

        private static bool TryResolveShaders(BodyType type, out Shader localShader, out Shader scaledShader)
        {
            scaledShader = Shader.Find(PlanetAuthoringShaders.Scaled);
            localShader = type == BodyType.SolidSurface ? Shader.Find(LocalShaderName) : null;

            if (scaledShader == null)
            {
                EditorUtility.DisplayDialog(
                    "Shader missing",
                    $"Could not find shader '{PlanetAuthoringShaders.Scaled}'. Ensure the SDK shader package is imported before creating a celestial body.",
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
            string path = folder + "/" + PlanetAuthoringNaming.ScaledMaterial(key);
            AssetDatabase.CreateAsset(mat, path);
            createdPaths.Add(path);
            return mat;
        }

        private static Material CreateLocalMaterial(string key, string folder, Shader shader, List<string> createdPaths)
        {
            Material mat = new(shader) { name = $"{key}_Local" };
            // The shader declares these as (0,0,0,0) / (1,1,1,1) defaults, but the local-body
            // fragment divides (normDist - fadeNeg) / (fadePos - fadeNeg) using values derived from
            // _DistanceResampleDistances. All-zero distances produce NaN, which propagates through
            // every blend in the deferred-base pass and the GPU discards NaN fragments - the body
            // then renders as completely blank with no error. Stamp the documented sane defaults so
            // a freshly-created body renders out of the box. UV scales follow the power-of-2
            // cascade the shader's PARAMS.md recommends.
            mat.SetVector("_DistanceResampleDistances", new Vector4(50f, 500f, 2000f, 12000f));
            mat.SetVector("_DistanceResampleUVScales", new Vector4(1f, 2f, 4f, 8f));
            string path = folder + "/" + PlanetAuthoringNaming.LocalMaterial(key);
            AssetDatabase.CreateAsset(mat, path);
            createdPaths.Add(path);
            return mat;
        }

        private static PQSData CreatePqsData(string key, string folder, Material localMaterial, List<string> createdPaths)
        {
            PQSData data = ScriptableObject.CreateInstance<PQSData>();
            data.materialSettings.surfaceMaterial = localMaterial;
            string path = folder + "/" + PlanetAuthoringNaming.PqsData(key);
            AssetDatabase.CreateAsset(data, path);
            createdPaths.Add(path);
            return data;
        }

        private static PQSDecalData CreatePqsDecalData(string key, string folder, List<string> createdPaths)
        {
            PQSDecalData data = ScriptableObject.CreateInstance<PQSDecalData>();
            string path = folder + "/" + PlanetAuthoringNaming.PqsDecalData(key);
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
            string path = folder + "/" + PlanetAuthoringNaming.ScienceRegions(key);
            AssetDatabase.CreateAsset(data, path);
            createdPaths.Add(path);
            return data;
        }

        private static GameObject CreateScaledPrefab(string key, string folder, BodyType type, Material scaledMaterial, int scaledLayer, List<string> createdPaths)
        {
            GameObject temp = new(PlanetAuthoringNaming.ScaledGameObject(key));
            try
            {
                temp.layer = scaledLayer;
                CoreCelestialBodyData coreData = temp.AddComponent<CoreCelestialBodyData>();
                bool solid = type == BodyType.SolidSurface;
                coreData.Core.data = new CelestialBodyData
                {
                    bodyName = key,
                    isStar = type == BodyType.Star,
                    hasSolidSurface = solid,
                    assetKeyScaled = PlanetAuthoringNaming.ScaledPrefab(key),
                    assetKeySimulation = solid ? PlanetAuthoringNaming.LocalPrefab(key) : null,
                };

                // Authoring-facing components first (Lighting + PostProcess data the artist tunes),
                // then the autogenerated mesh / renderer / collider / shader-swap that the baker
                // overwrites on every bake.
                var lighting = temp.AddComponent<CelestialBodyLighting>();
                lighting.Data = new CelestialBodyLightingData();
                var postProcess = temp.AddComponent<CelestialBodyPostProcess>();
                string postProcessPath = folder + "/" + PlanetAuthoringNaming.PostProcessData(key);
                var postProcessData = ScriptableObject.CreateInstance<PostProcessData>();
                AssetDatabase.CreateAsset(postProcessData, postProcessPath);
                createdPaths.Add(postProcessPath);
                postProcess.Data = postProcessData;

                temp.AddComponent<MeshFilter>();
                MeshRenderer renderer = temp.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = scaledMaterial;
                SphereCollider scaledCollider = temp.AddComponent<SphereCollider>();
                scaledCollider.radius = ScaledSpaceBakerOperation.AuthoredRadius;
                temp.AddComponent<CelestialScaledMaterialReplacer>();

                string path = folder + "/" + PlanetAuthoringNaming.ScaledPrefab(key);
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

        private static GameObject CreateLocalPrefab(string key, string folder, PQSData pqsData, PQSDecalData decalData, int localLayer, List<string> createdPaths)
        {
            GameObject temp = new("Local");
            try
            {
                temp.layer = localLayer;
                PQS pqs = temp.AddComponent<PQS>();
                PQSRenderer pqsRenderer = temp.AddComponent<PQSRenderer>();
                pqs.PQSRenderer = pqsRenderer;
                pqsRenderer.Pqs = pqs;
                pqs.data = pqsData;
                pqs.isActive = true;
                temp.AddComponent<PqsTerrain>();
                PQSDecalController decalController = temp.AddComponent<PQSDecalController>();
                // AddComponent leaves enabled=true by default, but make this explicit so a future
                // class-level [DisallowMultipleComponent] / OnValidate flip can't quietly disable it.
                decalController.enabled = true;
                decalController.PqsDecalData = decalData;
                decalController.Pqs = pqs;
                decalController.SharedHeightmap = AssetDatabase.LoadAssetAtPath<Texture2D>(SDKConfiguration.BasePath + "/Assets/DecalMaps/Full Decal Alpha.png");
                decalController.SharedAlphaMap = AssetDatabase.LoadAssetAtPath<Texture2D>(SDKConfiguration.BasePath + "/Assets/DecalMaps/Full Decal Alpha.png");

                string path = folder + "/" + PlanetAuthoringNaming.LocalPrefab(key);
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

        // Editor-only authoring scene with Scaled and Local prefabs as sibling scene roots, plus a
        // sun light. The runtime never loads it. CelestialBodyAuthoringSceneGuard blocks play-mode
        // entry against it. Sibling layout prevents accidentally nesting Local under Scaled and
        // dirtying the Scaled prefab asset with a transform-child override.
        private static void CreateAuthoringScene(string scenePath, GameObject scaledPrefab, GameObject localPrefab)
        {
            // Open additively so the user's currently-loaded scenes are not disturbed.
            Scene authoringScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);

            var sun = new GameObject("Sun");
            Light light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = SunIntensity;
            sun.transform.rotation = SunRotation;
            SceneManager.MoveGameObjectToScene(sun, authoringScene);

            GameObject scaledInstance = (GameObject)PrefabUtility.InstantiatePrefab(scaledPrefab, authoringScene);
            if (localPrefab != null)
            {
                GameObject localInstance = (GameObject)PrefabUtility.InstantiatePrefab(localPrefab, authoringScene);
                // Pre-wire the controller's body reference so DecalControllerHelper doesn't need
                // its scene-walk fallback for newly-authored bodies.
                var bodyData = scaledInstance.GetComponent<CoreCelestialBodyData>();
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
                    $"Add the new Scaled and Local prefabs to the '{ProjectLevelGroupName}' addressables group? Decline and you'll have to wire them up manually.",
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
            AddressablesTools.MakeAddressable(group, AssetDatabase.GetAssetPath(scaled), PlanetAuthoringNaming.ScaledPrefab(key));
            if (local != null)
                AddressablesTools.MakeAddressable(group, AssetDatabase.GetAssetPath(local), PlanetAuthoringNaming.LocalPrefab(key));
        }
    }
}
