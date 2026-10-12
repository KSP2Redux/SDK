using System;
using System.Collections.Generic;
using KSP;
using KSP.Rendering;
using KSP.VolumeCloud;
using Ksp2UnityTools.Editor.PlanetAuthoring.Atmosphere;
using Ksp2UnityTools.Editor.PlanetAuthoring.Clouds;
using Ksp2UnityTools.Editor.Validation;
using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Validation.Validators.Environment
{
    /// <summary>
    /// Checks a body's volumetric clouds: that both prefabs are wired, that every quality tier has a configuration,
    /// that the configuration is sized to the body, and that each enabled layer has its maps, a sane height range and
    /// baked scaled clouds.
    /// </summary>
    /// <remarks>
    /// Bodies without clouds are skipped. A helper that references a stock configuration from the game's bundles is
    /// only checked for its wiring, since the configuration itself is not in the project.
    /// </remarks>
    public class CloudValidator : IPlanetValidator
    {
        /// <summary>Code for clouds wired into one prefab but not the other.</summary>
        public const string HALF_WIRED_CODE = "CLOUD_HALF_WIRED";

        /// <summary>Code for a cloud helper with no High quality configuration.</summary>
        public const string CONFIG_MISSING_CODE = "CLOUD_CONFIG_MISSING";

        /// <summary>Code for a cloud helper with a quality tier left empty.</summary>
        public const string TIER_MISSING_CODE = "CLOUD_TIER_MISSING";

        /// <summary>Code for a Low quality tier that draws volumetric clouds.</summary>
        public const string LOW_VOLUMETRIC_CODE = "CLOUD_LOW_VOLUMETRIC";

        /// <summary>Code for a configuration whose planet radius does not match the body.</summary>
        public const string RADIUS_MISMATCH_CODE = "CLOUD_RADIUS_MISMATCH";

        /// <summary>Code for a configuration with no enabled layers.</summary>
        public const string NO_LAYERS_CODE = "CLOUD_NO_LAYERS";

        /// <summary>Code for an enabled layer with no distribution map.</summary>
        public const string MAP_MISSING_CODE = "CLOUD_MAP_MISSING";

        /// <summary>Code for an enabled layer missing a noise volume.</summary>
        public const string NOISE_MISSING_CODE = "CLOUD_NOISE_MISSING";

        /// <summary>Code for a layer whose top is not above its bottom.</summary>
        public const string RANGE_INVERTED_CODE = "CLOUD_RANGE_INVERTED";

        /// <summary>Code for a layer that starts below sea level.</summary>
        public const string BELOW_SEA_LEVEL_CODE = "CLOUD_BELOW_SEA_LEVEL";

        /// <summary>Code for a layer that reaches past the top of the visual atmosphere.</summary>
        public const string ABOVE_ATMOSPHERE_CODE = "CLOUD_ABOVE_ATMOSPHERE";

        /// <summary>Code for an enabled layer with no baked scaled clouds.</summary>
        public const string UNBAKED_CODE = "CLOUD_UNBAKED";

        // The configuration stores its radius as a float, so a meter of slack absorbs the rounding.
        private const double RADIUS_TOLERANCE_METERS = 1.0;

        /// <inheritdoc />
        public BodyClassFlags AppliesTo => BodyClassFlags.SolidSurface;

        /// <summary>
        /// Gets a value indicating whether a Low quality tier's configuration draws volumetric clouds.
        /// </summary>
        /// <param name="low">The configuration the Low tier loads, or null when it is not a project asset.</param>
        /// <returns>True if the configuration draws volumetric clouds, false otherwise.</returns>
        public static bool DrawsVolumetricOnLow(VolumeCloudConfiguration low) => low != null && !low.useScaleCloudsOnly;

        // A stock configuration lives in the game's bundles and resolves to nothing here.
        private static VolumeCloudConfiguration LoadProjectConfiguration(string guid)
        {
            if (string.IsNullOrEmpty(guid))
                return null;

            string path = AssetDatabase.GUIDToAssetPath(guid);
            return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<VolumeCloudConfiguration>(path);
        }

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(CoreCelestialBodyData body)
        {
            if (body == null || body.Data == null)
                yield break;

            CloudRenderHelper helper = CloudSetup.FindHelper(body);
            bool hasScaled = body.TryGetComponent(out ScaledCloudDataModelComponent scaledComponent);
            if (helper == null && !hasScaled)
                yield break;

            if (helper == null || !hasScaled || scaledComponent.ScaledCloudConfiguration == null)
            {
                yield return new ValidationIssue(
                    HALF_WIRED_CODE,
                    ValidationSeverity.Warning,
                    "Clouds need the cloud helper on the Local prefab's PQS and the scaled cloud component, with its configuration, on the Scaled prefab. One is missing, so the body has either no clouds up close or none from orbit.",
                    new[] { new ValidationFix("Refit clouds", () => Refit(body)) }
                );
            }

            if (helper == null)
                yield break;

            string highGuid = helper.HighQualityCloudConfiguration?.AssetGUID;
            if (string.IsNullOrEmpty(highGuid))
            {
                yield return new ValidationIssue(
                    CONFIG_MISSING_CODE,
                    ValidationSeverity.Error,
                    "The cloud helper has no High quality configuration, so the body draws no clouds.",
                    new[] { new ValidationFix("Refit clouds", () => Refit(body)) }
                );
                yield break;
            }

            if (string.IsNullOrEmpty(helper.MediumQualityCloudConfiguration?.AssetGUID)
                || string.IsNullOrEmpty(helper.LowQualityCloudConfiguration?.AssetGUID))
            {
                yield return new ValidationIssue(
                    TIER_MISSING_CODE,
                    ValidationSeverity.Warning,
                    "The cloud helper has an empty quality tier, so players on that cloud quality setting get no clouds.",
                    new[] { new ValidationFix("Refit clouds", () => Refit(body)) }
                );
            }

            VolumeCloudConfiguration low = LoadProjectConfiguration(helper.LowQualityCloudConfiguration?.AssetGUID);
            if (DrawsVolumetricOnLow(low))
            {
                yield return new ValidationIssue(
                    LOW_VOLUMETRIC_CODE,
                    ValidationSeverity.Warning,
                    $"The Low quality tier loads {low.name}, which draws volumetric clouds. Stock's Low tiers draw scaled clouds only, so players on Low cloud quality pay for clouds they turned down.",
                    new[] { new ValidationFix("Refit clouds", () => Refit(body)) }
                );
            }

            VolumeCloudConfiguration configuration = CloudSetup.FindConfiguration(helper);
            if (configuration == null)
                yield break;

            if (!RadiusMatches(configuration.planetRadius, body.Data.radius))
            {
                yield return new ValidationIssue(
                    RADIUS_MISMATCH_CODE,
                    ValidationSeverity.Warning,
                    $"The cloud planet radius ({configuration.planetRadius:0} m) does not match the body radius ({body.Data.radius:0} m), so every layer sits {configuration.planetRadius - body.Data.radius:0} m off the heights it shows.",
                    new[] { new ValidationFix("Refit clouds", () => Refit(body)) }
                );
            }

            float atmosphereTopMeters = FindAtmosphereTop(body);
            bool anyEnabled = false;
            for (int index = 0; index < configuration.cumulusList.Count; index++)
            {
                VolumeCloudConfiguration.CumulusData layer = configuration.cumulusList[index];
                if (layer == null || !layer.isEnable)
                    continue;

                anyEnabled = true;
                foreach (ValidationIssue issue in ValidateLayer(configuration, index, atmosphereTopMeters))
                {
                    yield return issue;
                }
            }

            if (!anyEnabled)
            {
                yield return new ValidationIssue(
                    NO_LAYERS_CODE,
                    ValidationSeverity.Warning,
                    $"{configuration.name} has no enabled layers, so the body draws no clouds."
                );
            }
        }

        /// <summary>
        /// Gets a value indicating whether a configuration's planet radius matches the body's radius.
        /// </summary>
        /// <param name="planetRadius">The configuration's planet radius in meters.</param>
        /// <param name="bodyRadius">The body's radius in meters.</param>
        /// <returns>True if they agree to within a meter, false otherwise.</returns>
        public static bool RadiusMatches(float planetRadius, double bodyRadius) =>
            Math.Abs(planetRadius - bodyRadius) <= RADIUS_TOLERANCE_METERS;

        /// <summary>
        /// Gets a value indicating whether a layer's height range is empty or upside down.
        /// </summary>
        /// <param name="range">The layer's bottom and top, in meters.</param>
        /// <returns>True if the top is not above the bottom, false otherwise.</returns>
        public static bool IsRangeInverted(Vector2 range) => range.y <= range.x;

        /// <summary>
        /// Gets a value indicating whether a layer reaches past the top of the visual atmosphere.
        /// </summary>
        /// <remarks>
        /// The atmosphere shader skips whatever lies above its top, so a layer reaching past it is drawn without air in
        /// front of it there. A body with no visual atmosphere, given a top of zero, is not reported.
        /// </remarks>
        /// <param name="range">The layer's bottom and top, in meters above sea level.</param>
        /// <param name="atmosphereTopMeters">The visual atmosphere's height in meters, or zero when there is none.</param>
        /// <returns>True if the layer's top is above the atmosphere's, false otherwise.</returns>
        public static bool IsAboveAtmosphere(Vector2 range, float atmosphereTopMeters) =>
            atmosphereTopMeters > 0f && range.y > atmosphereTopMeters;

        private static IEnumerable<ValidationIssue> ValidateLayer(
            VolumeCloudConfiguration configuration,
            int index,
            float atmosphereTopMeters
        )
        {
            VolumeCloudConfiguration.CumulusData layer = configuration.cumulusList[index];
            string name = string.IsNullOrEmpty(layer.layerName) ? "An unnamed layer" : $"Layer '{layer.layerName}'";
            if (layer.distributionMap == null)
            {
                yield return new ValidationIssue(
                    MAP_MISSING_CODE,
                    ValidationSeverity.Error,
                    $"{name} has no distribution map, the cubemap of where its clouds form, so it draws nothing."
                );
            }

            if (layer.baseTexture == null || layer.detailTexture == null)
            {
                yield return new ValidationIssue(
                    NOISE_MISSING_CODE,
                    ValidationSeverity.Error,
                    $"{name} is missing a noise volume, so its clouds have no shape.",
                    new[] { new ValidationFix("Use stock noise", () => UseStockNoise(configuration)) }
                );
            }

            if (IsRangeInverted(layer.cloudHeightRange))
            {
                yield return new ValidationIssue(
                    RANGE_INVERTED_CODE,
                    ValidationSeverity.Error,
                    $"{name}'s top ({layer.cloudHeightRange.y:0} m) is not above its bottom ({layer.cloudHeightRange.x:0} m)."
                );
            }

            if (layer.cloudHeightRange.x < 0f)
            {
                yield return new ValidationIssue(
                    BELOW_SEA_LEVEL_CODE,
                    ValidationSeverity.Warning,
                    $"{name} starts {-layer.cloudHeightRange.x:0} m below sea level."
                );
            }

            if (IsAboveAtmosphere(layer.cloudHeightRange, atmosphereTopMeters))
            {
                yield return new ValidationIssue(
                    ABOVE_ATMOSPHERE_CODE,
                    ValidationSeverity.Warning,
                    $"{name} reaches {layer.cloudHeightRange.y:0} m, above the visual atmosphere's top ({atmosphereTopMeters:0} m), so its tops are drawn without air in front of them."
                );
            }

            if (layer.bakedScaledTexture == null)
            {
                yield return new ValidationIssue(
                    UNBAKED_CODE,
                    ValidationSeverity.Warning,
                    $"{name} has no baked scaled clouds, so the game shows no clouds from orbit once the body switches to scaled space.",
                    new[] { new ValidationFix("Bake scaled clouds", () => BakeScaled(configuration, index)) }
                );
            }
        }

        private static float FindAtmosphereTop(CoreCelestialBodyData body)
        {
            if (!body.TryGetComponent(out AtmosphereDataModelComponent component))
                return 0f;

            AtmosphereModel model = AtmosphereSetup.FindModel(component.AtmosphereModelKey, out _);
            return model != null ? model.AtmosphereHeight * 1000f : 0f;
        }

        private static void Refit(CoreCelestialBodyData body)
        {
            CloudSetup.TryAddClouds(body, out _, out string message);
            Debug.Log($"[Clouds] {message}");
        }

        private static void BakeScaled(VolumeCloudConfiguration configuration, int index)
        {
            ScaledCloudBaker.TryBake(configuration, index, out string message);
            Debug.Log($"[Clouds] {message}");
        }

        private static void UseStockNoise(VolumeCloudConfiguration configuration)
        {
            if (!StockCloudNoise.TryLink(out Texture3D baseNoise, out Texture3D detailNoise, out string problem))
            {
                Debug.LogWarning($"[Clouds] Stock noise could not be linked: {problem}.");
                return;
            }

            Undo.RecordObject(configuration, "Use Stock Cloud Noise");
            StockCloudNoise.AssignWhereMissing(configuration, baseNoise, detailNoise);
            EditorUtility.SetDirty(configuration);
        }
    }
}
