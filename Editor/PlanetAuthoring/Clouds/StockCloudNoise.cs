using System;
using KSP.VolumeCloud;
using Ksp2UnityTools.Editor.LinkedAddressables;
using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Clouds
{
    /// <summary>
    /// Links stock's cloud noise volumes into the project so cloud layers can use them.
    /// </summary>
    /// <remarks>
    /// The links are linked Addressables assets: the project holds a descriptor, and builds point the reference back
    /// at the stock asset rather than shipping a copy. These are the RGBA volumes some stock Kerbin layers use. The
    /// single-channel copies its main layer uses are not addressable on their own.
    /// </remarks>
    public static class StockCloudNoise
    {
        /// <summary>
        /// The address of stock's low-frequency base noise volume.
        /// </summary>
        public const string BASE_ADDRESS = "cloud_basenoise_lowfrequency.asset";

        /// <summary>
        /// The address of stock's high-frequency detail noise volume.
        /// </summary>
        public const string DETAIL_ADDRESS = "cloud_basenoise_heighfrequency.asset";

        /// <summary>
        /// Links stock's base and detail noise, reusing existing links.
        /// </summary>
        /// <param name="baseNoise">The linked base noise, or null on failure.</param>
        /// <param name="detailNoise">The linked detail noise, or null on failure.</param>
        /// <param name="problem">Why the noise could not be linked, or an empty string on success.</param>
        /// <returns>True if both volumes were linked, false otherwise.</returns>
        public static bool TryLink(out Texture3D baseNoise, out Texture3D detailNoise, out string problem)
        {
            baseNoise = null;
            detailNoise = null;
            if (!EditorPqsBootstrap.EnsureStockCatalog())
            {
                problem = "the base-game catalog is not registered";
                return false;
            }

            try
            {
                LinkedAddressableEditorCatalog.EnsureLoaded();
                baseNoise = AssetDatabase.LoadAssetAtPath<Texture3D>(
                    LinkedAddressableAssetUtility.CreateLink(BASE_ADDRESS, typeof(Texture3D))
                );
                detailNoise = AssetDatabase.LoadAssetAtPath<Texture3D>(
                    LinkedAddressableAssetUtility.CreateLink(DETAIL_ADDRESS, typeof(Texture3D))
                );
            }
            catch (Exception e)
            {
                problem = e.Message;
                return false;
            }

            problem = baseNoise == null || detailNoise == null ? "the linked noise did not import" : string.Empty;
            return string.IsNullOrEmpty(problem);
        }

        /// <summary>
        /// Gives every layer without noise the stock noise volumes.
        /// </summary>
        /// <param name="configuration">The configuration whose layers to fill.</param>
        /// <param name="baseNoise">The base noise volume.</param>
        /// <param name="detailNoise">The detail noise volume.</param>
        /// <returns>The number of layers that were given noise.</returns>
        public static int AssignWhereMissing(VolumeCloudConfiguration configuration, Texture3D baseNoise, Texture3D detailNoise)
        {
            int assigned = 0;
            foreach (VolumeCloudConfiguration.CumulusData layer in configuration.cumulusList)
            {
                if (layer.baseTexture != null && layer.detailTexture != null)
                    continue;

                if (layer.baseTexture == null)
                {
                    layer.baseTexture = baseNoise;
                }

                if (layer.detailTexture == null)
                {
                    layer.detailTexture = detailNoise;
                }

                assigned++;
            }

            return assigned;
        }
    }
}
