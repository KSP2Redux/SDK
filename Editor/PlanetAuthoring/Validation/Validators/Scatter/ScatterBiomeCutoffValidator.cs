using System.Collections.Generic;
using AwesomeTechnologies.VegetationSystem;
using KSP;
using Ksp2UnityTools.Editor.PlanetAuthoring.Scatter;
using Ksp2UnityTools.Editor.Validation;
using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Validation.Validators.Scatter
{
    /// <summary>
    /// Errors when a package's biome cutoff is above anything its mask channel actually contains.
    /// </summary>
    /// <remarks>
    /// <c>BiomeCutoffWeight</c> is the minimum channel weight at which a package claims a point. Set
    /// it above the brightest value that channel reaches anywhere on the body and the package can
    /// never claim anything, so every item in it silently spawns nowhere while the package, its
    /// items and its rules all read as correct.
    ///
    /// Expensive, because the cutoff is a single float but answering "is there anywhere this channel
    /// is bright enough" means scanning the texture. Drast's mask is 4096x4096, which is 16.7 million
    /// pixels and nothing to run at the inspector's tick rate. The validator sits behind a manual
    /// "Run full validation", so the cost is paid when the button is pressed.
    ///
    /// Every pixel is read. A maximum is an extremum, so sampling a subset can under-report it: the
    /// brightest pixel in a channel can be a single pixel, and sampling one in sixteen misses it
    /// fifteen times in sixteen. An underestimated maximum silently clears a cutoff that really does
    /// exclude everything, which is the failure this exists to catch.
    ///
    /// One scan per body, not per package. Four packages on one mask is still one pass, and the
    /// result is cached against the mask's GUID and import timestamp so re-running is free until the
    /// texture changes.
    /// </remarks>
    public sealed class ScatterBiomeCutoffValidator : IPlanetValidator
    {
        /// <summary>
        /// Stable code identifying issues emitted by this validator.
        /// </summary>
        public const string Code = "SCATTER_CUTOFF_EXCLUDES_ALL";

        private static readonly Dictionary<string, CacheEntry> Cache = new();

        /// <inheritdoc />
        public BodyClassFlags AppliesTo => BodyClassFlags.SolidSurface;

        /// <inheritdoc />
        public ValidatorCost Cost => ValidatorCost.Expensive;

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(CoreCelestialBodyData body)
        {
            VegetationSystemPro system = ScatterValidatorHelper.FindSystem(body);
            if (system == null || !ScatterBiomeChannels.MaskingActive(system))
                yield break;

            Texture2D mask = system.BiomeTextureMask;
            if (!mask.isReadable)
            {
                // ScatterMaskTextureValidator already reports this, and reports it cheaply.
                yield break;
            }

            string path = AssetDatabase.GetAssetPath(mask);
            string guid = AssetDatabase.AssetPathToGUID(path);
            if (string.IsNullOrEmpty(guid))
                yield break;

            // Timestamped as well as keyed, because editing the mask leaves the GUID and often the
            // dimensions untouched, and a stale maximum is exactly the wrong thing to keep.
            AssetImporter importer = AssetImporter.GetAtPath(path);
            double stamp = importer != null ? importer.assetTimeStamp : 0d;

            if (!Cache.TryGetValue(guid, out CacheEntry entry) || entry.TimeStamp != stamp)
            {
                entry = Recompute(mask);
                entry.TimeStamp = stamp;
                Cache[guid] = entry;
            }

            foreach (VegetationPackagePro package in system.VegetationPackageProList)
            {
                if (package == null || package.BiomeType == BiomeType.Default ||
                    !ScatterBiomeChannels.TryGetMaskChannel(system, package.BiomeType, out int channel))
                    continue;

                float peak = entry.ChannelMaxima[channel];
                if (package.BiomeCutoffWeight <= peak)
                    continue;

                yield return new ValidationIssue(
                    Code,
                    ValidationSeverity.Error,
                    $"Package '{ScatterValidatorHelper.PackageLabel(package)}' needs channel "
                    + $"{PlanetAuthoringNaming.BiomeChannels[channel]} to reach {package.BiomeCutoffWeight:0.###}, "
                    + $"but it never rises above {peak:0.###} anywhere on the mask. Nothing in this package can spawn.");
            }
        }

        private static CacheEntry Recompute(Texture2D mask)
        {
            var entry = new CacheEntry { ChannelMaxima = new float[4] };

            Color32[] pixels = mask.GetPixels32();
            byte red = 0, green = 0, blue = 0, alpha = 0;
            foreach (Color32 pixel in pixels)
            {
                if (pixel.r > red) red = pixel.r;
                if (pixel.g > green) green = pixel.g;
                if (pixel.b > blue) blue = pixel.b;
                if (pixel.a > alpha) alpha = pixel.a;
            }

            entry.ChannelMaxima[0] = red / 255f;
            entry.ChannelMaxima[1] = green / 255f;
            entry.ChannelMaxima[2] = blue / 255f;
            entry.ChannelMaxima[3] = alpha / 255f;
            return entry;
        }

        private struct CacheEntry
        {
            public double TimeStamp;
            public float[] ChannelMaxima;
        }
    }
}
