using System.Collections.Generic;
using KSP;
using KSP.Rendering.Planets;
using Ksp2UnityTools.Editor.PlanetAuthoring.Inspectors;
using Ksp2UnityTools.Editor.PlanetAuthoring.Tools;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Validation.Validators.Surface
{
    /// <summary>
    /// Warns when a small-layer cell's altitude window cannot overlap the planet's actual terrain range.
    /// </summary>
    /// <remarks>
    /// Implements the analytic half of the "layers silently drop out" check from PARAMS.md section 4. For each
    /// (biome C, layer i) cell that is not intentionally disabled, the trapezoid window
    /// <c>[center - down - fade, center + up + fade]</c> is intersected against <c>[MinTerrainHeight, MaxTerrainHeight]</c>.
    /// An empty intersection means the layer never wins on this body. Slope reachability is deferred until per-pixel
    /// sampling lands in a later pass.
    /// </remarks>
    public sealed class LayerReachabilityValidator : IPlanetValidator
    {
        /// <summary>
        /// Stable code identifying issues emitted by this validator.
        /// </summary>
        public const string Code = "LAYER_UNREACHABLE";

        /// <inheritdoc />
        public BodyClassFlags AppliesTo => BodyClassFlags.SolidSurface;

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(CoreCelestialBodyData body)
        {
            if (body?.Core?.data == null) yield break;
            PQS pqs = BodyResolver.FindPqsIncludingAsset(body);
            Material mat = pqs?.data?.materialSettings?.surfaceMaterial;
            if (mat == null) yield break;

            double minTerrain = body.Core.data.MinTerrainHeight;
            double maxTerrain = body.Core.data.MaxTerrainHeight;
            // Unset terrain range is reported separately by UnsetTerrainHeightRangeValidator. Skip here so we
            // don't double-warn the artist with a degenerate "every layer is unreachable" cascade.
            if (minTerrain == 0.0 && maxTerrain == 0.0) yield break;
            if (maxTerrain <= minTerrain) yield break;

            for (int biome = 0; biome < 4; biome++)
            {
                string c = PqsAuthoringNaming.BiomeChannels[biome];
                if (!mat.HasVector($"_SmallBiome{c}")) continue;
                Vector4 sliceIndices = mat.GetVector($"_SmallBiome{c}");
                Vector4 enables = mat.HasVector($"_SmallHeightWeight{c}") ? mat.GetVector($"_SmallHeightWeight{c}") : Vector4.zero;

                for (int layer = 0; layer < 4; layer++)
                {
                    if (sliceIndices[layer] < 0f) continue;
                    if (enables[layer] <= 0f) continue;

                    string paramName = $"_SmallBiome{c}HeightParams{layer + 1}";
                    if (!mat.HasVector(paramName)) continue;
                    Vector4 height = mat.GetVector(paramName);
                    float center = height.x;
                    float up = Mathf.Max(0f, height.y);
                    float down = Mathf.Max(0f, height.z);
                    float fade = Mathf.Max(0f, height.w);
                    float windowLow = center - down - fade;
                    float windowHigh = center + up + fade;
                    // A zero-width window means the layer is muted via the trapezoid (the disabled state).
                    // TODO: a future LAYER_HEIGHT_ZERO validator could call out cells where the artist set
                    // up/down/fade all to 0 unintentionally. For now treat as a no-op here.
                    if (windowHigh <= windowLow) continue;
                    if (windowHigh < minTerrain || windowLow > maxTerrain)
                    {
                        yield return new ValidationIssue(
                            Code,
                            ValidationSeverity.Warning,
                            $"Biome {c} / Layer {layer + 1} height window [{windowLow:0.#}, {windowHigh:0.#}] m is outside the planet's terrain range [{minTerrain:0.#}, {maxTerrain:0.#}] m. The layer never wins on this body.");
                    }
                }
            }
        }
    }
}
