using System;
using System.IO;
using System.Linq;
using Ksp2UnityTools.Editor.API;
using Redux.Definitions;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.ResourceMaps
{
    /// <summary>
    /// Writes a rendered density map out as the four artifacts the game needs to see a resource on a body.
    /// </summary>
    /// <remarks>
    /// Writing only the PNG is not enough. The runtime discovers maps by loading every addressable
    /// carrying the <see cref="CB_RESOURCES_LABEL" /> label, reading the definition JSON behind it,
    /// and resolving that definition's asset key through Addressables. A map missing any of those
    /// steps never loads, and nothing reports why.
    /// </remarks>
    public static class ResourceMapBakeOperation
    {
        /// <summary>
        /// Addressables label the runtime loads every celestial body resource definition by.
        /// </summary>
        public const string CB_RESOURCES_LABEL = "cb_resources";

        /// <summary>
        /// The paths one generate writes to.
        /// </summary>
        public readonly struct OutputPaths
        {
            /// <summary>
            /// Initializes the pair of project-relative output paths.
            /// </summary>
            /// <param name="mapPath">Project-relative path of the density map PNG.</param>
            /// <param name="definitionPath">Project-relative path of the definition JSON.</param>
            public OutputPaths(string mapPath, string definitionPath)
            {
                MapPath = mapPath;
                DefinitionPath = definitionPath;
            }

            /// <summary>Gets the project-relative path of the density map PNG.</summary>
            public string MapPath { get; }

            /// <summary>Gets the project-relative path of the definition JSON.</summary>
            public string DefinitionPath { get; }

            /// <summary>Gets a value indicating whether either output already exists on disk.</summary>
            public bool AnyExists => File.Exists(MapPath) || File.Exists(DefinitionPath);
        }

        /// <summary>
        /// Builds the output paths for one body and resource.
        /// </summary>
        /// <param name="mapFolder">Project-relative folder the density maps are written to.</param>
        /// <param name="definitionFolder">Project-relative folder the definition JSONs are written to.</param>
        /// <param name="bodyName">The celestial body's name.</param>
        /// <param name="resourceName">The resource's name.</param>
        /// <returns>The pair of paths a generate would write.</returns>
        public static OutputPaths GetOutputPaths(string mapFolder, string definitionFolder, string bodyName, string resourceName)
        {
            string stem = $"{bodyName}_{resourceName}";
            return new OutputPaths(
                $"{mapFolder.TrimEnd('/')}/{stem}.png",
                $"{definitionFolder.TrimEnd('/')}/{stem}.json"
            );
        }

        /// <summary>
        /// Writes a density map, its definition, and both Addressables entries.
        /// </summary>
        /// <param name="densities">The rendered density map, each value 0 to 1.</param>
        /// <param name="size">Side length of the map, in pixels.</param>
        /// <param name="bodyName">The celestial body's name, written into the definition.</param>
        /// <param name="resourceName">The resource's name, written into the definition.</param>
        /// <param name="mapBrightness">Overlay brightness written into the definition.</param>
        /// <param name="paths">Where to write the two files.</param>
        /// <param name="groupName">Name of the Addressables group both entries are registered into.</param>
        /// <returns>The imported density map texture.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="densities" /> is null.</exception>
        public static Texture2D Write(
            float[] densities,
            int size,
            string bodyName,
            string resourceName,
            int mapBrightness,
            OutputPaths paths,
            string groupName)
        {
            if (densities == null)
                throw new ArgumentNullException(nameof(densities));

            Texture2D imported = WriteMap(densities, size, paths.MapPath);
            WriteDefinition(bodyName, resourceName, mapBrightness, paths);
            Register(paths, groupName);
            return imported;
        }

        private static Texture2D WriteMap(float[] densities, int size, string projectPath)
        {
            string directory = Path.GetDirectoryName(projectPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
            try
            {
                texture.SetPixels32(ResourceMapRenderer.ToPixels(densities));
                texture.Apply(false, false);
                File.WriteAllBytes(projectPath, texture.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }

            AssetDatabase.ImportAsset(projectPath, ImportAssetOptions.ForceUpdate);
            if (AssetImporter.GetAtPath(projectPath) is TextureImporter importer)
            {
                ConfigureDensityMapImporter(importer, size);
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(projectPath);
        }

        // Deliberately identical to the settings on the maps already shipping under
        // ResourceDensityMaps, including sRGB being on for what is really data. Flipping that would
        // change what the game samples from every existing map and invalidate the mapBrightness
        // values tuned against them, so it belongs in its own migration rather than here.
        private static void ConfigureDensityMapImporter(TextureImporter importer, int size)
        {
            importer.textureType = TextureImporterType.Default;
            importer.textureShape = TextureImporterShape.Texture2D;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = false;
            // The runtime samples these with GetPixel, which needs the CPU copy kept.
            importer.isReadable = true;
            importer.mipmapEnabled = true;
            importer.streamingMipmaps = false;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Bilinear;
            importer.anisoLevel = 1;
            importer.npotScale = TextureImporterNPOTScale.ToNearest;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.crunchedCompression = false;
            importer.maxTextureSize = Mathf.Max(size, 32);
        }

        private static void WriteDefinition(string bodyName, string resourceName, int mapBrightness, OutputPaths paths)
        {
            string directory = Path.GetDirectoryName(paths.DefinitionPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // The asset key is the map's project path, which is also the address registered below.
            var definition = new CelestialBodyResourceDefinition
            {
                celestialBodyName = bodyName,
                resourceName = resourceName,
                resourceMapAssetKey = paths.MapPath,
                mapBrightness = mapBrightness,
            };

            // The definition's field is a float, so the serializer renders a whole number as "10.0".
            // Every shipped definition writes it plainly as "10", and the two parse identically, so
            // the suffix is dropped to keep a regenerated map's diff to what actually changed.
            string json = JsonUtility.ToJson(definition, true)
                .Replace($"\"mapBrightness\": {mapBrightness}.0", $"\"mapBrightness\": {mapBrightness}");

            File.WriteAllText(paths.DefinitionPath, json);
            AssetDatabase.ImportAsset(paths.DefinitionPath, ImportAssetOptions.ForceUpdate);
        }

        private static void Register(OutputPaths paths, string groupName)
        {
            AddressableAssetGroup group = ResolveGroup(groupName);
            if (group == null)
            {
                Debug.LogWarning(
                    $"[ResourceMapBakeOperation] No Addressables group named '{groupName}'. " +
                    $"'{Path.GetFileName(paths.MapPath)}' was written but will not load until both files are registered."
                );
                return;
            }

            // The map carries no label. It is resolved by address, which the definition holds.
            AddressablesTools.MakeAddressable(group, paths.MapPath, paths.MapPath);
            AddressablesTools.MakeAddressable(group, paths.DefinitionPath, paths.DefinitionPath, CB_RESOURCES_LABEL);
        }

        /// <summary>
        /// Finds the Addressables group both outputs register into.
        /// </summary>
        /// <param name="groupName">Name of the group to find.</param>
        /// <returns>The group, or null when Addressables is not initialized or no group has that name.</returns>
        public static AddressableAssetGroup ResolveGroup(string groupName)
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
                return null;
            return settings.groups.FirstOrDefault(group => group != null && group.Name == groupName);
        }
    }
}
