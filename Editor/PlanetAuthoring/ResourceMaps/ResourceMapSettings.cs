using System.IO;
using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.ResourceMaps
{
    /// <summary>
    /// Per-machine settings for the Resource Maps window, stored in <see cref="EditorPrefs" />.
    /// </summary>
    /// <remarks>
    /// Follows the same convention as the other Planet Authoring tools, which each keep their
    /// settings under a <c>Ksp2UnityTools.&lt;Tool&gt;.</c> prefix rather than in a shared asset.
    /// Per-machine is the right scope here because the biome mask folder holds ripped stock art that
    /// stays out of version control, so each contributor supplies their own copy.
    /// </remarks>
    public static class ResourceMapSettings
    {
        private const string PREFS_PREFIX = "Ksp2UnityTools.ResourceMaps.";

        /// <summary>
        /// Samples per output pixel along each axis.
        /// </summary>
        /// <remarks>
        /// Fixed rather than configurable so a map's appearance never depends on a setting someone
        /// forgot they changed. Two matches the 4096 pixel masks against a 2048 pixel output, which
        /// keeps biome boundaries smooth and stops high frequency noise from aliasing.
        /// </remarks>
        public const int SUPERSAMPLE = 2;

        /// <summary>Default folder the density map PNGs are written to.</summary>
        public const string DEFAULT_MAP_FOLDER = "Assets/ReduxAssets/Definitions/ResourceSystem/ResourceDensityMaps";

        /// <summary>Default folder the definition JSONs are written to.</summary>
        public const string DEFAULT_DEFINITION_FOLDER = "Assets/ReduxAssets/Definitions/ResourceSystem/CelestialBodyResources";

        /// <summary>Default folder new authoring assets are created in.</summary>
        public const string DEFAULT_AUTHORING_FOLDER = "Assets/ReduxAssets/Definitions/ResourceSystem/ResourceMapAuthoring";

        /// <summary>Default folder biome masks are copied into, relative to the repository root.</summary>
        public const string DEFAULT_MASK_FOLDER = "reference/biome-masks";

        /// <summary>Default Addressables group both outputs are registered into.</summary>
        public const string DEFAULT_GROUP_NAME = "Resource Maps";

        /// <summary>Default side length of a generated map, in pixels.</summary>
        public const int DEFAULT_OUTPUT_SIZE = 2048;

        /// <summary>Default side length of the interactive previews, in pixels.</summary>
        public const int DEFAULT_PREVIEW_SIZE = 512;

        /// <summary>Gets or sets the folder the density map PNGs are written to.</summary>
        public static string MapFolder
        {
            get => EditorPrefs.GetString(PREFS_PREFIX + "MapFolder", DEFAULT_MAP_FOLDER);
            set => EditorPrefs.SetString(PREFS_PREFIX + "MapFolder", value);
        }

        /// <summary>Gets or sets the folder the definition JSONs are written to.</summary>
        public static string DefinitionFolder
        {
            get => EditorPrefs.GetString(PREFS_PREFIX + "DefinitionFolder", DEFAULT_DEFINITION_FOLDER);
            set => EditorPrefs.SetString(PREFS_PREFIX + "DefinitionFolder", value);
        }

        /// <summary>Gets or sets the folder new authoring assets are created in.</summary>
        public static string AuthoringFolder
        {
            get => EditorPrefs.GetString(PREFS_PREFIX + "AuthoringFolder", DEFAULT_AUTHORING_FOLDER);
            set => EditorPrefs.SetString(PREFS_PREFIX + "AuthoringFolder", value);
        }

        /// <summary>Gets or sets the biome mask folder, relative to the repository root.</summary>
        public static string MaskFolder
        {
            get => EditorPrefs.GetString(PREFS_PREFIX + "MaskFolder", DEFAULT_MASK_FOLDER);
            set => EditorPrefs.SetString(PREFS_PREFIX + "MaskFolder", value);
        }

        /// <summary>Gets or sets the Addressables group both outputs are registered into.</summary>
        public static string GroupName
        {
            get => EditorPrefs.GetString(PREFS_PREFIX + "GroupName", DEFAULT_GROUP_NAME);
            set => EditorPrefs.SetString(PREFS_PREFIX + "GroupName", value);
        }

        /// <summary>Gets or sets the side length of a generated map, in pixels.</summary>
        public static int OutputSize
        {
            get => EditorPrefs.GetInt(PREFS_PREFIX + "OutputSize", DEFAULT_OUTPUT_SIZE);
            set => EditorPrefs.SetInt(PREFS_PREFIX + "OutputSize", value);
        }

        /// <summary>Gets or sets the side length of the interactive previews, in pixels.</summary>
        public static int PreviewSize
        {
            get => EditorPrefs.GetInt(PREFS_PREFIX + "PreviewSize", DEFAULT_PREVIEW_SIZE);
            set => EditorPrefs.SetInt(PREFS_PREFIX + "PreviewSize", value);
        }

        /// <summary>
        /// Gets the repository root, which is the folder containing the project's Assets folder.
        /// </summary>
        public static string RepositoryRoot =>
            Path.GetDirectoryName(Application.dataPath)?.Replace('\\', '/') ?? "";

        /// <summary>
        /// Resolves the absolute path of the biome mask folder.
        /// </summary>
        /// <returns>The mask folder as an absolute path.</returns>
        public static string GetAbsoluteMaskFolder()
        {
            string folder = MaskFolder;
            if (Path.IsPathRooted(folder))
                return folder.Replace('\\', '/');
            return $"{RepositoryRoot}/{folder.Replace('\\', '/').TrimStart('/')}";
        }

        /// <summary>
        /// Resolves the absolute path of a mask file within the mask folder.
        /// </summary>
        /// <param name="fileName">File name of the mask.</param>
        /// <returns>The mask's absolute path, or an empty string when <paramref name="fileName" /> is empty.</returns>
        public static string GetAbsoluteMaskPath(string fileName) =>
            string.IsNullOrEmpty(fileName) ? "" : $"{GetAbsoluteMaskFolder()}/{fileName}";
    }
}
