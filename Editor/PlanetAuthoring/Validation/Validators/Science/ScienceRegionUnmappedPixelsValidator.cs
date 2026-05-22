using System.Collections.Generic;
using KSP;
using KSP.Game.Science;
using Ksp2UnityTools.Editor.PlanetAuthoring.Science;
using Ksp2UnityTools.Editor.ScriptableObjects;
using UnityEditor;
using UnityEngine;
using Ksp2UnityTools.Editor.Validation;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Validation.Validators.Science
{
    /// <summary>
    /// Flags a Science Region source map that contains a meaningful percentage of pixels whose
    /// color matches no defined region within tolerance.
    /// </summary>
    /// <remarks>
    /// Unmapped pixels usually indicate JPEG artifacts, anti-aliased edges between regions, or
    /// stray colors the artist forgot to add as their own region. The bake routes them to the
    /// nearest match so they silently merge into a neighboring region. Threshold is deliberately
    /// loose (1% by area) to avoid noise from one-pixel artifacts. Result is cached per asset GUID
    /// keyed by the bake fingerprint so the per-tick cost stays bounded.
    /// </remarks>
    public sealed class ScienceRegionUnmappedPixelsValidator : IPlanetValidator
    {
        /// <summary>Stable code identifying issues emitted by this validator.</summary>
        public const string Code = "SR_UNMAPPED_PIXELS";

        /// <inheritdoc />
        public ValidatorCost Cost => ValidatorCost.Expensive;

        // 1% of total pixels. Below this is noise (anti-aliasing, single-pixel artifacts).
        // Above is enough that the artist probably has a missing region.
        private const float WarnThresholdFraction = 0.01f;

        // Sample every Nth pixel along each axis. 4 means 1/16 of pixels are inspected. On a
        // 2048x1024 map that's ~131k samples, fast enough to recompute on cache miss.
        private const int SampleStep = 4;

        private static readonly Dictionary<string, CacheEntry> Cache = new();

        private struct CacheEntry
        {
            public string Fingerprint;
            public int UnmappedSamples;
            public int TotalSamples;
        }

        public IEnumerable<ValidationIssue> Validate(CoreCelestialBodyData body)
        {
            string bodyName = body?.Data?.bodyName;
            if (string.IsNullOrEmpty(bodyName)) yield break;
            ScienceRegionData data = ScienceRegionAssetLocator.FindForBody(bodyName);
            if (data?.scienceRegionMap == null || !data.scienceRegionMap.isReadable) yield break;
            ScienceRegionData.ExtendedScienceRegionDefinition[] defs = data.information?.ScienceRegionDefinitions;
            if (defs == null) yield break;

            string assetPath = AssetDatabase.GetAssetPath(data);
            if (string.IsNullOrEmpty(assetPath)) yield break;
            string guid = AssetDatabase.AssetPathToGUID(assetPath);
            string fingerprint = ScienceRegionBaker.ComputeFingerprint(data);

            if (!Cache.TryGetValue(guid, out CacheEntry entry) || entry.Fingerprint != fingerprint)
            {
                entry = Recompute(data, defs);
                entry.Fingerprint = fingerprint;
                Cache[guid] = entry;
            }
            if (entry.TotalSamples == 0) yield break;

            float fraction = entry.UnmappedSamples / (float)entry.TotalSamples;
            if (fraction < WarnThresholdFraction) yield break;

            yield return new ValidationIssue(
                Code,
                ValidationSeverity.Warning,
                $"~{fraction * 100f:0.0}% of '{bodyName}'s science region map pixels match no defined region within {ScienceRegionConstants.ColorCollisionTolerance:0.00}. " +
                $"Open the Science Region inspector and use Import & cluster colors to capture the missing colors as their own regions.");
        }

        private static CacheEntry Recompute(ScienceRegionData data, ScienceRegionData.ExtendedScienceRegionDefinition[] defs)
        {
            // Pre-collect the MapId>=0 region colors as Color32 so the nearest-match math runs in
            // byte space against the source's raw bytes. No managed Color[] allocation per scan.
            var paletteColors = new List<Color32>(defs.Length);
            for (var i = 0; i < defs.Length; i++)
            {
                if (defs[i] != null && defs[i].MapId >= 0)
                {
                    paletteColors.Add(defs[i].RegionColor);
                }
            }
            if (paletteColors.Count == 0) return new CacheEntry { UnmappedSamples = 0, TotalSamples = 0 };

            // Nearest-color match in 0..255 byte space. Tolerance scales by 255.
            var thresh255 = ScienceRegionConstants.ColorCollisionTolerance * 255f;
            var threshSq = thresh255 * thresh255 * 3f;
            var tex = data.scienceRegionMap;
            // Texture format must be RGBA32 (or otherwise 4-bytes-per-pixel) for GetRawTextureData<Color32>
            // to return the right number of elements. Fall back to GetPixels32 (managed alloc) when not.
            Color32[] pixels;
            if (tex.format == TextureFormat.RGBA32 || tex.format == TextureFormat.ARGB32)
            {
                var raw = tex.GetRawTextureData<Color32>();
                pixels = new Color32[raw.Length];
                raw.CopyTo(pixels);
            }
            else
            {
                pixels = tex.GetPixels32();
            }
            var width = tex.width;
            var height = tex.height;
            int unmapped = 0, total = 0;
            for (var y = 0; y < height; y += SampleStep)
            for (var x = 0; x < width; x += SampleStep)
            {
                var px = pixels[y * width + x];
                if (px.a < 128) continue; // transparent areas treated as "no map" rather than unmapped pixels
                total++;
                if (NearestDistanceSquared(paletteColors, px) > threshSq) unmapped++;
            }
            return new CacheEntry { UnmappedSamples = unmapped, TotalSamples = total };
        }

        private static float NearestDistanceSquared(List<Color32> palette, Color32 px)
        {
            var best = float.MaxValue;
            for (var i = 0; i < palette.Count; i++)
            {
                float dr = palette[i].r - px.r, dg = palette[i].g - px.g, db = palette[i].b - px.b;
                var d = dr * dr + dg * dg + db * db;
                if (d < best) best = d;
            }
            return best;
        }
    }
}
