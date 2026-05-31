using System.IO;
using KSP;
using Ksp2UnityTools.Editor.API;
using Ksp2UnityTools.Editor.IO;
using Redux.Assets.PartIconRendering;
using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PartAuthoring.Tools
{
    /// <summary>
    /// Renders a part's icon to its PNG sidecar and registers it with the parent mod's addressables group.
    /// </summary>
    /// <remarks>
    /// The bake forces a transparent background and 2x supersample regardless of preview-time settings,
    /// so the saved icon is independent of whatever colour the in-editor preview happened to use.
    /// </remarks>
    public static class PartIconBaker
    {
        /// <summary>
        /// Bakes the icon for <paramref name="target" /> using the supplied render settings.
        /// </summary>
        /// <param name="target">The part to render.</param>
        /// <param name="settings">The render settings to use. The caller's instance is cloned, not mutated.</param>
        public static void Bake(CorePartData target, PartIconRenderSettings settings)
        {
            Bake(target, settings, true);
        }

        /// <summary>
        /// Bakes the icon for <paramref name="target" /> using the supplied render settings.
        /// </summary>
        /// <param name="target">The part to render.</param>
        /// <param name="settings">The render settings to use. The caller's instance is cloned, not mutated.</param>
        /// <param name="showDialog">When true, shows the usual completion/failure dialogs.</param>
        public static void Bake(CorePartData target, PartIconRenderSettings settings, bool showDialog)
        {
            if (target == null || target.Core == null || settings == null)
            {
                return;
            }

            string prefabPath = PathUtils.GetPrefabOrAssetPath(target, target.gameObject);
            if (string.IsNullOrEmpty(prefabPath))
            {
                return;
            }

            string partName = ResolvePartName(target);

            var bakeSettings = settings.Clone();
            bakeSettings.backgroundColor = new Color(0f, 0f, 0f, 0f);
            bakeSettings.supersampleScale = 2;
            bakeSettings.makeTextureNoLongerReadable = false;

            Texture2D texture = PartIconRenderer.RenderTexture2D(
                partName + "-editor-saved-icon",
                target.Core,
                target.gameObject,
                bakeSettings
            );
            if (texture == null)
            {
                if (showDialog)
                {
                    EditorUtility.DisplayDialog(
                        "Icon Export Failed",
                        "No renderable meshes were found for this part.",
                        "OK"
                    );
                }
                return;
            }

            string path = Path.Combine(Path.GetDirectoryName(prefabPath), $"{partName}_icon.png")
                .Replace('\\', '/');
            try
            {
                Directory.CreateDirectory(new FileInfo(path).DirectoryName);
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }

            AssetDatabase.ImportAsset(path);
            ConfigureIconTextureImporter(path);

            bool madeAddressable = false;
            var group = PartAuthoringAddressables.ResolveIconGroup(target);
            if (group != null)
            {
                madeAddressable = true;
                AddressablesTools.MakeAddressable(
                    group,
                    path,
                    $"{partName}_icon.png"
                );
            }

            AssetDatabase.Refresh();
            AssetDatabase.SaveAssets();
            if (showDialog)
            {
                EditorUtility.DisplayDialog(
                    "Part Icon Exported",
                    !madeAddressable
                        ? $"Icon is at: {path}, you need to manually make it addressable"
                        : $"Icon is at: {path}",
                    "OK"
                );
            }
        }

        /// <summary>
        /// Bakes the icon for <paramref name="target" /> using the default render settings derived from the part's core data.
        /// </summary>
        /// <param name="target">The part to render.</param>
        public static void Bake(CorePartData target)
        {
            if (target == null || target.Core == null)
            {
                return;
            }
            Bake(target, PartIconRenderSettings.CreateDefault(target.Core));
        }

        /// <summary>
        /// Bakes the icon for <paramref name="target" /> using default render settings.
        /// </summary>
        /// <param name="target">The part to render.</param>
        /// <param name="showDialog">When true, shows the usual completion/failure dialogs.</param>
        public static void Bake(CorePartData target, bool showDialog)
        {
            if (target == null || target.Core == null)
            {
                return;
            }
            Bake(target, PartIconRenderSettings.CreateDefault(target.Core), showDialog);
        }

        private static string ResolvePartName(CorePartData target)
        {
            return !string.IsNullOrWhiteSpace(target.Core?.data?.partName)
                ? target.Core.data.partName
                : target.gameObject.name;
        }

        private static void ConfigureIconTextureImporter(string path)
        {
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
            {
                return;
            }
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.sRGBTexture = true;
            importer.SaveAndReimport();
        }
    }
}
