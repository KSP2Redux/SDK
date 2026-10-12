using System;
using System.Collections.Generic;
using System.IO;
using KSP;
using KSP.Rendering;
using Ksp2UnityTools.Editor.PlanetAuthoring.Atmosphere;
using Ksp2UnityTools.Editor.Validation;
using UnityEditor;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Validation.Validators.Environment
{
    /// <summary>
    /// Checks a body's visual atmosphere: that its model resolves, its shells are wired, it is sized to
    /// the body, no visible air is drawn past the physics atmosphere, no terrain rises above its top,
    /// and its baked tables are current.
    /// </summary>
    /// <remarks>
    /// One validator rather than one per check, so the model is resolved once per refresh. Bodies
    /// without an atmosphere component are skipped.
    /// </remarks>
    public class AtmosphereValidator : IPlanetValidator
    {
        /// <summary>Code for an atmosphere component whose model key resolves to no Bruneton model.</summary>
        public const string MODEL_MISSING_CODE = "ATMO_MODEL_MISSING";

        /// <summary>Code for an atmosphere component missing its inner or outer shell renderer.</summary>
        public const string SHELLS_MISSING_CODE = "ATMO_SHELLS_MISSING";

        /// <summary>Code for a model whose bottom radius does not match the body radius.</summary>
        public const string RADIUS_MISMATCH_CODE = "ATMO_RADIUS_MISMATCH";

        /// <summary>Code for visible air drawn past the physics atmosphere.</summary>
        public const string AIR_PAST_DEPTH_CODE = "ATMO_AIR_PAST_DEPTH";

        /// <summary>Code for terrain that rises above the top of the visual atmosphere.</summary>
        public const string TERRAIN_ABOVE_TOP_CODE = "ATMO_TERRAIN_ABOVE_TOP";

        /// <summary>Code for a model with no baked lookup tables.</summary>
        public const string TABLES_MISSING_CODE = "ATMO_LUTS_MISSING";

        /// <summary>Code for baked lookup tables that predate the model's current settings.</summary>
        public const string TABLES_STALE_CODE = "ATMO_LUTS_STALE";

        // The model stores radius in kilometers as a float, so a metre of slack absorbs the rounding.
        private const double RADIUS_TOLERANCE_METERS = 1.0;

        // Air under a hundredth of its surface density adds no visible haze, so a visual atmosphere
        // that reaches past the physics depth is only a problem while the air there is denser.
        private const double VISIBLE_DENSITY_FRACTION = 0.01;

        // Headroom the raise fix leaves above the highest terrain.
        private const double TERRAIN_CLEARANCE_FRACTION = 0.05;

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(CoreCelestialBodyData body)
        {
            if (body == null || body.Data == null || !body.TryGetComponent(out AtmosphereDataModelComponent component))
                yield break;

            var serialized = new SerializedObject(component);
            if (serialized.FindProperty("_innerMeshRenderer")?.objectReferenceValue == null
                || serialized.FindProperty("_outerMeshRenderer")?.objectReferenceValue == null)
            {
                yield return new ValidationIssue(
                    SHELLS_MISSING_CODE,
                    ValidationSeverity.Error,
                    "The atmosphere component is missing its inner or outer shell. Run Refit Atmosphere to recreate them.");
            }

            AtmosphereModel model = AtmosphereSetup.FindModel(component.AtmosphereModelKey, out string problem);
            if (model == null)
            {
                yield return new ValidationIssue(MODEL_MISSING_CODE, ValidationSeverity.Error, $"No atmosphere model: {problem}.");
                yield break;
            }

            if (!RadiusMatches(model.BottomRadius, body.Data.radius))
            {
                yield return new ValidationIssue(
                    RADIUS_MISMATCH_CODE,
                    ValidationSeverity.Warning,
                    $"Atmosphere bottom radius ({model.BottomRadius:0.###} km) does not match the body radius ({body.Data.radius / 1000.0:0.###} km).",
                    new[] { new ValidationFix("Refit to body", () => Refit(model, body)) });
            }

            if (IsAirPastDepth(
                    model.AtmosphereHeight,
                    model.RayleighExponentialDistribution,
                    model.MieExponentialDistribution,
                    body.Data.atmosphereDepth))
            {
                double density = DensityAt(body.Data.atmosphereDepth / 1000.0, model.RayleighExponentialDistribution, model.MieExponentialDistribution);
                yield return new ValidationIssue(
                    AIR_PAST_DEPTH_CODE,
                    ValidationSeverity.Warning,
                    $"Visible air is still {density:P1} of its surface density at Atmosphere Depth ({body.Data.atmosphereDepth / 1000.0:0.#} km), and the visual atmosphere ({model.AtmosphereHeight:0.#} km) goes past it, so haze is drawn where there is no drag. Shorten the scale heights or raise Atmosphere Depth.");
            }

            if (IsTerrainAboveTop(model.AtmosphereHeight, body.Data.MinTerrainHeight, body.Data.MaxTerrainHeight))
            {
                yield return new ValidationIssue(
                    TERRAIN_ABOVE_TOP_CODE,
                    ValidationSeverity.Warning,
                    $"Terrain reaches {body.Data.MaxTerrainHeight / 1000.0:0.##} km, above the visual atmosphere top ({model.AtmosphereHeight:0.#} km). The atmosphere shader skips anything above the top, so a hard seam shows where terrain crosses it, even through the air below. Keep Visual Height above the highest terrain, with short scale heights if the air should stay low.",
                    new[] { new ValidationFix("Raise above terrain", () => RaiseAboveTerrain(model, body.Data.MaxTerrainHeight)) });
            }

            switch (AtmosphereSetup.GetTableState(model))
            {
                case AtmosphereTableState.Missing:
                    yield return new ValidationIssue(
                        TABLES_MISSING_CODE,
                        ValidationSeverity.Error,
                        "The atmosphere model has no baked lookup tables, so the game renders no atmosphere.",
                        new[] { new ValidationFix("Bake LUTs", () => Bake(model)) });
                    break;
                case AtmosphereTableState.Stale:
                    yield return new ValidationIssue(
                        TABLES_STALE_CODE,
                        ValidationSeverity.Warning,
                        "The atmosphere's baked lookup tables predate its current settings. The preview shows the new settings, but the game would not.",
                        new[] { new ValidationFix("Bake LUTs", () => Bake(model)) });
                    break;
            }
        }

        /// <summary>
        /// Gets a value indicating whether a model's bottom radius matches the body's radius.
        /// </summary>
        /// <param name="bottomRadiusKilometers">The model's bottom radius in kilometers.</param>
        /// <param name="bodyRadiusMeters">The body's radius in meters.</param>
        /// <returns>True if they agree to within a metre, false otherwise.</returns>
        public static bool RadiusMatches(float bottomRadiusKilometers, double bodyRadiusMeters) =>
            Math.Abs(bottomRadiusKilometers * 1000.0 - bodyRadiusMeters) <= RADIUS_TOLERANCE_METERS;

        /// <summary>
        /// Gets the density of the visible air at an altitude, as a fraction of its surface density.
        /// </summary>
        /// <remarks>
        /// The larger of the Rayleigh and Mie fractions, since either one alone shows as haze.
        /// </remarks>
        /// <param name="altitudeKilometers">The altitude above the atmosphere's bottom radius, in kilometers.</param>
        /// <param name="rayleighScaleHeightKilometers">The Rayleigh scale height in kilometers.</param>
        /// <param name="mieScaleHeightKilometers">The Mie scale height in kilometers.</param>
        /// <returns>The density fraction, from 0 to 1.</returns>
        public static double DensityAt(double altitudeKilometers, float rayleighScaleHeightKilometers, float mieScaleHeightKilometers) =>
            Math.Max(
                Math.Exp(-altitudeKilometers / Math.Max(0.001, rayleighScaleHeightKilometers)),
                Math.Exp(-altitudeKilometers / Math.Max(0.001, mieScaleHeightKilometers)));

        /// <summary>
        /// Gets a value indicating whether visible air is drawn past the physics atmosphere.
        /// </summary>
        /// <remarks>
        /// A visual atmosphere taller than the physics one is fine when the air above the physics
        /// depth is too thin to see, which is how air stays confined to low ground.
        /// </remarks>
        /// <param name="visualHeightKilometers">The model's visual height in kilometers.</param>
        /// <param name="rayleighScaleHeightKilometers">The Rayleigh scale height in kilometers.</param>
        /// <param name="mieScaleHeightKilometers">The Mie scale height in kilometers.</param>
        /// <param name="atmosphereDepthMeters">The body's Atmosphere Depth in meters.</param>
        /// <returns>True if the visual atmosphere passes the depth while the air there is still visible, false otherwise.</returns>
        public static bool IsAirPastDepth(
            float visualHeightKilometers,
            float rayleighScaleHeightKilometers,
            float mieScaleHeightKilometers,
            double atmosphereDepthMeters) =>
            visualHeightKilometers * 1000.0 > atmosphereDepthMeters + RADIUS_TOLERANCE_METERS
            && DensityAt(atmosphereDepthMeters / 1000.0, rayleighScaleHeightKilometers, mieScaleHeightKilometers) > VISIBLE_DENSITY_FRACTION;

        /// <summary>
        /// Gets a value indicating whether terrain rises above the top of the visual atmosphere.
        /// </summary>
        /// <remarks>
        /// Both heights are measured from sea level, which is also the atmosphere's bottom radius. A
        /// body whose terrain range was never computed, with both bounds still zero, is not reported.
        /// </remarks>
        /// <param name="visualHeightKilometers">The model's visual height in kilometers.</param>
        /// <param name="minTerrainHeightMeters">The body's Min Terrain Height in meters.</param>
        /// <param name="maxTerrainHeightMeters">The body's Max Terrain Height in meters.</param>
        /// <returns>True if the highest terrain is above the visual atmosphere's top, false otherwise.</returns>
        public static bool IsTerrainAboveTop(float visualHeightKilometers, double minTerrainHeightMeters, double maxTerrainHeightMeters) =>
            !(minTerrainHeightMeters == 0.0 && maxTerrainHeightMeters == 0.0)
            && maxTerrainHeightMeters > visualHeightKilometers * 1000.0;

        private static void RaiseAboveTerrain(AtmosphereModel model, double maxTerrainHeightMeters)
        {
            Undo.RecordObject(model, "Raise Atmosphere Above Terrain");
            double clearKilometers = maxTerrainHeightMeters * (1.0 + TERRAIN_CLEARANCE_FRACTION) / 1000.0;
            model.AtmosphereHeight = (float)(Math.Ceiling(clearKilometers * 10.0) / 10.0);
            EditorUtility.SetDirty(model);
        }

        private static void Refit(AtmosphereModel model, CoreCelestialBodyData body)
        {
            Undo.RecordObject(model, "Refit Atmosphere To Body");
            AtmosphereSetup.FitModelToBody(model, body.Data, created: false);
            EditorUtility.SetDirty(model);
        }

        private static void Bake(AtmosphereModel model)
        {
            string folder = Path.GetDirectoryName(AssetDatabase.GetAssetPath(model))?.Replace('\\', '/');
            if (string.IsNullOrEmpty(folder))
                return;

            BrunetonLutBaker.Bake(model, folder, out _);
        }
    }
}
