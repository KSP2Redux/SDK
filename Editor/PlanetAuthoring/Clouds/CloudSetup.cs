using System.Collections.Generic;
using System.IO;
using KSP;
using KSP.Rendering;
using KSP.Rendering.Planets;
using KSP.VolumeCloud;
using Ksp2UnityTools.Editor.API;
using Ksp2UnityTools.Editor.PlanetAuthoring.Authoring;
using Ksp2UnityTools.Editor.PlanetAuthoring.Tools;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Clouds
{
    /// <summary>
    /// Gives a celestial body volumetric clouds: the cloud configuration, its scaled counterpart, the addressable
    /// entry, the cloud helper on the Local prefab's PQS and the scaled cloud component on the Scaled prefab.
    /// </summary>
    /// <remarks>
    /// Running it again on a body that already has clouds refits the configuration's planet radius to the body,
    /// re-derives the layers and re-wires both prefabs without touching the layer settings. High and Medium quality load
    /// the configuration. Low loads a mirror of it that draws scaled clouds only, as stock's Low tiers do, so players on
    /// Low cloud quality get no volumetric clouds.
    /// </remarks>
    public static class CloudSetup
    {
        /// <summary>
        /// The folder, beside the scaled prefab, that holds the configuration and everything generated for it.
        /// </summary>
        public const string CLOUDS_FOLDER = "Clouds";

        /// <summary>
        /// Creates or refits the body's clouds and wires them into both prefabs.
        /// </summary>
        /// <remarks>
        /// A new configuration starts from stock Kerbin's settings when the base-game catalog is available, and from
        /// one empty layer otherwise.
        /// </remarks>
        /// <param name="body">The body, either the scaled prefab asset or an instance of it.</param>
        /// <param name="configuration">The created or refitted configuration, or null on failure.</param>
        /// <param name="message">A status line describing the outcome or the reason for failure.</param>
        /// <returns>True if the clouds were created or refitted, false otherwise.</returns>
        public static bool TryAddClouds(CoreCelestialBodyData body, out VolumeCloudConfiguration configuration, out string message)
        {
            configuration = null;
            if (body == null || body.Data == null)
            {
                message = "Could not resolve a body.";
                return false;
            }

            string scaledPath = ResolvePrefabPath(body);
            if (string.IsNullOrEmpty(scaledPath))
            {
                message = "The body is not part of a prefab, so there is nothing to wire the clouds into.";
                return false;
            }

            string localPath = ResolveLocalPrefabPath(body);
            if (string.IsNullOrEmpty(localPath))
            {
                message = "No Local prefab with a PQS was found. Clouds belong to solid bodies.";
                return false;
            }

            string bodyName = string.IsNullOrEmpty(body.Data.bodyName) ? body.name : body.Data.bodyName;
            string folder = $"{Path.GetDirectoryName(scaledPath)?.Replace('\\', '/')}/{CLOUDS_FOLDER}";
            configuration = LoadOrCreate<VolumeCloudConfiguration>(folder, $"{bodyName}_Clouds", out bool created);
            ScaledCloudConfiguration scaled = LoadOrCreate<ScaledCloudConfiguration>(folder, $"{bodyName}_ScaledClouds", out _);

            Undo.RecordObject(configuration, "Fit Clouds To Body");
            string presetNote = string.Empty;
            if (created)
            {
                InitializeLists(configuration);
                if (!CloudPresets.TryApply(configuration, "Kerbin", out _))
                {
                    configuration.cumulusList.Add(CreateDefaultLayer());
                    presetNote = " Stock Kerbin's clouds could not be read, so it starts from one default layer.";
                }
            }

            if (StockCloudNoise.TryLink(out Texture3D baseNoise, out Texture3D detailNoise, out _))
            {
                StockCloudNoise.AssignWhereMissing(configuration, baseNoise, detailNoise);
            }

            configuration.bodyName = bodyName;
            FitToBody(configuration, body.Data);
            DeriveLayers(configuration);
            EditorUtility.SetDirty(configuration);

            VolumeCloudConfigurationAuthoring sidecar = AuthoringSidecars.GetOrCreate(configuration);
            sidecar.ScaledConfiguration = scaled;
            sidecar.LowConfiguration = LoadOrCreate<VolumeCloudConfiguration>(folder, $"{bodyName}_Clouds_Low", out _);
            EditorUtility.SetDirty(sidecar);
            SyncScaled(configuration);
            SyncLowTier(configuration);

            bool registered = RegisterAddressable(configuration) && RegisterAddressable(sidecar.LowConfiguration);
            WireLocalPrefab(localPath, configuration, sidecar.LowConfiguration);
            WireScaledPrefab(scaledPath, scaled);
            AssetDatabase.SaveAssets();

            message = created
                ? $"Created {configuration.name}.{presetNote} Assign each layer a distribution map."
                : $"Refitted {configuration.name} to the body and re-wired both prefabs.";
            if (!registered)
            {
                message += " No Celestial Bodies addressables group was found, so the configuration is not addressable yet.";
            }

            return true;
        }

        /// <summary>
        /// Gets a value indicating whether the body has clouds wired into either prefab.
        /// </summary>
        /// <param name="body">The body, either the scaled prefab asset or an instance of it.</param>
        /// <returns>True if either prefab carries cloud components, false otherwise.</returns>
        public static bool HasClouds(CoreCelestialBodyData body) =>
            body != null && (body.TryGetComponent(out ScaledCloudDataModelComponent _) || FindHelper(body) != null);

        /// <summary>
        /// Finds the cloud helper on the body's PQS.
        /// </summary>
        /// <param name="body">The body.</param>
        /// <returns>The helper, or null when the body has none.</returns>
        public static CloudRenderHelper FindHelper(CoreCelestialBodyData body)
        {
            PQS pqs = BodyResolver.FindPqsIncludingAsset(body);
            return pqs != null && pqs.TryGetComponent(out CloudRenderHelper helper) ? helper : null;
        }

        /// <summary>
        /// Resolves the configuration a cloud helper loads at High quality, when it is a project asset.
        /// </summary>
        /// <remarks>
        /// A helper that references a stock configuration resolves to nothing here, since stock configurations live in
        /// the game's bundles rather than the project.
        /// </remarks>
        /// <param name="helper">The cloud helper.</param>
        /// <returns>The configuration, or null when the reference is empty or outside the project.</returns>
        public static VolumeCloudConfiguration FindConfiguration(CloudRenderHelper helper)
        {
            string guid = helper != null ? helper.HighQualityCloudConfiguration?.AssetGUID : null;
            if (string.IsNullOrEmpty(guid))
                return null;

            string path = AssetDatabase.GUIDToAssetPath(guid);
            return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<VolumeCloudConfiguration>(path);
        }

        /// <summary>
        /// Sizes a configuration to the body it renders for.
        /// </summary>
        /// <remarks>
        /// Layer heights are measured from the configuration's planet radius, so setting it to the body's sea-level
        /// radius makes every height a height above sea level.
        /// </remarks>
        /// <param name="configuration">The configuration to fit.</param>
        /// <param name="data">The body's data.</param>
        public static void FitToBody(VolumeCloudConfiguration configuration, KSP.Sim.Definitions.CelestialBodyData data)
        {
            configuration.planetRadius = (float)data.radius;
        }

        /// <summary>
        /// Recomputes what each layer derives from its heights, and the order the renderer walks the layers in.
        /// </summary>
        /// <remarks>
        /// A layer's baked height, where its scaled clouds sit, is the bottom of its height range, as it is on every
        /// stock layer.
        /// </remarks>
        /// <param name="configuration">The configuration to update.</param>
        public static void DeriveLayers(VolumeCloudConfiguration configuration)
        {
            InitializeLists(configuration);
            foreach (VolumeCloudConfiguration.CumulusData layer in configuration.cumulusList)
            {
                layer.bakedCloudHeight = layer.cloudHeightRange.x;
            }

            configuration.SortCloudsLayer();
        }

        /// <summary>
        /// Brings a configuration's scaled counterpart in line with it, one scaled layer per volumetric layer.
        /// </summary>
        /// <param name="configuration">The volumetric configuration.</param>
        /// <returns>True if a scaled configuration was found and updated, false otherwise.</returns>
        public static bool SyncScaled(VolumeCloudConfiguration configuration)
        {
            VolumeCloudConfigurationAuthoring sidecar = AuthoringSidecars.Find(configuration);
            ScaledCloudConfiguration scaled = sidecar != null ? sidecar.ScaledConfiguration : null;
            if (scaled == null)
                return false;

            Undo.RecordObject(scaled, "Sync Scaled Clouds");
            scaled.scaledCloudLayers ??= new List<ScaledCloudConfiguration.scaledCloudMaterialData>();
            CloudRenderList.RebuildScaledLayers(scaled, configuration);
            CloudRenderList.SyncScaledConfiguration(scaled, configuration);
            EditorUtility.SetDirty(scaled);
            return true;
        }

        /// <summary>
        /// Brings a configuration's Low tier mirror in line with it: every setting and layer copied, with scaled clouds only.
        /// </summary>
        /// <remarks>
        /// The scaled clouds sync their layers from whichever tier loaded, so the mirror keeps every layer even though it
        /// draws none of them volumetrically.
        /// </remarks>
        /// <param name="configuration">The configuration High and Medium quality load.</param>
        /// <returns>True if a Low tier mirror was found and updated, false otherwise.</returns>
        public static bool SyncLowTier(VolumeCloudConfiguration configuration)
        {
            VolumeCloudConfigurationAuthoring sidecar = AuthoringSidecars.Find(configuration);
            VolumeCloudConfiguration low = sidecar != null ? sidecar.LowConfiguration : null;
            if (low == null || low == configuration)
                return false;

            Undo.RecordObject(low, "Sync Low Quality Clouds");
            MirrorToLowTier(configuration, low);
            EditorUtility.SetDirty(low);
            return true;
        }

        /// <summary>
        /// Copies every setting and layer of <paramref name="source" /> onto <paramref name="low" />, keeping its name,
        /// and makes it draw scaled clouds only.
        /// </summary>
        /// <param name="source">The configuration High and Medium quality load.</param>
        /// <param name="low">The Low tier mirror to overwrite.</param>
        public static void MirrorToLowTier(VolumeCloudConfiguration source, VolumeCloudConfiguration low)
        {
            string name = low.name;
            EditorUtility.CopySerialized(source, low);
            low.name = name;
            low.useScaleCloudsOnly = true;
        }

        /// <summary>
        /// Lists what <see cref="TryRemoveClouds" /> would remove, for a confirmation prompt.
        /// </summary>
        /// <param name="body">The body, either the scaled prefab asset or an instance of it.</param>
        /// <returns>One line per thing removed, or an empty list when there are no clouds.</returns>
        public static List<string> DescribeRemoval(CoreCelestialBodyData body)
        {
            var lines = new List<string>();
            if (!HasClouds(body))
                return lines;

            lines.Add("the cloud helper on the Local prefab's PQS");
            lines.Add("the scaled cloud component on the Scaled prefab");
            VolumeCloudConfiguration configuration = FindConfiguration(FindHelper(body));
            if (configuration == null)
                return lines;

            lines.Add($"the addressable entry for {configuration.name}");
            lines.AddRange(OwnedAssetPaths(configuration));
            return lines;
        }

        /// <summary>
        /// Takes the clouds off a body: both prefabs' cloud components, the configuration's addressable entry, and the
        /// configuration with everything generated for it in the clouds folder.
        /// </summary>
        /// <remarks>
        /// Assets go to the OS trash rather than being deleted outright. Textures that live outside the clouds folder,
        /// such as a distribution map kept elsewhere, are left where they are.
        /// </remarks>
        /// <param name="body">The body, either the scaled prefab asset or an instance of it.</param>
        /// <param name="message">A status line describing the outcome or the reason for failure.</param>
        /// <returns>True if the clouds were removed, false otherwise.</returns>
        public static bool TryRemoveClouds(CoreCelestialBodyData body, out string message)
        {
            if (!HasClouds(body))
            {
                message = "This body has no clouds to remove.";
                return false;
            }

            VolumeCloudConfiguration configuration = FindConfiguration(FindHelper(body));
            var paths = configuration != null ? OwnedAssetPaths(configuration) : new List<string>();

            string localPath = ResolveLocalPrefabPath(body);
            if (!string.IsNullOrEmpty(localPath))
            {
                UnwireLocalPrefab(localPath);
            }

            string scaledPath = ResolvePrefabPath(body);
            if (!string.IsNullOrEmpty(scaledPath))
            {
                UnwireScaledPrefab(scaledPath);
            }

            if (configuration != null)
            {
                var settings = AddressableAssetSettingsDefaultObject.Settings;
                if (settings != null)
                {
                    settings.RemoveAssetEntry(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(configuration)));
                    VolumeCloudConfiguration low = AuthoringSidecars.Find(configuration)?.LowConfiguration;
                    if (low != null)
                    {
                        settings.RemoveAssetEntry(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(low)));
                    }
                }
            }

            var trashed = new List<string>();
            foreach (string path in paths)
            {
                if (AssetDatabase.MoveAssetToTrash(path))
                {
                    trashed.Add(Path.GetFileName(path));
                }
            }

            AssetDatabase.SaveAssets();
            message = trashed.Count > 0
                ? $"Removed the clouds and moved {string.Join(", ", trashed)} to the trash."
                : "Removed the cloud components. No configuration asset was found in the project.";
            return true;
        }

        // The configuration, its scaled counterpart and sidecar, and every texture it references from inside its own
        // folder, which is where the tools write noise and baked cubemaps.
        private static List<string> OwnedAssetPaths(VolumeCloudConfiguration configuration)
        {
            var paths = new List<string>();
            string configurationPath = AssetDatabase.GetAssetPath(configuration);
            string folder = Path.GetDirectoryName(configurationPath)?.Replace('\\', '/');
            AddIfOwned(paths, folder, configurationPath);
            VolumeCloudConfigurationAuthoring sidecar = AuthoringSidecars.Find(configuration);
            if (sidecar != null)
            {
                AddIfOwned(paths, folder, AssetDatabase.GetAssetPath(sidecar.ScaledConfiguration));
                AddIfOwned(paths, folder, AssetDatabase.GetAssetPath(sidecar.LowConfiguration));
                AddIfOwned(paths, folder, AssetDatabase.GetAssetPath(sidecar));
            }

            foreach (VolumeCloudConfiguration.CumulusData layer in configuration.cumulusList)
            {
                foreach (Texture texture in new Texture[]
                         {
                             layer.distributionMap, layer.cloudColorMap, layer.baseTexture, layer.detailTexture,
                             layer.bakedScaledTexture, layer.cloudNormalMap, layer.bakedBottomScaledTexture,
                         })
                {
                    AddIfOwned(paths, folder, texture != null ? AssetDatabase.GetAssetPath(texture) : null);
                }
            }

            return paths;
        }

        private static void AddIfOwned(List<string> paths, string folder, string path)
        {
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(folder) || paths.Contains(path))
                return;

            if (path.StartsWith(folder + "/"))
            {
                paths.Add(path);
            }
        }

        /// <summary>
        /// Creates a cumulus layer two to four kilometers above sea level, with stock Kerbin's main layer's shape.
        /// </summary>
        /// <returns>The layer, with no textures.</returns>
        public static VolumeCloudConfiguration.CumulusData CreateDefaultLayer() =>
            new()
            {
                isEnable = true,
                layerName = "Clouds",
                castShadow = true,
                cloudsType = VolumeCloudConfiguration.CloudsLayerType.Cumulus,
                cloudHeightRange = new Vector2(2000f, 4000f),
                bakeCloudMipmap = 10f,
                currentBakedCloudMipmap = 10f,
                enableWind = true,
                windDirection = Vector2.one,
                movementSpeed = 0.001f,
                evolveSpeed = 0.001f,
                baseTexureTile = 1f,
                coverageScale = 1f,
                evanish = 0.109f,
                detailAmount = 23.8f,
                upperFalloff = 12.5f,
                lowerFalloff = 50f,
                enableDetailTexture = true,
                detailTextureTile = 5f,
                detailStrength = 1f,
                cloudsDensity = 1.25f,
                normalScale = 1f,
            };

        private static void InitializeLists(VolumeCloudConfiguration configuration)
        {
            configuration.cumulusList ??= new List<VolumeCloudConfiguration.CumulusData>();
            configuration.cumulusIndex ??= new List<int>();
            configuration.lenticularList ??= new List<VolumeCloudConfiguration.CloudBoxSetting>();
            configuration.boxCloudsGroupList ??= new List<VolumeCloudConfiguration.BoxCloudsGroup>();
        }

        private static string ResolvePrefabPath(CoreCelestialBodyData body)
        {
            if (PrefabUtility.IsPartOfPrefabAsset(body))
                return AssetDatabase.GetAssetPath(body);

            return PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(body.gameObject);
        }

        private static string ResolveLocalPrefabPath(CoreCelestialBodyData body)
        {
            PQS pqs = BodyResolver.FindPqsIncludingAsset(body);
            if (pqs == null)
                return null;

            return PrefabUtility.IsPartOfPrefabAsset(pqs)
                ? AssetDatabase.GetAssetPath(pqs)
                : PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(pqs.gameObject);
        }

        private static T LoadOrCreate<T>(string folder, string assetName, out bool created) where T : ScriptableObject
        {
            EnsureFolder(folder);
            string path = $"{folder}/{assetName}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            created = existing == null;
            if (!created)
                return existing;

            var asset = ScriptableObject.CreateInstance<T>();
            asset.name = assetName;
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        // The helper loads the configuration through an asset reference, which needs an addressable entry. The address
        // itself is not read, so it is the configuration's name.
        private static bool RegisterAddressable(VolumeCloudConfiguration configuration)
        {
            var group = PlanetAuthoringAddressables.ResolveCelestialBodiesGroup(configuration);
            if (group == null)
                return false;

            AddressablesTools.MakeAddressable(group, AssetDatabase.GetAssetPath(configuration), configuration.name);
            return true;
        }

        private static void WireLocalPrefab(string localPath, VolumeCloudConfiguration configuration, VolumeCloudConfiguration low)
        {
            string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(configuration));
            string lowGuid = low != null ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(low)) : guid;
            GameObject root = PrefabUtility.LoadPrefabContents(localPath);
            try
            {
                PQS pqs = root.GetComponentInChildren<PQS>(true);
                if (pqs == null)
                    return;

                if (!pqs.TryGetComponent(out CloudRenderHelper helper))
                {
                    helper = pqs.gameObject.AddComponent<CloudRenderHelper>();
                }

                var serialized = new SerializedObject(helper);
                serialized.FindProperty("HighQualityCloudConfiguration.m_AssetGUID").stringValue = guid;
                serialized.FindProperty("MediumQualityCloudConfiguration.m_AssetGUID").stringValue = guid;
                serialized.FindProperty("LowQualityCloudConfiguration.m_AssetGUID").stringValue = lowGuid;

                // Stock helpers take their lights from the cloud light manager, which the game fills from the star.
                serialized.FindProperty("AutoGetLight").boolValue = true;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, localPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void WireScaledPrefab(string scaledPath, ScaledCloudConfiguration scaled)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(scaledPath);
            try
            {
                if (!root.TryGetComponent(out ScaledCloudDataModelComponent component))
                {
                    component = root.AddComponent<ScaledCloudDataModelComponent>();
                }

                component.ScaledCloudConfiguration = scaled;
                PrefabUtility.SaveAsPrefabAsset(root, scaledPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void UnwireLocalPrefab(string localPath)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(localPath);
            try
            {
                PQS pqs = root.GetComponentInChildren<PQS>(true);
                if (pqs != null && pqs.TryGetComponent(out CloudRenderHelper helper))
                {
                    Object.DestroyImmediate(helper);
                }

                PrefabUtility.SaveAsPrefabAsset(root, localPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void UnwireScaledPrefab(string scaledPath)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(scaledPath);
            try
            {
                if (root.TryGetComponent(out ScaledCloudDataModelComponent component))
                {
                    Object.DestroyImmediate(component);
                }

                PrefabUtility.SaveAsPrefabAsset(root, scaledPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
                return;

            string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}
