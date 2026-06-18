using System.Collections.Generic;
using KSP.Rendering.Planets;
using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Tools
{
    /// <summary>
    /// Computes the absolute terrain elevation at a body's pole by sampling the same heightmap
    /// combination the runtime uses inside <c>PQSJobUtil.HeightSample</c>.
    /// </summary>
    /// <remarks>
    /// At the pole the runtime evaluates
    /// <c>height = global * globalScale + sum_channels(largeTopSample * largeScale * mask)
    /// + sum_channels(mediumTopSample * mediumScale * mask)</c> because the latitude blend factor
    /// is 0 and <c>uvTop = (dir.x, dir.z) * 0.5 * uvScale</c> collapses to (0, 0). This helper
    /// reproduces that combination over the top two rows (north) or bottom two rows (south) of
    /// the global heightmap and biome mask, averaging across the row to absorb the equirectangular
    /// pole singularity, and returns the result in meters.
    /// </remarks>
    public static class PoleHeightAutoCalc
    {
        /// <summary>
        /// Attempts to compute the terrain elevation at the specified pole.
        /// </summary>
        /// <param name="pqsData">The body's PQS data. Must have <c>globalHeightMap</c> and <c>mask</c> set.</param>
        /// <param name="north">True to sample the north pole (top rows), false for south (bottom rows).</param>
        /// <param name="meters">The averaged elevation in meters when the call succeeds.</param>
        /// <returns>True if sampled successfully, false if required textures are missing or unreadable after attempt.</returns>
        public static bool TryComputeHeightAtPole(PQSData pqsData, bool north, out float meters)
        {
            meters = 0f;
            if (pqsData?.heightMapInfo == null) return false;
            PQSData.HeightMapInfo hmi = pqsData.heightMapInfo;
            if (hmi.globalHeightMap == null || hmi.mask == null) return false;

            EnsureReadable(hmi.globalHeightMap);
            EnsureReadable(hmi.mask);
            EnsureReadable(hmi.largeR.heightMap);
            EnsureReadable(hmi.largeG.heightMap);
            EnsureReadable(hmi.largeB.heightMap);
            EnsureReadable(hmi.largeA.heightMap);
            EnsureReadable(hmi.mediumR.heightMap);
            EnsureReadable(hmi.mediumG.heightMap);
            EnsureReadable(hmi.mediumB.heightMap);
            EnsureReadable(hmi.mediumA.heightMap);

            var globalData = hmi.globalHeightMap.GetRawTextureData<ushort>();
            int gw = hmi.globalHeightMap.width;
            int gh = hmi.globalHeightMap.height;
            if (globalData.Length < gw * gh) return false;

            Color32[] maskData = hmi.mask.GetPixels32();
            int mw = hmi.mask.width;
            int mh = hmi.mask.height;

            float globalScale = hmi.GetHeightScale();

            float largeR00 = SampleCorner(hmi.largeR.heightMap);
            float largeG00 = SampleCorner(hmi.largeG.heightMap);
            float largeB00 = SampleCorner(hmi.largeB.heightMap);
            float largeA00 = SampleCorner(hmi.largeA.heightMap);
            float mediumR00 = SampleCorner(hmi.mediumR.heightMap);
            float mediumG00 = SampleCorner(hmi.mediumG.heightMap);
            float mediumB00 = SampleCorner(hmi.mediumB.heightMap);
            float mediumA00 = SampleCorner(hmi.mediumA.heightMap);

            int globalRow0 = north ? 0 : gh - 1;
            int globalRow1 = north ? 1 : gh - 2;
            int maskRow0 = north ? 0 : mh - 1;
            int maskRow1 = north ? 1 : mh - 2;

            double sum = 0;
            int count = 0;
            for (int x = 0; x < gw; x++)
            {
                ushort g0 = globalData[globalRow0 * gw + x];
                ushort g1 = globalData[globalRow1 * gw + x];
                float globalNorm = (g0 + g1) * 0.5f / 65535f;
                float globalContrib = globalNorm * globalScale;

                int mx = (int)((long)x * mw / gw);
                Color32 m0 = maskData[maskRow0 * mw + mx];
                Color32 m1 = maskData[maskRow1 * mw + mx];
                float mr = (m0.r + m1.r) * 0.5f / 255f;
                float mg = (m0.g + m1.g) * 0.5f / 255f;
                float mb = (m0.b + m1.b) * 0.5f / 255f;
                float ma = (m0.a + m1.a) * 0.5f / 255f;
                float maskTotal = mr + mg + mb + ma;
                if (maskTotal < 0.001f) maskTotal = 1f;
                mr /= maskTotal; mg /= maskTotal; mb /= maskTotal; ma /= maskTotal;

                float largeContrib =
                    largeR00 * hmi.largeR.heightScale * mr +
                    largeG00 * hmi.largeG.heightScale * mg +
                    largeB00 * hmi.largeB.heightScale * mb +
                    largeA00 * hmi.largeA.heightScale * ma;
                float mediumContrib =
                    mediumR00 * hmi.mediumR.heightScale * mr +
                    mediumG00 * hmi.mediumG.heightScale * mg +
                    mediumB00 * hmi.mediumB.heightScale * mb +
                    mediumA00 * hmi.mediumA.heightScale * ma;

                sum += globalContrib + largeContrib + mediumContrib;
                count++;
            }

            if (count == 0) return false;
            meters = (float)(sum / count);
            return true;
        }

        private static float SampleCorner(Texture2D map)
        {
            if (map == null) return 0f;
            var data = map.GetRawTextureData<ushort>();
            if (data.Length == 0) return 0f;
            return data[0] / 65535f;
        }

        private static void EnsureReadable(Texture2D tex)
        {
            if (tex == null) return;
            string path = AssetDatabase.GetAssetPath(tex);
            if (string.IsNullOrEmpty(path)) return;
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null || importer.isReadable) return;
            importer.isReadable = true;
            importer.SaveAndReimport();
        }
    }
}
