using System.IO;
using Ksp2UnityTools.Editor.PlanetAuthoring.Authoring;
using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Tools
{
    /// <summary>
    /// Creates new <see cref="PQSDecal" /> assets via menu and helper API.
    /// </summary>
    /// <remarks>
    /// Provides both an Assets/Create menu entry for general use and a programmatic
    /// <see cref="CreateAt" /> helper that body-inspector buttons call to drop a new template
    /// alongside a body's prefab.
    /// </remarks>
    public static class CreatePqsDecalAsset
    {

        [MenuItem("Assets/Create/Redux/Planet Authoring/PQS Decal", priority = 300)]
        private static void CreateInProjectWindow()
        {
            var folder = ResolveSelectedFolder();
            var path = AssetDatabase.GenerateUniqueAssetPath(folder + "/PQSDecal.asset");
            CreateAndPing(path);
        }

        /// <summary>
        /// Creates a new PQSDecal in the body's Decals subfolder.
        /// </summary>
        /// <remarks>The Decals subfolder is created if it does not already exist.</remarks>
        /// <param name="bodyFolder">The body's asset folder. Falls back to Assets if null, empty, or not a valid project folder.</param>
        /// <param name="baseName">The base filename for the new asset, before uniqueness suffixing.</param>
        /// <returns>The created PQSDecal asset.</returns>
        public static PQSDecal CreateAt(string bodyFolder, string baseName = "PQSDecal")
        {
            if (string.IsNullOrEmpty(bodyFolder)) bodyFolder = "Assets";
            if (!AssetDatabase.IsValidFolder(bodyFolder)) bodyFolder = "Assets";
            var decalsFolder = bodyFolder + "/Decals";
            if (!AssetDatabase.IsValidFolder(decalsFolder))
            {
                AssetDatabase.CreateFolder(bodyFolder, "Decals");
            }
            var path = AssetDatabase.GenerateUniqueAssetPath(decalsFolder + "/" + baseName + ".asset");
            return CreateAndPing(path);
        }

        /// <summary>
        /// Creates a new PQSDecal at the given folder with the supplied initial configuration and textures.
        /// </summary>
        /// <param name="bodyFolder">The body's asset folder, passed through to <see cref="CreateAt" />.</param>
        /// <param name="config">The new-decal configuration from the prompt window.</param>
        /// <returns>The created PQSDecal asset, or null if creation failed.</returns>
        public static PQSDecal CreateConfigured(string bodyFolder, Windows.NewDecalPromptWindow.Result config)
        {
            var decal = CreateAt(bodyFolder, config.Name);
            if (decal == null) return null;
            // Base-game value fields stay on the asset.
            decal.HeightScale = config.HeightScale;
            decal.HeightBlendMode = config.BlendMode;
            EditorUtility.SetDirty(decal);
            // Redux-only per-decal source textures live on the editor-only sidecar.
            var authoring = PlanetAuthoringRegistry.Instance.GetOrCreateDecalTemplate(decal.DecalID);
            authoring.Diffuse = config.Diffuse;
            authoring.Normal = config.Normal;
            authoring.AlphaMaskTexture = config.AlphaMaskTexture;
            authoring.Peak = config.Peak;
            authoring.Slope = config.Slope;
            EditorUtility.SetDirty(authoring);
            AssetDatabase.SaveAssets();
            return decal;
        }

        private static PQSDecal CreateAndPing(string path)
        {
            var decal = ScriptableObject.CreateInstance<PQSDecal>();
            decal.DecalID = Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(decal, path);
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(decal);
            return decal;
        }

        private static string ResolveSelectedFolder()
        {
            var active = Selection.activeObject;
            if (active == null) return "Assets";
            var path = AssetDatabase.GetAssetPath(active);
            if (string.IsNullOrEmpty(path)) return "Assets";
            return AssetDatabase.IsValidFolder(path) ? path : Path.GetDirectoryName(path)?.Replace('\\', '/') ?? "Assets";
        }
    }
}
