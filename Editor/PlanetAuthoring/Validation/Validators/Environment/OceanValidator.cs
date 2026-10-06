using System.Collections.Generic;
using KSP;
using KSP.Rendering.Planets;
using Ksp2UnityTools.Editor.PlanetAuthoring.Ocean;
using Ksp2UnityTools.Editor.Validation;
using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Validation.Validators.Environment
{
    /// <summary>
    /// Checks that a body with Has Ocean on has what its PQS renderer needs to draw water: a wave spectrum and a material
    /// on the ocean shader.
    /// </summary>
    /// <remarks>
    /// A material on the SDK's stand-in, or one that lost its shader, is fine, since the renderer draws with a copy on the
    /// game's ocean shader. One on any other shader is refused by the renderer, which then draws no ocean.
    /// </remarks>
    public class OceanValidator : IPlanetValidator
    {
        /// <summary>Code for an ocean body with no wave spectrum.</summary>
        public const string NO_SPECTRUM_CODE = "OCEAN_NO_SPECTRUM";

        /// <summary>Code for an ocean body with neither a water nor a lava material.</summary>
        public const string NO_MATERIAL_CODE = "OCEAN_NO_MATERIAL";

        /// <summary>Code for an ocean material on a shader the renderer refuses.</summary>
        public const string WRONG_SHADER_CODE = "OCEAN_WRONG_SHADER";

        /// <summary>Code for an ocean material with no shoreline mask.</summary>
        public const string SHORELINE_MISSING_CODE = "OCEAN_SHORELINE_MISSING";

        /// <summary>Code for a shoreline mask made from terrain or a sea level that has since changed.</summary>
        public const string SHORELINE_STALE_CODE = "OCEAN_SHORELINE_STALE";

        private const string WATER_SHADER = "KSP2/Environment/Ocean/OceanWater";
        private const string LAVA_SHADER = "KSP2/Environment/Ocean/OceanLavaShader";

        /// <inheritdoc />
        public BodyClassFlags AppliesTo => BodyClassFlags.SolidSurface;

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(CoreCelestialBodyData body)
        {
            PQSRenderer renderer = OceanSetup.FindRenderer(body);
            if (renderer == null)
                return new List<ValidationIssue>();

            var issues = new List<ValidationIssue>(Check(body, renderer._oceanSpectrum, renderer.OceanWaterMaterial, renderer.OceanLavaMaterial));
            ValidationIssue? shoreline = CheckShoreline(body, renderer.OceanWaterMaterial);
            if (shoreline.HasValue)
            {
                issues.Add(shoreline.Value);
            }

            return issues;
        }

        // A project material is the only kind the SDK makes a shoreline for.
        private static ValidationIssue? CheckShoreline(CoreCelestialBodyData body, Material water)
        {
            if (body?.Data == null || !body.Data.hasOcean || water == null || string.IsNullOrEmpty(AssetDatabase.GetAssetPath(water)))
                return null;

            var fix = new[] { new ValidationFix("Generate shoreline", () => OceanShoreline.TryBake(body, water, out _)) };
            return OceanShoreline.GetState(body, water) switch
            {
                ShorelineState.Missing => new ValidationIssue(
                    SHORELINE_MISSING_CODE,
                    ValidationSeverity.Warning,
                    "The ocean has no shoreline mask, so waves run at full height right up to the coast.",
                    fix
                ),
                ShorelineState.Stale => new ValidationIssue(
                    SHORELINE_STALE_CODE,
                    ValidationSeverity.Warning,
                    "The ocean's shoreline mask was made before the terrain or Ocean Altitude last changed, so the coast it calms waves along is in the wrong place.",
                    fix
                ),
                _ => null,
            };
        }

        /// <summary>
        /// Checks a body's ocean assets as its PQS renderer holds them.
        /// </summary>
        /// <param name="body">The body.</param>
        /// <param name="spectrum">The renderer's wave spectrum.</param>
        /// <param name="water">The renderer's water material.</param>
        /// <param name="lava">The renderer's lava material.</param>
        /// <returns>The issues found.</returns>
        public static IEnumerable<ValidationIssue> Check(CoreCelestialBodyData body, OceanWaveSpectrum spectrum, Material water, Material lava)
        {
            if (body == null || body.Data == null || !body.Data.hasOcean)
                yield break;

            if (spectrum == null)
            {
                yield return new ValidationIssue(
                    NO_SPECTRUM_CODE,
                    ValidationSeverity.Error,
                    "Has Ocean is on but the PQS renderer has no wave spectrum, so no water is drawn.",
                    new[] { new ValidationFix("Add ocean", () => OceanSetup.TryAddOcean(body, "Kerbin", out _)) }
                );
            }

            if (water == null && lava == null)
            {
                yield return new ValidationIssue(
                    NO_MATERIAL_CODE,
                    ValidationSeverity.Error,
                    "Has Ocean is on but the PQS renderer has no water or lava material, so no water is drawn.",
                    new[] { new ValidationFix("Add ocean", () => OceanSetup.TryAddOcean(body, "Kerbin", out _)) }
                );
            }

            // The renderer uses the water material when both are set, so only that one is checked then.
            Material used = water != null ? water : lava;
            string expected = water != null ? WATER_SHADER : LAVA_SHADER;
            if (used != null && !PQSRenderer.NeedsGameOceanShader(used) && used.shader.name != expected)
            {
                yield return new ValidationIssue(
                    WRONG_SHADER_CODE,
                    ValidationSeverity.Error,
                    $"{used.name} uses {used.shader.name}, but the PQS renderer only draws an ocean with {expected}, so no water is drawn."
                );
            }
        }
    }
}
