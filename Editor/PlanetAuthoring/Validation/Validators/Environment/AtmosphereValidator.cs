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
    /// the body, it sits inside the physics atmosphere, and its baked tables are current.
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

        /// <summary>Code for a visual atmosphere that extends past the physics atmosphere.</summary>
        public const string HEIGHT_PAST_DEPTH_CODE = "ATMO_HEIGHT_PAST_DEPTH";

        /// <summary>Code for a model with no baked lookup tables.</summary>
        public const string TABLES_MISSING_CODE = "ATMO_LUTS_MISSING";

        /// <summary>Code for baked lookup tables that predate the model's current settings.</summary>
        public const string TABLES_STALE_CODE = "ATMO_LUTS_STALE";

        // The model stores radius in kilometers as a float, so a metre of slack absorbs the rounding.
        private const double RADIUS_TOLERANCE_METERS = 1.0;

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

            if (IsPastDepth(model.AtmosphereHeight, body.Data.atmosphereDepth))
            {
                yield return new ValidationIssue(
                    HEIGHT_PAST_DEPTH_CODE,
                    ValidationSeverity.Warning,
                    $"Visual atmosphere height ({model.AtmosphereHeight:0.#} km) extends past Atmosphere Depth ({body.Data.atmosphereDepth / 1000.0:0.#} km), so air is drawn where there is no drag. Stock keeps it inside.");
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
        /// Gets a value indicating whether a visual atmosphere reaches past the physics atmosphere.
        /// </summary>
        /// <param name="visualHeightKilometers">The model's visual height in kilometers.</param>
        /// <param name="atmosphereDepthMeters">The body's Atmosphere Depth in meters.</param>
        /// <returns>True if the visual height is greater than the depth, false otherwise.</returns>
        public static bool IsPastDepth(float visualHeightKilometers, double atmosphereDepthMeters) =>
            visualHeightKilometers * 1000.0 > atmosphereDepthMeters + RADIUS_TOLERANCE_METERS;

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
